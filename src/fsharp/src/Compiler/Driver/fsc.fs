// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

// Driver for F# compiler.
//
// Roughly divides into:
//    - Parsing
//    - Flags
//    - Importing IL assemblies
//    - Compiling (including optimizing)
//    - Linking (including ILX-IL transformation)

module internal FSharp.Compiler.Driver

open System
open System.Collections.Generic
open System.Diagnostics
open System.Globalization
open System.IO
open System.Reflection
open System.Text
open System.Threading

open Internal.Utilities
open Internal.Utilities.TypeHashing
open Internal.Utilities.Library
open Internal.Utilities.Library.Extras

open FSharp.Compiler
open FSharp.Compiler.AbstractIL
open FSharp.Compiler.AbstractIL.IL
open FSharp.Compiler.AbstractIL.ILBinaryReader
open FSharp.Compiler.AccessibilityLogic
open FSharp.Compiler.CheckDeclarations
open FSharp.Compiler.CompilerConfig
open FSharp.Compiler.CompilerDiagnostics
open FSharp.Compiler.CompilerImports
open FSharp.Compiler.CompilerOptions
open FSharp.Compiler.CreateILModule
open FSharp.Compiler.DependencyManager
open FSharp.Compiler.Diagnostics
open FSharp.Compiler.DiagnosticsLogger
open FSharp.Compiler.Features
open FSharp.Compiler.IlxGen
open FSharp.Compiler.InfoReader
open FSharp.Compiler.IO
open FSharp.Compiler.ParseAndCheckInputs
open FSharp.Compiler.OptimizeInputs
open FSharp.Compiler.ScriptClosure
open FSharp.Compiler.StaticLinking
open FSharp.Compiler.TcGlobals
open FSharp.Compiler.Text
open FSharp.Compiler.Text.Range
open FSharp.Compiler.TypedTree
open FSharp.Compiler.TypedTreeOps
open FSharp.Compiler.XmlDocFileWriter
open FSharp.Compiler.CheckExpressionsOps

//----------------------------------------------------------------------------
// Reporting - warnings, errors
//----------------------------------------------------------------------------

/// An error logger that reports errors up to some maximum, notifying the exiter when that maximum is reached
[<AbstractClass>]
type DiagnosticsLoggerUpToMaxErrors(tcConfigB: TcConfigBuilder, exiter: Exiter, nameForDebugging) =
    inherit DiagnosticsLogger(nameForDebugging)

    let mutable errors = 0

    /// Called when an error or warning occurs
    abstract HandleIssue: tcConfig: TcConfig * diagnostic: PhasedDiagnostic * severity: FSharpDiagnosticSeverity -> unit

    /// Called when 'too many errors' has occurred
    abstract HandleTooManyErrors: text: string -> unit

    override _.ErrorCount = errors

    override x.DiagnosticSink(diagnostic, severity) =
        let tcConfig = TcConfig.Create(tcConfigB, validate = false)

        match diagnostic.AdjustSeverity(tcConfig.diagnosticsOptions, severity) with
        | FSharpDiagnosticSeverity.Error ->
            if errors >= tcConfig.maxErrors then
                x.HandleTooManyErrors(FSComp.SR.fscTooManyErrors ())
                exiter.Exit 1

            x.HandleIssue(tcConfig, diagnostic, FSharpDiagnosticSeverity.Error)

            errors <- errors + 1

            match diagnostic.Exception, tcConfigB.simulateException with
            | InternalError(msg, _), None
            | Failure msg, None -> Debug.Assert(false, sprintf "Bug in compiler: %s\n%s" msg (diagnostic.Exception.ToString()))
            | :? KeyNotFoundException, None ->
                Debug.Assert(false, sprintf "Lookup exception in compiler: %s" (diagnostic.Exception.ToString()))
            | _ -> ()

        | FSharpDiagnosticSeverity.Hidden -> ()
        | s -> x.HandleIssue(tcConfig, diagnostic, s)

/// Create an error logger that counts and prints errors
let ConsoleDiagnosticsLogger (tcConfigB: TcConfigBuilder, exiter: Exiter) =
    { new DiagnosticsLoggerUpToMaxErrors(tcConfigB, exiter, "ConsoleDiagnosticsLogger") with

        member _.HandleTooManyErrors(text: string) =
            DoWithDiagnosticColor FSharpDiagnosticSeverity.Warning (fun () -> Printf.eprintfn "%s" text)

        member _.HandleIssue(tcConfig, diagnostic, severity) =
            DoWithDiagnosticColor severity (fun () ->
                writeViaBuffer stderr (fun buf -> diagnostic.Output(buf, tcConfig, severity))
                stderr.WriteLine())
    }
    :> DiagnosticsLogger

/// DiagnosticLoggers can be sensitive to the TcConfig flags. During the checking
/// of the flags themselves we have to create temporary loggers, until the full configuration is
/// available.
type IDiagnosticsLoggerProvider =

    abstract CreateLogger: tcConfigB: TcConfigBuilder * exiter: Exiter -> DiagnosticsLogger

type CapturingDiagnosticsLogger with

    /// Commit the delayed diagnostics via a fresh temporary logger of the right kind.
    member x.CommitDelayedDiagnostics(diagnosticsLoggerProvider: IDiagnosticsLoggerProvider, tcConfigB, exiter) =
        let diagnosticsLogger = diagnosticsLoggerProvider.CreateLogger(tcConfigB, exiter)
        x.CommitDelayedDiagnostics diagnosticsLogger

/// The default DiagnosticsLogger implementation, reporting messages to the Console up to the maxerrors maximum
type ConsoleLoggerProvider() =

    interface IDiagnosticsLoggerProvider with

        member _.CreateLogger(tcConfigB, exiter) =
            ConsoleDiagnosticsLogger(tcConfigB, exiter)

/// Notify the exiter if any error has occurred
let AbortOnError (diagnosticsLogger: DiagnosticsLogger, exiter: Exiter) =
    if diagnosticsLogger.ErrorCount > 0 then
        exiter.Exit 1

let TypeCheck
    (ctok, tcConfig, tcImports, tcGlobals, diagnosticsLogger: DiagnosticsLogger, assemblyName, tcEnv0, openDecls0, inputs, exiter: Exiter)
    =
    try
        if isNil inputs then
            error (Error(FSComp.SR.fscNoImplementationFiles (), rangeStartup))

        let ccuName = assemblyName

        let tcInitialState =
            GetInitialTcState(rangeStartup, ccuName, tcConfig, tcGlobals, tcImports, tcEnv0, openDecls0)

        let eagerFormat (diag: PhasedDiagnostic) = diag.EagerlyFormatCore true

        CheckClosedInputSet(
            ctok,
            (fun () -> diagnosticsLogger.CheckForRealErrorsIgnoringWarnings),
            tcConfig,
            tcImports,
            tcGlobals,
            None,
            tcInitialState,
            eagerFormat,
            inputs
        )
    with exn ->
        errorRecovery exn rangeStartup
        exiter.Exit 1

/// Check for .fsx and, if present, compute the load closure for of #loaded files.
///
/// This is the "script compilation" feature that has always been present in the F# compiler, that allows you to compile scripts
/// and get the load closure and references from them. This applies even if the script is in a project (with 'Compile' action), for example.
///
/// Any DLL references implied by package references are also retrieved from the script.
///
/// When script compilation is invoked, the outputs are not necessarily a functioning application - the referenced DLLs are not
/// copied to the output folder, for example (except perhaps FSharp.Core.dll).
///
/// NOTE: there is similar code in IncrementalBuilder.fs and this code should really be reconciled with that
let AdjustForScriptCompile (tcConfigB: TcConfigBuilder, commandLineSourceFiles, lexResourceManager, dependencyProvider) =

    let combineFilePath file =
        try
            if FileSystem.IsPathRootedShim file then
                file
            else
                Path.Combine(tcConfigB.implicitIncludeDir, file)
        with _ ->
            error (Error(FSComp.SR.pathIsInvalid file, rangeStartup))

    let commandLineSourceFiles = commandLineSourceFiles |> List.map combineFilePath
    tcConfigB.embedSourceList <- tcConfigB.embedSourceList |> List.map combineFilePath

    // Script compilation is active if the last item being compiled is a script and --noframework has not been specified
    let mutable allSources = []

    let tcConfig = TcConfig.Create(tcConfigB, validate = false)

    let AddIfNotPresent (fileName: string) =
        if not (allSources |> List.contains fileName) then
            allSources <- fileName :: allSources

    let AppendClosureInformation fileName =
        if IsScript fileName then
            let closure =
                LoadClosure.ComputeClosureOfScriptFiles(
                    tcConfig,
                    [ fileName, rangeStartup ],
                    CodeContext.Compilation,
                    lexResourceManager,
                    dependencyProvider
                )

            // Record the new references (non-framework) references from the analysis of the script. (The full resolutions are recorded
            // as the corresponding #I paths used to resolve them are local to the scripts and not added to the tcConfigB - they are
            // added to localized clones of the tcConfigB).
            let references =
                closure.References
                |> List.collect snd
                |> List.filter (fun r ->
                    not (equals r.originalReference.Range range0)
                    && not (equals r.originalReference.Range rangeStartup))

            references
            |> List.iter (fun r -> tcConfigB.AddReferencedAssemblyByPath(r.originalReference.Range, r.resolvedPath))

            closure.SourceFiles |> List.map fst |> List.iter AddIfNotPresent
            closure.AllRootFileDiagnostics |> List.iter diagnosticSink

            // If there is a target framework for the script then push that as a requirement into the overall compilation and add all the framework references implied
            // by the script too.
            let primaryAssembly =
                if closure.UseDesktopFramework then
                    PrimaryAssembly.Mscorlib
                else
                    PrimaryAssembly.System_Runtime

            tcConfigB.SetPrimaryAssembly primaryAssembly

            if tcConfigB.implicitlyReferenceDotNetAssemblies then
                let references = closure.References |> List.collect snd

                for reference in references do
                    tcConfigB.AddReferencedAssemblyByPath(reference.originalReference.Range, reference.resolvedPath)

        else
            AddIfNotPresent fileName

    // Find closure of .fsx files.
    commandLineSourceFiles |> List.iter AppendClosureInformation

    List.rev allSources

let SetProcessThreadLocals tcConfigB =
    match tcConfigB.preferredUiLang with
    | Some s -> Thread.CurrentThread.CurrentUICulture <- CultureInfo(s)
    | None -> ()

let ProcessCommandLineFlags (tcConfigB: TcConfigBuilder, lcidFromCodePage, argv) =
    let mutable inputFilesRef = []

    let collect name =
        if List.exists (FileSystemUtils.checkSuffix name) [ ".resx" ] then
            error (Error(FSComp.SR.fscResxSourceFileDeprecated name, rangeStartup))
        else
            inputFilesRef <- name :: inputFilesRef

    let abbrevArgs = GetAbbrevFlagSet tcConfigB true

    // This is where flags are interpreted by the command line fsc.exe.
    ParseCompilerOptions(collect, GetCoreFscCompilerOptions tcConfigB, List.tail (PostProcessCompilerArgs abbrevArgs argv))

    let inputFiles = List.rev inputFilesRef

    // Check if we have a codepage from the console
    match tcConfigB.lcid with
    | Some _ -> ()
    | None -> tcConfigB.lcid <- lcidFromCodePage

    SetProcessThreadLocals tcConfigB

    (* step - get dll references *)
    let dllFiles, sourceFiles =
        inputFiles
        |> List.map FileSystemUtils.trimQuotes
        |> List.partition FileSystemUtils.isDll

    match dllFiles with
    | [] -> ()
    | h :: _ -> errorR (Error(FSComp.SR.fscReferenceOnCommandLine h, rangeStartup))

    dllFiles
    |> List.iter (fun f -> tcConfigB.AddReferencedAssemblyByPath(rangeStartup, f))

    sourceFiles

/// Write a .fsi file for the --sig option
module InterfaceFileWriter =
    let WriteInterfaceFile (tcGlobals, tcConfig: TcConfig, infoReader, declaredImpls: CheckedImplFile list) =
        // there are two modes here:
        // * write one unified sig file to a given path, or
        // * write individual sig files to paths matching their impl files
        let denv = DisplayEnv.InitialForSigFileGeneration tcGlobals

        let denv =
            { denv with
                shrinkOverloads = false
                printVerboseSignatures = true
            }

        let writeToFile os (CheckedImplFile(contents = mexpr)) =
            let text =
                NicePrint.layoutImpliedSignatureOfModuleOrNamespace true denv infoReader AccessibleFromSomewhere range0 mexpr
                |> Display.squashTo 80
                |> LayoutRender.showL

            Printf.fprintf os "%s\n\n" text

        let writeHeader filePath os =
            if
                filePath <> ""
                && not (List.exists (FileSystemUtils.checkSuffix filePath) FSharpIndentationAwareSyntaxFileSuffixes)
            then
                fprintfn os "#light"
                fprintfn os ""

        let writeAllToSameFile declaredImpls =
            /// Use a UTF-8 Encoding with no Byte Order Mark
            let os =
                if String.IsNullOrEmpty(tcConfig.printSignatureFile) then
                    Console.Out
                else
                    FileSystem.OpenFileForWriteShim(tcConfig.printSignatureFile, FileMode.Create).GetWriter()

            writeHeader tcConfig.printSignatureFile os

            for impl in declaredImpls do
                writeToFile os impl

            if tcConfig.printSignatureFile <> "" then
                os.Dispose()

        let extensionForFile (filePath: string) =
            if (List.exists (FileSystemUtils.checkSuffix filePath) FSharpMLCompatFileSuffixes) then
                ".mli"
            else
                ".fsi"

        let writeToSeparateFiles (declaredImpls: CheckedImplFile list) =
            for CheckedImplFile(qualifiedNameOfFile = name) as impl in declaredImpls do
                let fileName =
                    !!Path.ChangeExtension(name.Range.FileName, extensionForFile name.Range.FileName)

                printfn "writing impl file to %s" fileName
                use os = FileSystem.OpenFileForWriteShim(fileName, FileMode.Create).GetWriter()
                writeHeader fileName os
                writeToFile os impl

        if tcConfig.printSignature then
            writeAllToSameFile declaredImpls
        else if tcConfig.printAllSignatureFiles then
            writeToSeparateFiles declaredImpls

//----------------------------------------------------------------------------
// CopyFSharpCore
//----------------------------------------------------------------------------

// If the --nocopyfsharpcore switch is not specified, this will:
// 1) Look into the referenced assemblies, if FSharp.Core.dll is specified, it will copy it to output directory.
// 2) If not, but FSharp.Core.dll exists beside the compiler binaries, it will copy it to output directory.
// 3) If not, it will produce an error.
let CopyFSharpCore (outFile: string, referencedDlls: AssemblyReference list) =
    let outDir = !!Path.GetDirectoryName(outFile)
    let fsharpCoreAssemblyName = GetFSharpCoreLibraryName() + ".dll"
    let fsharpCoreDestinationPath = Path.Combine(outDir, fsharpCoreAssemblyName)

    let copyFileIfDifferent src dest =
        if
            not (FileSystem.FileExistsShim dest)
            || (FileSystem.GetCreationTimeShim src <> FileSystem.GetCreationTimeShim dest)
        then
            FileSystem.CopyShim(src, dest, true)

    let fsharpCoreReferences =
        referencedDlls
        |> Seq.tryFind (fun dll ->
            String.Equals(Path.GetFileName(dll.Text), fsharpCoreAssemblyName, StringComparison.CurrentCultureIgnoreCase))

    match fsharpCoreReferences with
    | Some referencedFsharpCoreDll -> copyFileIfDifferent referencedFsharpCoreDll.Text fsharpCoreDestinationPath
    | None ->
        let executionLocation = Assembly.GetExecutingAssembly().Location
        let compilerLocation = !!Path.GetDirectoryName(executionLocation)

        let compilerFsharpCoreDllPath =
            Path.Combine(compilerLocation, fsharpCoreAssemblyName)

        if FileSystem.FileExistsShim compilerFsharpCoreDllPath then
            copyFileIfDifferent compilerFsharpCoreDllPath fsharpCoreDestinationPath
        else
            errorR (Error(FSComp.SR.fsharpCoreNotFoundToBeCopied (), rangeCmdArgs))

// Try to find an AssemblyVersion attribute
let TryFindVersionAttribute g attrib attribName attribs deterministic =
    match AttributeHelpers.TryFindStringAttribute g attrib attribs with
    | Some versionString ->
        if deterministic && versionString.Contains("*") then
            errorR (Error(FSComp.SR.fscAssemblyWildcardAndDeterminism (attribName, versionString), rangeStartup))

        try
            Some(parseILVersion versionString)
        with e ->
            // Warning will be reported by CheckExpressions.fs
            None
    | _ -> None

//----------------------------------------------------------------------------
// Main phases of compilation. These are written as separate functions with explicit argument passing
// to ensure transient objects are eligible for GC and only actual required information
// is propagated.
//-----------------------------------------------------------------------------

[<NoEquality; NoComparison>]
type Args<'T> = Args of 'T

let getParallelReferenceResolutionFromEnvironment () =
    Environment.GetEnvironmentVariable("FCS_ParallelReferenceResolution")
    |> Option.ofObj
    |> Option.bind (fun flag ->
        match bool.TryParse flag with
        | true, runInParallel ->
            if runInParallel then
                Some ParallelReferenceResolution.On
            else
                Some ParallelReferenceResolution.Off
        | false, _ -> None)

/// First phase of compilation.
///   - Set up console encoding and code page settings
///   - Process command line, flags and collect filenames
///   - Resolve assemblies
///   - Import assemblies
///   - Parse source files
///   - Check the inputs
let main1
    (
        ctok,
        argv,
        legacyReferenceResolver,
        bannerAlreadyPrinted,
        reduceMemoryUsage: ReduceMemoryFlag,
        defaultCopyFSharpCore: CopyFSharpCoreFlag,
        exiter: Exiter,
        diagnosticsLoggerProvider: IDiagnosticsLoggerProvider,
        disposables: DisposablesTracker
    ) =

    // See Bug 735819
    let lcidFromCodePage =
        let thread = Thread.CurrentThread

        if
            (Console.OutputEncoding.CodePage <> 65001)
            && (Console.OutputEncoding.CodePage <> thread.CurrentUICulture.TextInfo.OEMCodePage)
            && (Console.OutputEncoding.CodePage <> thread.CurrentUICulture.TextInfo.ANSICodePage)
            && (CultureInfo.InvariantCulture <> thread.CurrentUICulture)
        then
            thread.CurrentUICulture <- CultureInfo("en-US")
            Some 1033
        else
            None

    let directoryBuildingFrom = Directory.GetCurrentDirectory()

    let tryGetMetadataSnapshot = (fun _ -> None)

    let defaultFSharpBinariesDir =
        FSharpEnvironment.BinFolderOfDefaultFSharpCompiler(None).Value

    let tcConfigB =
        TcConfigBuilder.CreateNew(
            legacyReferenceResolver,
            defaultFSharpBinariesDir,
            reduceMemoryUsage = reduceMemoryUsage,
            implicitIncludeDir = directoryBuildingFrom,
            isInteractive = false,
            isInvalidationSupported = false,
            defaultCopyFSharpCore = defaultCopyFSharpCore,
            tryGetMetadataSnapshot = tryGetMetadataSnapshot,
            sdkDirOverride = None,
            rangeForErrors = range0,
            compilationMode = CompilationMode.OneOff
        )

    tcConfigB.exiter <- exiter

    // Preset: --optimize+ -g --tailcalls+ (see 4505)
    SetOptimizeSwitch tcConfigB OptionSwitch.On
    SetDebugSwitch tcConfigB None OptionSwitch.Off
    SetTailcallSwitch tcConfigB OptionSwitch.On

    // Now install a delayed logger to hold all errors from flags until after all flags have been parsed (for example, --vserrors)
    let delayForFlagsLogger = CapturingDiagnosticsLogger("DelayFlagsLogger")

    SetThreadDiagnosticsLoggerNoUnwind delayForFlagsLogger

    // Share intern'd strings across all lexing/parsing
    let lexResourceManager = Lexhelp.LexResourceManager()

    let dependencyProvider = new DependencyProvider()

    // Process command line, flags and collect filenames
    let sourceFiles =
        // The ParseCompilerOptions function calls imperative function to process "real" args
        // Rather than start processing, just collect names, then process them.
        try
            let files = ProcessCommandLineFlags(tcConfigB, lcidFromCodePage, argv)
            let files = CheckAndReportSourceFileDuplicates(ResizeArray.ofList files)
            AdjustForScriptCompile(tcConfigB, files, lexResourceManager, dependencyProvider)
        with e ->
            errorRecovery e rangeStartup
            delayForFlagsLogger.CommitDelayedDiagnostics(diagnosticsLoggerProvider, tcConfigB, exiter)
            exiter.Exit 1

    tcConfigB.conditionalDefines <- "COMPILED" :: tcConfigB.conditionalDefines

    // Override ParallelReferenceResolution set on the CLI with an environment setting if present.
    match getParallelReferenceResolutionFromEnvironment () with
    | Some parallelReferenceResolution -> tcConfigB.parallelReferenceResolution <- parallelReferenceResolution
    | None -> ()

    if tcConfigB.utf8output && Console.OutputEncoding <> Encoding.UTF8 then
        let previousEncoding = Console.OutputEncoding
        Console.OutputEncoding <- Encoding.UTF8

        disposables.Register(
            { new IDisposable with
                member _.Dispose() =
                    Console.OutputEncoding <- previousEncoding
            }
        )

    // Display the banner text, if necessary
    if not bannerAlreadyPrinted then
        Console.Write(GetBannerText tcConfigB)

    // Create tcGlobals and frameworkTcImports
    let outfile, pdbfile, assemblyName =
        try
            tcConfigB.DecideNames sourceFiles
        with e ->
            errorRecovery e rangeStartup
            delayForFlagsLogger.CommitDelayedDiagnostics(diagnosticsLoggerProvider, tcConfigB, exiter)
            exiter.Exit 1

    // DecideNames may give "no inputs" error. Abort on error at this point. bug://3911
    if not tcConfigB.continueAfterParseFailure && delayForFlagsLogger.ErrorCount > 0 then
        delayForFlagsLogger.CommitDelayedDiagnostics(diagnosticsLoggerProvider, tcConfigB, exiter)
        exiter.Exit 1

    // If there's a problem building TcConfig, abort
    let tcConfig =
        try
            TcConfig.Create(tcConfigB, validate = false)
        with e ->
            errorRecovery e rangeStartup
            delayForFlagsLogger.CommitDelayedDiagnostics(diagnosticsLoggerProvider, tcConfigB, exiter)
            exiter.Exit 1

    if tcConfig.showTimes then
        Activity.Profiling.addConsoleListener () |> disposables.Register

    tcConfig.writeTimesToFile
    |> Option.iter (fun f ->
        Activity.CsvExport.addCsvFileListener f |> disposables.Register

        Activity.start
            "FSC compilation"
            [
                Activity.Tags.project, tcConfig.outputFile |> Option.defaultValue String.Empty
            ]
        |> disposables.Register)

    let diagnosticsLogger = diagnosticsLoggerProvider.CreateLogger(tcConfigB, exiter)

    // Install the global error logger and never remove it. This logger does have all command-line flags considered.
    SetThreadDiagnosticsLoggerNoUnwind diagnosticsLogger

    // Forward all errors from flags
    delayForFlagsLogger.CommitDelayedDiagnostics diagnosticsLogger

    if not tcConfigB.continueAfterParseFailure then
        AbortOnError(diagnosticsLogger, exiter)

    // Resolve assemblies
    ReportTime tcConfig "Import mscorlib+FSharp.Core"
    let foundationalTcConfigP = TcConfigProvider.Constant tcConfig

    let sysRes, otherRes, knownUnresolved =
        TcAssemblyResolutions.SplitNonFoundationalResolutions(tcConfig)

    // Import basic assemblies
    let tcGlobals, frameworkTcImports =
        TcImports.BuildFrameworkTcImports(foundationalTcConfigP, sysRes, otherRes)
        |> Async.RunImmediate

    let ilSourceDocs =
        [
            for sourceFile in sourceFiles -> tcGlobals.memoize_file (FileIndex.fileIndexOfFile sourceFile)
        ]

    // Register framework tcImports to be disposed in future
    disposables.Register frameworkTcImports

    // Parse sourceFiles
    ReportTime tcConfig "Parse inputs"
    use unwindParsePhase = UseBuildPhase BuildPhase.Parse

    let inputs =
        ParseInputFiles(tcConfig, lexResourceManager, sourceFiles, diagnosticsLogger, false)

    let inputs, _ =
        (Map.empty, inputs)
        ||> List.mapFold (fun state (input, x) ->
            let inputT, stateT = DeduplicateParsedInputModuleName state input
            (inputT, x), stateT)

    // Print the AST if requested
    if tcConfig.printAst then
        for input, _filename in inputs do
            printf "AST:\n"
            printfn "%+A" input
            printf "\n"

    if tcConfig.parseOnly then
        exiter.Exit 0

    if not tcConfig.continueAfterParseFailure then
        AbortOnError(diagnosticsLogger, exiter)

    let tcConfig =
        (tcConfig, inputs)
        ||> List.fold (fun z (input, sourceFileDirectory) ->
            ApplyMetaCommandsFromInputToTcConfig(z, input, sourceFileDirectory, dependencyProvider))

    let tcConfigP = TcConfigProvider.Constant tcConfig

    // Import other assemblies
    ReportTime tcConfig "Import non-system references"

    let tcImports =
        TcImports.BuildNonFrameworkTcImports(tcConfigP, frameworkTcImports, otherRes, knownUnresolved, dependencyProvider)
        |> Async.RunImmediate

    // register tcImports to be disposed in future
    disposables.Register tcImports

    if not tcConfig.continueAfterParseFailure then
        AbortOnError(diagnosticsLogger, exiter)

    if tcConfig.importAllReferencesOnly then
        exiter.Exit 0

    // Build the initial type checking environment
    ReportTime tcConfig "Typecheck"

    use unwindParsePhase = UseBuildPhase BuildPhase.TypeCheck

    let tcEnv0, openDecls0 =
        GetInitialTcEnv(assemblyName, rangeStartup, tcConfig, tcImports, tcGlobals)

    // Type check the inputs
    let inputs = inputs |> List.map fst

    let tcState, topAttrs, typedAssembly, _tcEnvAtEnd =
        TypeCheck(ctok, tcConfig, tcImports, tcGlobals, diagnosticsLogger, assemblyName, tcEnv0, openDecls0, inputs, exiter)

    AbortOnError(diagnosticsLogger, exiter)
    ReportTime tcConfig "Typechecked"

    Args(
        ctok,
        tcGlobals,
        tcImports,
        frameworkTcImports,
        tcState.Ccu,
        typedAssembly,
        topAttrs,
        tcConfig,
        outfile,
        pdbfile,
        assemblyName,
        diagnosticsLogger,
        exiter,
        ilSourceDocs
    )

/// Second phase of compilation.
///   - Write the signature file, check some attributes
let main2
    (Args(ctok,
          tcGlobals,
          tcImports: TcImports,
          frameworkTcImports,
          generatedCcu: CcuThunk,
          typedImplFiles,
          topAttrs,
          tcConfig: TcConfig,
          outfile,
          pdbfile,
          assemblyName,
          diagnosticsLogger,
          exiter: Exiter,
          ilSourceDocs))
    =
    if tcConfig.typeCheckOnly then
        exiter.Exit 0

    generatedCcu.Contents.SetAttribs(generatedCcu.Contents.Attribs @ topAttrs.assemblyAttrs)

    use unwindPhase = UseBuildPhase BuildPhase.CodeGen
    let signingInfo = ValidateKeySigningAttributes(tcConfig, tcGlobals, topAttrs)

    AbortOnError(diagnosticsLogger, exiter)

    // Build an updated diagnosticsLogger that filters according to the scopedPragmas. Then install
    // it as the updated global error logger and never remove it
    let oldLogger = diagnosticsLogger

    let diagnosticsLogger =
        GetDiagnosticsLoggerFilteringByScopedNowarn(tcConfig.diagnosticsOptions, oldLogger)

    SetThreadDiagnosticsLoggerNoUnwind diagnosticsLogger

    // Try to find an AssemblyVersion attribute
    let assemVerFromAttrib =
        match
            TryFindVersionAttribute
                tcGlobals
                "System.Reflection.AssemblyVersionAttribute"
                "AssemblyVersionAttribute"
                topAttrs.assemblyAttrs
                tcConfig.deterministic
        with
        | Some v ->
            match tcConfig.version with
            | VersionNone -> Some v
            | _ ->
                warning (Error(FSComp.SR.fscAssemblyVersionAttributeIgnored (), rangeStartup))
                None
        | _ ->
            match tcConfig.version with
            | VersionNone -> Some(ILVersionInfo(0us, 0us, 0us, 0us)) //If no attribute was specified in source then version is 0.0.0.0
            | _ -> Some(tcConfig.version.GetVersionInfo tcConfig.implicitIncludeDir)

    // write interface, xmldoc
    ReportTime tcConfig "Write Interface File"
    use _ = UseBuildPhase BuildPhase.Output

    if tcConfig.printSignature || tcConfig.printAllSignatureFiles then
        InterfaceFileWriter.WriteInterfaceFile(tcGlobals, tcConfig, InfoReader(tcGlobals, tcImports.GetImportMap()), typedImplFiles)

    ReportTime tcConfig "Write XML doc signatures"

    if tcConfig.xmlDocOutputFile.IsSome then
        XmlDocWriter.ComputeXmlDocSigs(tcGlobals, generatedCcu)

    ReportTime tcConfig "Write XML docs"

    tcConfig.xmlDocOutputFile
    |> Option.iter (fun xmlFile ->
        let xmlFile = tcConfig.MakePathAbsolute xmlFile
        XmlDocWriter.WriteXmlDocFile(tcGlobals, assemblyName, generatedCcu, xmlFile))

    // Pass on only the minimum information required for the next phase
    Args(
        ctok,
        tcConfig,
        tcImports,
        frameworkTcImports,
        tcGlobals,
        diagnosticsLogger,
        generatedCcu,
        outfile,
        typedImplFiles,
        topAttrs,
        pdbfile,
        assemblyName,
        assemVerFromAttrib,
        signingInfo,
        exiter,
        ilSourceDocs
    )

/// Third phase of compilation.
///   - encode signature data
///   - optimize
///   - encode optimization data
let main3
    (Args(ctok,
          tcConfig,
          tcImports,
          frameworkTcImports: TcImports,
          tcGlobals,
          diagnosticsLogger: DiagnosticsLogger,
          generatedCcu: CcuThunk,
          outfile,
          typedImplFiles,
          topAttrs,
          pdbfile,
          assemblyName,
          assemVerFromAttrib,
          signingInfo,
          exiter: Exiter,
          ilSourceDocs))
    =
    // Encode the signature data
    ReportTime tcConfig "Encode Interface Data"
    let exportRemapping = MakeExportRemapping generatedCcu generatedCcu.Contents

    let sigDataAttributes, sigDataResources =
        try
            EncodeSignatureData(tcConfig, tcGlobals, exportRemapping, generatedCcu, outfile, false)
        with e ->
            errorRecoveryNoRange e
            exiter.Exit 1

    let metadataVersion =
        match tcConfig.metadataVersion with
        | Some v -> v
        | _ ->
            match frameworkTcImports.DllTable.TryFind tcConfig.primaryAssembly.Name with
            | Some ib -> ib.RawMetadata.TryGetILModuleDef().Value.MetadataVersion
            | _ -> ""

    let optimizedImpls, optDataResources =
        // Perform optimization
        use _ = UseBuildPhase BuildPhase.Optimize

        let optEnv0 = GetInitialOptimizationEnv(tcImports, tcGlobals)

        let importMap = tcImports.GetImportMap()

        let optimizedImpls, optimizationData, _ =
            ApplyAllOptimizations(
                tcConfig,
                tcGlobals,
                (LightweightTcValForUsingInBuildMethodCall tcGlobals),
                outfile,
                importMap,
                false,
                optEnv0,
                generatedCcu,
                typedImplFiles
            )

        AbortOnError(diagnosticsLogger, exiter)

        // Encode the optimization data
        ReportTime tcConfig ("Encoding OptData")

        optimizedImpls, EncodeOptimizationData(tcGlobals, tcConfig, outfile, exportRemapping, (generatedCcu, optimizationData), false)

    if tcGlobals.langVersion.SupportsFeature LanguageFeature.WarningWhenTailRecAttributeButNonTailRecUsage then
        match optimizedImpls with
        | CheckedAssemblyAfterOptimization checkedImplFileAfterOptimizations ->
            ReportTime tcConfig ("TailCall Checks")

            for f in checkedImplFileAfterOptimizations do
                TailCallChecks.CheckImplFile(tcGlobals, tcImports.GetImportMap(), true, f.ImplFile.Contents)

    let refAssemblySignatureHash =
        match tcConfig.emitMetadataAssembly with
        | MetadataAssemblyGeneration.None -> None
        | MetadataAssemblyGeneration.ReferenceOnly
        | MetadataAssemblyGeneration.ReferenceOut _ ->
            let hasIvt =
                TryFindFSharpStringAttribute tcGlobals tcGlobals.attrib_InternalsVisibleToAttribute topAttrs.assemblyAttrs
                |> Option.isSome

            let observer = if hasIvt then PublicAndInternal else PublicOnly

            let optDataHash =
                optDataResources
                |> List.map (fun ilResource ->
                    use s = ilResource.GetBytes().AsStream()
                    let sha256 = System.Security.Cryptography.SHA256.Create()
                    sha256.ComputeHash s)
                |> List.sumBy (hash >> int64)
                |> hash

            try
                Fsharp.Compiler.SignatureHash.calculateSignatureHashOfFiles typedImplFiles tcGlobals observer
                + Fsharp.Compiler.SignatureHash.calculateHashOfAssemblyTopAttributes topAttrs tcConfig.platform
                + optDataHash
                |> Some
            with e ->
                printfn "Unexpected error when hashing implied signature, will hash the all of .NET metadata instead. Error: %O " e
                None

    // Pass on only the minimum information required for the next phase
    Args(
        ctok,
        tcConfig,
        tcImports,
        tcGlobals,
        diagnosticsLogger,
        generatedCcu,
        outfile,
        optimizedImpls,
        topAttrs,
        pdbfile,
        assemblyName,
        sigDataAttributes,
        sigDataResources,
        optDataResources,
        assemVerFromAttrib,
        signingInfo,
        metadataVersion,
        exiter,
        ilSourceDocs,
        refAssemblySignatureHash
    )

/// Fourth phase of compilation.
///   -  Static linking
///   -  IL code generation
let main4
    (tcImportsCapture, dynamicAssemblyCreator)
    (Args(ctok,
          tcConfig: TcConfig,
          tcImports,
          tcGlobals: TcGlobals,
          diagnosticsLogger,
          generatedCcu: CcuThunk,
          outfile,
          optimizedImpls,
          topAttrs,
          pdbfile,
          assemblyName,
          sigDataAttributes,
          sigDataResources,
          optDataResources,
          assemVerFromAttrib,
          signingInfo,
          metadataVersion,
          exiter: Exiter,
          ilSourceDocs,
          refAssemblySignatureHash))
    =
    match tcImportsCapture with
    | None -> ()
    | Some f -> f tcImports

    if tcConfig.standalone && generatedCcu.UsesFSharp20PlusQuotations then
        error (Error(FSComp.SR.fscQuotationLiteralsStaticLinking0 (), rangeStartup))

    // Compute a static linker, it gets called later.
    let staticLinker = StaticLink(ctok, tcConfig, tcImports, tcGlobals)

    ReportTime tcConfig "TAST -> IL"
    use _ = UseBuildPhase BuildPhase.IlxGen

    // Create the Abstract IL generator
    let ilxGenerator =
        CreateIlxAssemblyGenerator(tcConfig, tcImports, tcGlobals, (LightweightTcValForUsingInBuildMethodCall tcGlobals), generatedCcu)

    let codegenBackend =
        (if Option.isSome dynamicAssemblyCreator then
             IlReflectBackend
         else
             IlWriteBackend)

    // Generate the Abstract IL Code
    let codegenResults =
        GenerateIlxCode(
            codegenBackend,
            Option.isSome dynamicAssemblyCreator,
            tcConfig,
            topAttrs,
            optimizedImpls,
            generatedCcu.AssemblyName,
            ilxGenerator
        )

    // Build the Abstract IL view of the final main module, prior to static linking

    let topAssemblyAttrs = codegenResults.topAssemblyAttrs

    let topAttrs =
        { topAttrs with
            assemblyAttrs = topAssemblyAttrs
        }

    let permissionSets = codegenResults.permissionSets
    let secDecls = mkILSecurityDecls permissionSets

    let ilxMainModule =
        MainModuleBuilder.CreateMainModule(
            ctok,
            tcConfig,
            tcGlobals,
            tcImports,
            pdbfile,
            assemblyName,
            outfile,
            topAttrs,
            sigDataAttributes,
            sigDataResources,
            optDataResources,
            codegenResults,
            assemVerFromAttrib,
            metadataVersion,
            secDecls
        )

    AbortOnError(diagnosticsLogger, exiter)

    // Pass on only the minimum information required for the next phase
    Args(
        ctok,
        tcConfig,
        tcImports,
        tcGlobals,
        diagnosticsLogger,
        staticLinker,
        outfile,
        pdbfile,
        ilxMainModule,
        signingInfo,
        exiter,
        ilSourceDocs,
        refAssemblySignatureHash
    )

/// Fifth phase of compilation.
///   -  static linking
let main5
    (Args(ctok,
          tcConfig,
          tcImports,
          tcGlobals,
          diagnosticsLogger: DiagnosticsLogger,
          staticLinker,
          outfile,
          pdbfile,
          ilxMainModule,
          signingInfo,
          exiter: Exiter,
          ilSourceDocs,
          refAssemblySignatureHash))
    =

    use _ = UseBuildPhase BuildPhase.Output

    // Static linking, if any
    let ilxMainModule =
        try
            staticLinker ilxMainModule
        with e ->
            errorRecoveryNoRange e
            exiter.Exit 1

    AbortOnError(diagnosticsLogger, exiter)

    // Pass on only the minimum information required for the next phase
    Args(
        ctok,
        tcConfig,
        tcImports,
        tcGlobals,
        diagnosticsLogger,
        ilxMainModule,
        outfile,
        pdbfile,
        signingInfo,
        exiter,
        ilSourceDocs,
        refAssemblySignatureHash
    )

/// Sixth phase of compilation.
///   -  write the binaries
let main6
    dynamicAssemblyCreator
    (Args(ctok,
          tcConfig,
          tcImports: TcImports,
          tcGlobals: TcGlobals,
          diagnosticsLogger: DiagnosticsLogger,
          ilxMainModule,
          outfile,
          pdbfile,
          signingInfo,
          exiter: Exiter,
          ilSourceDocs,
          refAssemblySignatureHash))
    =
    ReportTime tcConfig "Write .NET Binary"

    use _ = UseBuildPhase BuildPhase.Output
    let outfile = tcConfig.MakePathAbsolute outfile

    DoesNotRequireCompilerThreadTokenAndCouldPossiblyBeMadeConcurrent ctok

    let pdbfile =
        pdbfile
        |> Option.map (fun s ->
            let absolutePath = tcConfig.MakePathAbsolute s
            FileSystem.GetFullPathShim(absolutePath))

    let normalizeAssemblyRefs (aref: ILAssemblyRef) =
        match tcImports.TryFindDllInfo(ctok, rangeStartup, aref.Name, lookupOnly = false) with
        | Some dllInfo ->
            match dllInfo.ILScopeRef with
            | ILScopeRef.Assembly ref -> ref
            | _ -> aref
        | None -> aref

    match dynamicAssemblyCreator with
    | None ->
        try
            match tcConfig.emitMetadataAssembly with
            | MetadataAssemblyGeneration.None -> ()
            | _ ->
                let outfile =
                    match tcConfig.emitMetadataAssembly with
                    | MetadataAssemblyGeneration.ReferenceOut outputPath -> outputPath
                    | _ -> outfile

                let referenceAssemblyAttribOpt =
                    tcGlobals.iltyp_ReferenceAssemblyAttributeOpt
                    |> Option.map (fun ilTy -> mkILCustomAttribute (ilTy.TypeRef, [], [], []))

                try
                    // We want to write no PDB info.
                    ILBinaryWriter.WriteILBinaryFile(
                        {
                            ilg = tcGlobals.ilg
                            outfile = outfile
                            pdbfile = None
                            emitTailcalls = tcConfig.emitTailcalls
                            deterministic = tcConfig.deterministic
                            portablePDB = false
                            embeddedPDB = false
                            embedAllSource = tcConfig.embedAllSource
                            embedSourceList = tcConfig.embedSourceList
                            allGivenSources = ilSourceDocs
                            sourceLink = tcConfig.sourceLink
                            checksumAlgorithm = tcConfig.checksumAlgorithm
                            signer = GetStrongNameSigner signingInfo
                            dumpDebugInfo = tcConfig.dumpDebugInfo
                            referenceAssemblyOnly = true
                            referenceAssemblyAttribOpt = referenceAssemblyAttribOpt
                            referenceAssemblySignatureHash = refAssemblySignatureHash
                            pathMap = tcConfig.pathMap
                        },
                        ilxMainModule,
                        normalizeAssemblyRefs
                    )
                with Failure msg ->
                    error (Error(FSComp.SR.fscProblemWritingBinary (outfile, msg), rangeCmdArgs))

            match tcConfig.emitMetadataAssembly with
            | MetadataAssemblyGeneration.ReferenceOnly -> ()
            | _ ->
                try
                    ILBinaryWriter.WriteILBinaryFile(
                        {
                            ilg = tcGlobals.ilg
                            outfile = outfile
                            pdbfile = pdbfile
                            emitTailcalls = tcConfig.emitTailcalls
                            deterministic = tcConfig.deterministic
                            portablePDB = tcConfig.portablePDB
                            embeddedPDB = tcConfig.embeddedPDB
                            embedAllSource = tcConfig.embedAllSource
                            embedSourceList = tcConfig.embedSourceList
                            allGivenSources = ilSourceDocs
                            sourceLink = tcConfig.sourceLink
                            checksumAlgorithm = tcConfig.checksumAlgorithm
                            signer = GetStrongNameSigner signingInfo
                            dumpDebugInfo = tcConfig.dumpDebugInfo
                            referenceAssemblyOnly = false
                            referenceAssemblyAttribOpt = None
                            referenceAssemblySignatureHash = None
                            pathMap = tcConfig.pathMap
                        },
                        ilxMainModule,
                        normalizeAssemblyRefs
                    )
                with Failure msg ->
                    error (Error(FSComp.SR.fscProblemWritingBinary (outfile, msg), rangeCmdArgs))
        with e ->
            errorRecoveryNoRange e
            exiter.Exit 1
    | Some da -> da (tcConfig, tcGlobals, outfile, ilxMainModule)

    AbortOnError(diagnosticsLogger, exiter)

    // Don't copy referenced FSharp.core.dll if we are building FSharp.Core.dll
    if
        (tcConfig.copyFSharpCore = CopyFSharpCoreFlag.Yes)
        && not tcConfig.compilingFSharpCore
        && not tcConfig.standalone
    then
        CopyFSharpCore(outfile, tcConfig.referencedDLLs)

    ReportTime tcConfig "Exiting"

/// The main (non-incremental) compilation entry point used by fsc.exe
let CompileFromCommandLineArguments
    (
        ctok,
        argv,
        legacyReferenceResolver,
        bannerAlreadyPrinted,
        reduceMemoryUsage,
        defaultCopyFSharpCore,
        exiter: Exiter,
        loggerProvider,
        tcImportsCapture,
        dynamicAssemblyCreator
    ) =

    use disposables = new DisposablesTracker()

    main1 (
        ctok,
        argv,
        legacyReferenceResolver,
        bannerAlreadyPrinted,
        reduceMemoryUsage,
        defaultCopyFSharpCore,
        exiter,
        loggerProvider,
        disposables
    )
    |> main2
    |> main3
    |> main4 (tcImportsCapture, dynamicAssemblyCreator)
    |> main5
    |> main6 dynamicAssemblyCreator
