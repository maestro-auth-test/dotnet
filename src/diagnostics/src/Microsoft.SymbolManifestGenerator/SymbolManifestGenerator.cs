// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SymbolStore;
using Microsoft.SymbolStore.KeyGenerators;

namespace Microsoft.SymbolManifestGenerator;

public static class SymbolManifestGenerator
{
    private const int TargetDebugLevel = 0x154 | 0x1 | 0x500; // Private | Binary | SourceIndexed

    private static readonly JsonSerializerOptions s_serializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static bool GenerateManifest(ITracer tracer, DirectoryInfo dir, string manifestFileName, bool specialFilesRequireAdjacentRuntime = true)
    {
        ManifestDataV1 manifestData = new();

        FileInfo[] allFiles = dir.GetFiles("*", SearchOption.AllDirectories);
        foreach (FileInfo file in allFiles)
        {
            using FileStream fileStream = file.OpenRead();
            SymbolStoreFile symbolStoreFile = new(fileStream, file.FullName);
            FileKeyGenerator generator = new(tracer, symbolStoreFile);
            if (!generator.IsValid())
            {
                tracer.Information($"Could not generate a valid FileKeyGenerator from file '{file.FullName}'. Skipping.");
                continue;
            }

            // For any given runtime that's found - enumerate all possible special keys.
            foreach (SymbolStoreKey runtimeCorrelatedKey in generator.GetKeys(KeyTypeFlags.ClrKeys))
            {
                (bool foundMultipleCandidates, FileInfo correlatedFile) = FindCorrelatedFileForRuntimeModule(tracer, allFiles, file, runtimeCorrelatedKey, specialFilesRequireAdjacentRuntime);

                if (foundMultipleCandidates)
                {
                    tracer.Error("Multiple files with same name to be indexed under same runtime - aborting manifest creation.");
                    return false;
                }

                if (correlatedFile is null)
                {
                    tracer.Information($"Unique special file '{runtimeCorrelatedKey.FullPathName}' for runtime module '{file.FullName}' could not be found under '{file.DirectoryName}'. Skipping.");
                    continue;
                }

                string basedirRelativePath = correlatedFile.FullName.Replace(dir.FullName, string.Empty).TrimStart(Path.DirectorySeparatorChar);
                string fileHash = CalculateSHA512(correlatedFile);

                if (manifestData.Entries.Any(x => x.BasedirRelativePath == basedirRelativePath))
                {
                    tracer.Error($"Special module '{correlatedFile.FullName}' cannot be associated with a single runtime unambiguously. Multiple runtimes found in lookup scope. Aborting manifest creation.");
                    return false;
                }

                // This is the special module for the runtime in runtimeCorrelatedKey that's under the special index.
                manifestData.Entries.Add(new()
                {
                    BasedirRelativePath = basedirRelativePath,
                    SymbolKey = runtimeCorrelatedKey.Index,
                    Sha512 = fileHash,
                    DebugInformationLevel = TargetDebugLevel
                });

                using FileStream fs = correlatedFile.OpenRead();
                SymbolStoreFile specialDiagnosticFile = new(fs, correlatedFile.FullName);
                FileKeyGenerator correlatedGenerator = new(tracer, specialDiagnosticFile);

                if (!correlatedGenerator.IsValid())
                {
                    tracer.Error("Unable to get a key generator for special diagnostic file '{0}' of runtime '{1}'", file.FullName, correlatedFile.FullName);
                    return false;
                }

                SymbolStoreKey diagnosticFileIdentityKey = correlatedGenerator.GetKeys(KeyTypeFlags.IdentityKey).Single();
                // This is the special module for the runtime in runtimeCorrelatedKey that's under its own identity key
                // with the upgraded debug information level.
                manifestData.Entries.Add(new()
                {
                    BasedirRelativePath = basedirRelativePath,
                    SymbolKey = diagnosticFileIdentityKey.Index,
                    Sha512 = fileHash,
                    DebugInformationLevel = TargetDebugLevel
                });
            }
        }

        string manifestDataContent = JsonSerializer.Serialize(manifestData, s_serializeOptions);
        File.WriteAllText(manifestFileName, manifestDataContent);

        return true;

        // Special files associated with a particular runtime module have not been guaranteed to be in the same directory as the runtime module.
        // in some scenarios (e.g. the ARM64 long-name binaries in the runtime pack for .NET/.NET Core).
        // - In cases where we require the file to be collocated (CLR), just validate existence.
        // - In cases where collocation is not guaranteed, guarantee at most one module under said name exists.
        static (bool FoundMultipleCandidates, FileInfo File) FindCorrelatedFileForRuntimeModule(ITracer tracer, FileInfo[] allFiles, FileInfo runtimeModule, SymbolStoreKey correlatedKey, bool specialFilesRequireAdjacentRuntime)
        {
            string correlatedFileName = Path.GetFileName(correlatedKey.FullPathName);

            if (specialFilesRequireAdjacentRuntime)
            {
                FileInfo correlatedFile = new(Path.Combine(runtimeModule.DirectoryName, correlatedFileName));
                return (FoundMultipleCandidates: false,
                        File: correlatedFile.Exists ? correlatedFile : default);
            }

            FileInfo[] correlatedFiles = allFiles.Where(file => file.Name.Equals(correlatedFileName, StringComparison.OrdinalIgnoreCase)).ToArray();

            if (correlatedFiles.Length > 1)
            {
                tracer.Error($"Multiple files '${correlatedFileName}' found for runtime '{runtimeModule.FullName}': {string.Join<FileInfo>(", ", correlatedFiles)}.");
            }

            return correlatedFiles.Length switch
            {
                0 => (FoundMultipleCandidates: false, default),
                1 => (FoundMultipleCandidates: false, correlatedFiles[0]),
                _ => (FoundMultipleCandidates: true, default)
            };
        }
    }

    private static string CalculateSHA512(FileInfo file)
    {
        using FileStream fileReadStream = file.OpenRead();
        using System.Security.Cryptography.SHA512 sha = System.Security.Cryptography.SHA512.Create();
        byte[] hashValueBytes = sha.ComputeHash(fileReadStream);
        return BitConverter.ToString(hashValueBytes).Replace("-", "");
    }

    private interface IManifestFile
    {
        string Version { get; }
    }

    private sealed class ManifestDataV1 : IManifestFile
    {
        public List<ManifestDataEntry> Entries { get; } = [];

        [JsonInclude]
        public string Version => "1";
    }

    private class ManifestDataEntry
    {
        public string BasedirRelativePath { get; set; }
        public string SymbolKey { get; set; }
        public string Sha512 { get; set; }
        public int DebugInformationLevel { get; set; }
    }
}
