// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.ProjectModel;
using NuGet.RuntimeModel;
using NuGet.Shared;
using NuGet.Versioning;

namespace NuGet.Commands
{
    /// <summary>
    /// Helpers for dealing with dg files and processing msbuild related inputs.
    /// </summary>
    public static class MSBuildRestoreUtility
    {
        // Clear keyword for properties.
        public static readonly string Clear = nameof(Clear);
        private static readonly string[] HttpPrefixes = new string[] { "http:", "https:" };
        private const string DoubleSlash = "//";

        /// <summary>
        /// Creates a <see cref="DependencyGraphSpec" /> from the specified MSBuild items.
        /// </summary>
        /// <param name="items">An <see cref="IEnumerable{T}" /> of <see cref="IMSBuildItem" /> objects representing the MSBuild items gathered for restore.</param>
        public static DependencyGraphSpec GetDependencySpec(IEnumerable<IMSBuildItem> items)
        {
            return GetDependencySpec(items, readOnly: false);
        }
        /// <summary>
        /// Creates a <see cref="DependencyGraphSpec" /> from the specified MSBuild items.
        /// </summary>
        /// <param name="items">An <see cref="IEnumerable{T}" /> of <see cref="IMSBuildItem" /> objects representing the MSBuild items gathered for restore.</param>
        /// <param name="readOnly"><see langword="true" /> to indicate that the <see cref="DependencyGraphSpec" /> is considered read-only and won't be changed, otherwise <see langword="false" />.</param>
        public static DependencyGraphSpec GetDependencySpec(IEnumerable<IMSBuildItem> items, bool readOnly)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            // Unique names created by the MSBuild restore target are project paths, these
            // can be different on case-insensitive file systems for the same project file.
            // To workaround this unique names should be compared based on the OS.
            var uniqueNameComparer = PathUtility.GetStringComparerBasedOnOS();

            var graphSpec = new DependencyGraphSpec(readOnly);
            var itemsById = new Dictionary<string, List<IMSBuildItem>>(uniqueNameComparer);
            var restoreSpecs = new HashSet<string>(uniqueNameComparer);
            var validForRestore = new HashSet<string>(uniqueNameComparer);
            var projectPathLookup = new Dictionary<string, string>(uniqueNameComparer);
            var toolItems = new List<IMSBuildItem>();

            // Sort items and add restore specs
            foreach (var item in items)
            {
                var projectUniqueName = item.GetProperty("ProjectUniqueName");

                if (item.IsType("restorespec"))
                {
                    restoreSpecs.Add(projectUniqueName);
                }
                else if (!string.IsNullOrEmpty(projectUniqueName))
                {
                    List<IMSBuildItem> idItems;
                    if (!itemsById.TryGetValue(projectUniqueName, out idItems))
                    {
                        idItems = new List<IMSBuildItem>(1);
                        itemsById.Add(projectUniqueName, idItems);
                    }

                    idItems.Add(item);
                }
            }

            // Add projects
            var validProjectSpecs = itemsById.Values.Select(GetPackageSpec).Where(e => e != null);

            foreach (var spec in validProjectSpecs)
            {
                // Keep track of all project path casings
                var uniqueName = spec.RestoreMetadata.ProjectUniqueName;
                if (uniqueName != null && !projectPathLookup.ContainsKey(uniqueName))
                {
                    projectPathLookup.Add(uniqueName, uniqueName);
                }

                var projectPath = spec.RestoreMetadata.ProjectPath;
                if (projectPath != null && !projectPathLookup.ContainsKey(projectPath))
                {
                    projectPathLookup.Add(projectPath, projectPath);
                }

                if (spec.RestoreMetadata.ProjectStyle == ProjectStyle.PackageReference
                    || spec.RestoreMetadata.ProjectStyle == ProjectStyle.ProjectJson
                    || spec.RestoreMetadata.ProjectStyle == ProjectStyle.DotnetCliTool)
                {
                    validForRestore.Add(spec.RestoreMetadata.ProjectUniqueName);
                }

                graphSpec.AddProject(spec);
            }

            // Fix project reference casings to match the original project on case insensitive file systems.
            NormalizePathCasings(projectPathLookup, graphSpec);

            // Remove references to projects that could not be read by restore.
            RemoveMissingProjects(graphSpec);

            // Add valid projects to restore section
            foreach (var projectUniqueName in restoreSpecs.Intersect(validForRestore))
            {
                graphSpec.AddRestore(projectUniqueName);
            }

            return graphSpec;
        }

        /// <summary>
        /// Insert asset flags into dependency, based on ;-delimited string args
        /// </summary>
        public static (LibraryIncludeFlags includeType, LibraryIncludeFlags suppressParent) GetLibraryDependencyIncludeFlags(string includeAssets, string excludeAssets, string privateAssets)
        {
            var includeFlags = GetIncludeFlags(includeAssets, LibraryIncludeFlags.All);
            var excludeFlags = GetIncludeFlags(excludeAssets, LibraryIncludeFlags.None);

            var includeType = includeFlags & ~excludeFlags;
            var suppressParent = GetIncludeFlags(privateAssets, LibraryIncludeFlagUtils.DefaultSuppressParent);

            return (includeType, suppressParent);
        }

        /// <summary>
        /// Insert asset flags into project dependency, based on ;-delimited string args
        /// </summary>
        public static void ApplyIncludeFlags(ProjectRestoreReference dependency, string includeAssets, string excludeAssets, string privateAssets)
        {
            if (dependency == null) throw new ArgumentNullException(nameof(dependency));
            dependency.IncludeAssets = GetIncludeFlags(includeAssets, LibraryIncludeFlags.All);
            dependency.ExcludeAssets = GetIncludeFlags(excludeAssets, LibraryIncludeFlags.None);
            dependency.PrivateAssets = GetIncludeFlags(privateAssets, LibraryIncludeFlagUtils.DefaultSuppressParent);
        }

        /// <summary>
        /// Convert MSBuild items to a PackageSpec.
        /// </summary>
        public static PackageSpec GetPackageSpec(IEnumerable<IMSBuildItem> items)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            PackageSpec result = null;

            // There should only be one ProjectSpec per project in the item set,
            // but if multiple do appear take only the first one in an effort
            // to handle this gracefully.
            var specItem = GetItemByType(items, "projectSpec").FirstOrDefault();

            if (specItem != null)
            {
                ProjectStyle restoreType = GetProjectStyle(specItem);

                (bool isCentralPackageManagementEnabled, bool isCentralPackageVersionOverrideDisabled, bool isCentralPackageTransitivePinningEnabled, bool isCentralPackageFloatingVersionsEnabled) = GetCentralPackageManagementSettings(specItem, restoreType);

                // Get base spec
                if (restoreType == ProjectStyle.ProjectJson)
                {
                    result = GetProjectJsonSpec(specItem);
                }
                else
                {
                    // Read msbuild data for PR and related projects
                    result = GetBaseSpec(specItem, restoreType, items);
                }

                // Applies to all types
                result.RestoreMetadata.ProjectStyle = restoreType;
                result.RestoreMetadata.ProjectPath = specItem.GetProperty("ProjectPath");
                result.RestoreMetadata.ProjectUniqueName = specItem.GetProperty("ProjectUniqueName");

                if (string.IsNullOrEmpty(result.Name))
                {
                    result.Name = result.RestoreMetadata.ProjectName
                        ?? result.RestoreMetadata.ProjectUniqueName
                        ?? Path.GetFileNameWithoutExtension(result.FilePath);
                }

                // Read project references for all
                AddProjectReferences(result, items);

                if (restoreType == ProjectStyle.PackageReference
                    || restoreType == ProjectStyle.DotnetCliTool
                    || restoreType == ProjectStyle.ProjectJson
                    || restoreType == ProjectStyle.PackagesConfig)
                {

                    foreach (var source in MSBuildStringUtility.Split(specItem.GetProperty("Sources")))
                    {
                        // Fix slashes incorrectly removed by MSBuild
                        var pkgSource = new PackageSource(FixSourcePath(source));
                        result.RestoreMetadata.Sources.Add(pkgSource);
                    }

                    foreach (var configFilePath in MSBuildStringUtility.Split(specItem.GetProperty("ConfigFilePaths")))
                    {
                        result.RestoreMetadata.ConfigFilePaths.Add(configFilePath);
                    }

                    foreach (var folder in MSBuildStringUtility.Split(specItem.GetProperty("FallbackFolders")))
                    {
                        result.RestoreMetadata.FallbackFolders.Add(folder);
                    }

                    result.RestoreMetadata.PackagesPath = specItem.GetProperty("PackagesPath");
                    result.RestoreMetadata.OutputPath = specItem.GetProperty("OutputPath");
                }

                // Read package references for netcore, tools, and standalone
                if (restoreType == ProjectStyle.PackageReference
                    || restoreType == ProjectStyle.DotnetCliTool)
                {
                    AddPackageReferences(result, items, isCentralPackageManagementEnabled);
                    AddPackageDownloads(result, items);
                    AddFrameworkReferences(result, items);
                    AddPrunePackageReferences(result, items);

                    // Store the original framework strings for msbuild conditionals
                    result.TargetFrameworks.ForEach(tfi =>
                        result.RestoreMetadata.OriginalTargetFrameworks.Add(
                                !string.IsNullOrEmpty(tfi.TargetAlias) ?
                                    tfi.TargetAlias :
                                    tfi.FrameworkName.GetShortFolderName()));
                }

                if (restoreType == ProjectStyle.PackageReference)
                {
                    // Set project version
                    result.Version = GetVersion(specItem);

                    // Add RIDs and Supports
                    result.RuntimeGraph = GetRuntimeGraph(specItem);

                    // Add CrossTargeting flag
                    result.RestoreMetadata.CrossTargeting = IsPropertyTrue(specItem, "CrossTargeting");

                    // Add RestoreLegacyPackagesDirectory flag
                    result.RestoreMetadata.LegacyPackagesDirectory = IsPropertyTrue(
                        specItem,
                        "RestoreLegacyPackagesDirectory");

                    // ValidateRuntimeAssets compat check
                    result.RestoreMetadata.ValidateRuntimeAssets = IsPropertyTrue(specItem, "ValidateRuntimeAssets");

                    // True for .NETCore projects.
                    result.RestoreMetadata.SkipContentFileWrite = IsPropertyTrue(specItem, "SkipContentFileWrite");

                    // Warning properties
                    result.RestoreMetadata.ProjectWideWarningProperties = GetWarningProperties(specItem);

                    // Packages lock file properties
                    result.RestoreMetadata.RestoreLockProperties = GetRestoreLockProperties(specItem);

                    // NuGet audit properties
                    result.RestoreMetadata.RestoreAuditProperties = GetRestoreAuditProperties(specItem, items, GetAuditSuppressions(items));
                }

                if (restoreType == ProjectStyle.PackagesConfig)
                {
                    var pcRestoreMetadata = (PackagesConfigProjectRestoreMetadata)result.RestoreMetadata;
                    // Packages lock file properties
                    pcRestoreMetadata.PackagesConfigPath = specItem.GetProperty("PackagesConfigPath");
                    pcRestoreMetadata.RepositoryPath = specItem.GetProperty("RepositoryPath");
                    var solutionDir = specItem.GetProperty("SolutionDir");
                    if (string.IsNullOrEmpty(pcRestoreMetadata.RepositoryPath) && !string.IsNullOrEmpty(solutionDir) && solutionDir != "*Undefined*")
                    {
                        pcRestoreMetadata.RepositoryPath = Path.Combine(
                            solutionDir,
                            "packages"
                        );
                    }
                    pcRestoreMetadata.RestoreLockProperties = GetRestoreLockProperties(specItem);
                    pcRestoreMetadata.RestoreAuditProperties = GetRestoreAuditProperties(specItem, items, GetAuditSuppressions(items));
                }

                if (restoreType == ProjectStyle.ProjectJson)
                {
                    // Check runtime assets by default for project.json
                    result.RestoreMetadata.ValidateRuntimeAssets = true;
                }

                result.RestoreMetadata.CentralPackageVersionsEnabled = isCentralPackageManagementEnabled;
                result.RestoreMetadata.CentralPackageVersionOverrideDisabled = isCentralPackageVersionOverrideDisabled;
                result.RestoreMetadata.CentralPackageFloatingVersionsEnabled = isCentralPackageFloatingVersionsEnabled;
                result.RestoreMetadata.CentralPackageTransitivePinningEnabled = isCentralPackageTransitivePinningEnabled;
                result.RestoreMetadata.UsingMicrosoftNETSdk = GetUsingMicrosoftNETSdk(specItem.GetProperty("UsingMicrosoftNETSdk"));
                result.RestoreMetadata.SdkAnalysisLevel = GetSdkAnalysisLevel(specItem.GetProperty("SdkAnalysisLevel"));
                result.RestoreMetadata.UseLegacyDependencyResolver = IsPropertyTrue(specItem, "RestoreUseLegacyDependencyResolver");
            }

            return result;
        }

        /// <summary>
        /// Remove missing project dependencies. These are typically caused by
        /// non-NuGet projects which are missing the targets needed to walk them.
        /// Visual Studio ignores these projects so from the command line we should
        /// also. Build will fail with the appropriate errors for missing projects
        /// restore should not warn or message for this.
        /// </summary>
        public static void RemoveMissingProjects(DependencyGraphSpec graphSpec)
        {
            var existingProjects = new HashSet<string>(
                graphSpec.Projects.Select(e => e.RestoreMetadata.ProjectPath),
                PathUtility.GetStringComparerBasedOnOS());

            foreach (var project in graphSpec.Projects)
            {
                foreach (var framework in project.RestoreMetadata.TargetFrameworks)
                {
                    // Loop through the items in reverse order so items can be removed from the collection safely
                    for (int i = framework.ProjectReferences.Count - 1; i >= 0; i--)
                    {
                        if (!existingProjects.Contains(framework.ProjectReferences[i].ProjectPath))
                        {
                            framework.ProjectReferences.Remove(framework.ProjectReferences[i]);
                        }
                    }
                }
            }
        }

        public static void NormalizePathCasings(Dictionary<string, string> paths, DependencyGraphSpec graphSpec)
        {
            NormalizePathCasings((IDictionary<string, string>)paths, graphSpec);
        }

        /// <summary>
        /// Change all project paths to the same casing.
        /// </summary>
        public static void NormalizePathCasings(IDictionary<string, string> paths, DependencyGraphSpec graphSpec)
        {
            if (PathUtility.IsFileSystemCaseInsensitive)
            {
                foreach (var project in graphSpec.Projects)
                {
                    foreach (var framework in project.RestoreMetadata.TargetFrameworks)
                    {
                        foreach (var projectReference in framework.ProjectReferences)
                        {
                            // Check reference unique name
                            var refUniqueName = projectReference.ProjectUniqueName;

                            if (refUniqueName != null
                                && paths.TryGetValue(refUniqueName, out var refUniqueNameCasing)
                                && !StringComparer.Ordinal.Equals(refUniqueNameCasing, refUniqueName))
                            {
                                projectReference.ProjectUniqueName = refUniqueNameCasing;
                            }

                            // Check reference project path
                            var projectRefPath = projectReference.ProjectPath;

                            if (projectRefPath != null
                                && paths.TryGetValue(projectRefPath, out var projectRefPathCasing)
                                && !StringComparer.Ordinal.Equals(projectRefPathCasing, projectRefPath))
                            {
                                projectReference.ProjectPath = projectRefPathCasing;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// True if the list contains CLEAR.
        /// </summary>
        public static bool ContainsClearKeyword(IEnumerable<string> values)
        {
            return (values?.Contains(Clear, StringComparer.OrdinalIgnoreCase) == true);
        }

        /// <summary>
        /// True if the list contains CLEAR and non-CLEAR keywords.
        /// </summary>
        /// <remarks>CLEAR;CLEAR is considered valid.</remarks>
        public static bool HasInvalidClear(IEnumerable<string> values)
        {
            return ContainsClearKeyword(values)
                    && (values?.Any(e => !StringComparer.OrdinalIgnoreCase.Equals(e, Clear)) == true);
        }

        /// <summary>
        /// Logs an error if CLEAR is used with non-CLEAR entries.
        /// </summary>
        /// <returns>True if an invalid combination exists.</returns>
        public static bool LogErrorForClearIfInvalid(IEnumerable<string> values, string projectPath, ILogger logger)
        {
            if (HasInvalidClear(values))
            {
                var text = string.Format(CultureInfo.CurrentCulture, Strings.CannotBeUsedWithOtherValues, Clear);
                var message = LogMessage.CreateError(NuGetLogCode.NU1002, text);
                message.ProjectPath = projectPath;
                logger.Log(message);

                return true;
            }

            return false;
        }

        /// <summary>
        /// Log warning NU1503
        /// </summary>
        public static RestoreLogMessage GetWarningForUnsupportedProject(string path)
        {
            var text = string.Format(CultureInfo.CurrentCulture, Strings.UnsupportedProject, path);
            var message = RestoreLogMessage.CreateWarning(NuGetLogCode.NU1503, text);
            message.FilePath = path;
            message.ProjectPath = path;

            return message;
        }

        public static RestoreLogMessage GetMessageForUnsupportedProject(string path)
        {
            var text = string.Format(CultureInfo.CurrentCulture, Strings.UnsupportedProject, path);
            var message = new RestoreLogMessage(LogLevel.Information, text);
            message.FilePath = path;
            message.ProjectPath = path;

            return message;
        }

        private static IEnumerable<TargetFrameworkInformation> GetTargetFrameworkInformation(string filePath, ProjectStyle restoreType, IEnumerable<IMSBuildItem> items)
        {
            var uniqueIds = new HashSet<string>();
            foreach (var item in GetItemByType(items, "TargetFrameworkInformation"))
            {
                var frameworkString = item.GetProperty("TargetFramework");
                var targetFrameworkMoniker = item.GetProperty("TargetFrameworkMoniker");
                var targetPlatforMoniker = item.GetProperty("TargetPlatformMoniker");
                var targetPlatformMinVersion = item.GetProperty("TargetPlatformMinVersion");
                var clrSupport = item.GetProperty("CLRSupport");
                var windowsTargetPlatformMinVersion = item.GetProperty("WindowsTargetPlatformMinVersion");
                var targetAlias = string.IsNullOrEmpty(frameworkString) ? string.Empty : frameworkString;
                if (uniqueIds.Contains(targetAlias))
                {
                    continue;
                }
                uniqueIds.Add(targetAlias);

                NuGetFramework targetFramework = MSBuildProjectFrameworkUtility.GetProjectFramework(
                    projectFilePath: filePath,
                    targetFrameworkMoniker: targetFrameworkMoniker,
                    targetPlatformMoniker: targetPlatforMoniker,
                    targetPlatformMinVersion: targetPlatformMinVersion,
                    clrSupport: clrSupport,
                    windowsTargetPlatformMinVersion: windowsTargetPlatformMinVersion);

                string runtimeIdentifierGraphPath = null;
                ImmutableArray<NuGetFramework> imports = [];
                bool assetTargetFallback = false;
                bool warn = false;

                if (restoreType == ProjectStyle.PackageReference)
                {
                    var packageTargetFallback = MSBuildStringUtility.Split(item.GetProperty("PackageTargetFallback"))
                        .Select(NuGetFramework.Parse)
                        .ToList();

                    var assetTargetFallbackList = MSBuildStringUtility.Split(item.GetProperty(AssetTargetFallbackUtility.AssetTargetFallback))
                        .Select(NuGetFramework.Parse)
                        .ToList();

                    // Throw if an invalid combination was used.
                    AssetTargetFallbackUtility.EnsureValidFallback(packageTargetFallback, assetTargetFallbackList, filePath);

                    // Update the framework appropriately
                    (targetFramework, imports, assetTargetFallback, warn) = AssetTargetFallbackUtility.GetFallbackFrameworkInformation(targetFramework, packageTargetFallback, assetTargetFallbackList);

                    runtimeIdentifierGraphPath = item.GetProperty("RuntimeIdentifierGraphPath");
                }

                var targetFrameworkInfo = new TargetFrameworkInformation()
                {
                    AssetTargetFallback = assetTargetFallback,
                    FrameworkName = targetFramework,
                    Imports = imports,
                    RuntimeIdentifierGraphPath = runtimeIdentifierGraphPath,
                    TargetAlias = targetAlias,
                    Warn = warn
                };

                yield return targetFrameworkInfo;
            }
        }

        /// <summary>
        /// Remove duplicates and excluded values a set of sources or fallback folders.
        /// </summary>
        /// <remarks>Compares with Ordinal, excludes must be exact matches.</remarks>
        public static IEnumerable<string> AggregateSources(IEnumerable<string> values, IEnumerable<string> excludeValues)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            if (excludeValues == null)
            {
                throw new ArgumentNullException(nameof(excludeValues));
            }

            var result = new SortedSet<string>(values, StringComparer.Ordinal);

            // Remove excludes
            result.ExceptWith(excludeValues);

            return result;
        }

        private static RuntimeGraph GetRuntimeGraph(IMSBuildItem specItem)
        {
            var runtimes = MSBuildStringUtility.Split(specItem.GetProperty("RuntimeIdentifiers"))
                .Distinct(StringComparer.Ordinal)
                .Select(rid => new RuntimeDescription(rid))
                .ToList();

            var supports = MSBuildStringUtility.Split(specItem.GetProperty("RuntimeSupports"))
                .Distinct(StringComparer.Ordinal)
                .Select(s => new CompatibilityProfile(s))
                .ToList();

            return new RuntimeGraph(runtimes, supports);
        }

        private static void AddProjectReferences(PackageSpec spec, IEnumerable<IMSBuildItem> items)
        {
            // Add groups for each spec framework
            var aliasGroups = new Dictionary<string, List<ProjectRestoreReference>>();

            foreach (string alias in spec.TargetFrameworks.Select(e => e.TargetAlias).Distinct())
            {
                aliasGroups.Add(alias, new List<ProjectRestoreReference>());
            }

            var flatReferences = GetItemByType(items, "ProjectReference")
                .Select(GetProjectRestoreReference);

            var comparer = PathUtility.GetStringComparerBasedOnOS();

            // Add project paths
            foreach (var frameworkPair in flatReferences)
            {
                // If no frameworks were given, apply to all
                var addToFrameworks = frameworkPair.Item1.Count == 0
                    ? aliasGroups.Keys.ToList()
                    : frameworkPair.Item1;

                foreach (var framework in addToFrameworks)
                {
                    List<ProjectRestoreReference> references;
                    if (aliasGroups.TryGetValue(framework, out references))
                    {
                        // Ensure unique
                        if (!references.Any(e => comparer.Equals(e.ProjectUniqueName, frameworkPair.Item2.ProjectUniqueName)))
                        {
                            references.Add(frameworkPair.Item2);
                        }
                    }
                }
            }

            // Add groups to spec
            foreach (KeyValuePair<string, List<ProjectRestoreReference>> frameworkPair in aliasGroups)
            {
                TargetFrameworkInformation targetFrameworkInformation = spec.TargetFrameworks.Single(e => e.TargetAlias.Equals(frameworkPair.Key, StringComparison.Ordinal));
                spec.RestoreMetadata.TargetFrameworks.Add(new ProjectRestoreMetadataFrameworkInfo(targetFrameworkInformation.FrameworkName)
                {
                    ProjectReferences = frameworkPair.Value,
                    TargetAlias = targetFrameworkInformation.TargetAlias
                });
            }
        }

        private static Tuple<List<string>, ProjectRestoreReference> GetProjectRestoreReference(IMSBuildItem item)
        {
            var frameworks = GetFrameworks(item).ToList();

            var reference = new ProjectRestoreReference()
            {
                ProjectPath = item.GetProperty("ProjectPath"),
                ProjectUniqueName = item.GetProperty("ProjectReferenceUniqueName"),
            };

            ApplyIncludeFlags(reference, item.GetProperty("IncludeAssets"), item.GetProperty("ExcludeAssets"), item.GetProperty("PrivateAssets"));

            return new Tuple<List<string>, ProjectRestoreReference>(frameworks, reference);
        }

        private static bool AddDownloadDependencyIfNotExist(PackageSpec spec, string targetAlias, DownloadDependency dependency)
        {
            var index = spec.TargetFrameworks.SingleIndex(e => e.TargetAlias.Equals(targetAlias, StringComparison.Ordinal));
            var frameworkInfo = spec.TargetFrameworks[index];

            if (!frameworkInfo.DownloadDependencies.Contains(dependency))
            {
                var newDownloadDependencies = frameworkInfo.DownloadDependencies.Add(dependency);
                spec.TargetFrameworks[index] = new TargetFrameworkInformation(frameworkInfo) { DownloadDependencies = newDownloadDependencies };

                return true;
            }
            return false;
        }

        private static bool AddDependencyIfNotExist(PackageSpec spec, string targetAlias, LibraryDependency dependency)
        {
            var index = spec.TargetFrameworks.SingleIndex(e => e.TargetAlias.Equals(targetAlias, StringComparison.Ordinal));
            var frameworkInfo = spec.TargetFrameworks[index];
            var dependencies = frameworkInfo.Dependencies;

            if (!spec.Dependencies
                            .Concat(dependencies)
                            .Select(d => d.Name)
                            .Contains(dependency.Name, StringComparer.OrdinalIgnoreCase))
            {
                spec.TargetFrameworks[index] = new TargetFrameworkInformation(frameworkInfo) { Dependencies = dependencies.Add(dependency) };

                return true;
            }

            return false;
        }

        private static void AddPackageReferences(PackageSpec spec, IEnumerable<IMSBuildItem> items, bool isCpvmEnabled)
        {
            if (isCpvmEnabled)
            {
                AddCentralPackageVersions(spec, items);
            }

            var dict = new Dictionary<string, TargetFrameworkInformation>(StringComparer.OrdinalIgnoreCase);
            foreach (var targetFramework in spec.TargetFrameworks)
            {
                dict.Add(targetFramework.TargetAlias, targetFramework);
            }

            foreach (var item in GetItemByType(items, "Dependency"))
            {
                // Get warning suppressions
                var noWarn = MSBuildStringUtility.GetNuGetLogCodes(item.GetProperty("NoWarn"));
                (var includeType, var suppressParent) = GetLibraryDependencyIncludeFlags(item);
                IEnumerable<string> frameworks = GetFrameworks(item);
                string name = item.GetProperty("Id");
                bool autoReferenced = IsPropertyTrue(item, "IsImplicitlyDefined");
                VersionRange versionOverrideRange = GetVersionRange(item, defaultValue: null, "VersionOverride");

                VersionRange versionRange = GetVersionRange(item, defaultValue: isCpvmEnabled ? null : VersionRange.All);
                bool versionDefined = versionRange != null;
                if (versionRange == null && !isCpvmEnabled)
                {
                    versionRange = VersionRange.All;
                }

                if (!frameworks.Any())
                {
                    frameworks = dict.Keys;
                }

                foreach (var framework in frameworks)
                {
                    dict.TryGetValue(framework, out TargetFrameworkInformation frameworkInfo);
                    CentralPackageVersion centralPackageVersion = null;
                    bool isCentrallyManaged = !versionDefined && !autoReferenced && isCpvmEnabled && versionOverrideRange == null && frameworkInfo.CentralPackageVersions != null && frameworkInfo.CentralPackageVersions.TryGetValue(name, out centralPackageVersion);

                    if (centralPackageVersion != null)
                    {
                        versionRange = centralPackageVersion.VersionRange;
                    }
                    versionRange = versionOverrideRange ?? versionRange;

                    var dependency = new LibraryDependency()
                    {
                        LibraryRange = new LibraryRange(
                            name: name,
                            versionRange: versionRange,
                            typeConstraint: LibraryDependencyTarget.Package),

                        AutoReferenced = autoReferenced,
                        GeneratePathProperty = IsPropertyTrue(item, "GeneratePathProperty"),
                        Aliases = item.GetProperty("Aliases"),
                        VersionOverride = versionOverrideRange,
                        NoWarn = noWarn,
                        IncludeType = includeType,
                        SuppressParent = suppressParent,
                        VersionCentrallyManaged = isCentrallyManaged,
                    };

                    AddDependencyIfNotExist(spec, framework, dependency);
                }
            }
        }

        internal static void AddPrunePackageReferences(PackageSpec spec, IEnumerable<IMSBuildItem> items)
        {
            var prunePackageReferences = new Dictionary<string, Dictionary<string, PrunePackageReference>>(StringComparer.OrdinalIgnoreCase);
            var isPruningEnabled = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (var targetFramework in spec.TargetFrameworks)
            {
                prunePackageReferences.Add(targetFramework.TargetAlias, new Dictionary<string, PrunePackageReference>(StringComparer.OrdinalIgnoreCase));
            }

            foreach (var item in GetItemByType(items, "TargetFrameworkInformation"))
            {
                var tfm = item.GetProperty("TargetFramework") ?? string.Empty;

                bool enabled = IsPropertyTrue(item, "RestoreEnablePackagePruning");
                isPruningEnabled[tfm] = enabled;
            }

            foreach (var item in GetItemByType(items, "PrunePackageReference"))
            {
                var id = item.GetProperty("Id");
                var versionString = item.GetProperty("VersionRange");
                HashSet<string> tfms = GetFrameworks(item);

                if (tfms.Count > 0)
                {
                    foreach (var targetAlias in tfms)
                    {
                        if (isPruningEnabled[targetAlias])
                        {
                            var frameworkInfo = prunePackageReferences[targetAlias];
                            AddPackageToPrune(id, versionString, frameworkInfo);
                        }
                    }
                }
                else
                {
                    foreach (var frameworkInfo in prunePackageReferences)
                    {
                        if (isPruningEnabled[frameworkInfo.Key])
                        {
                            AddPackageToPrune(id, versionString, frameworkInfo.Value);
                        }
                    }
                }
            }

            for (int i = 0; i < spec.TargetFrameworks.Count; i++)
            {
                spec.TargetFrameworks[i] = new TargetFrameworkInformation(spec.TargetFrameworks[i]) { PackagesToPrune = prunePackageReferences[spec.TargetFrameworks[i].TargetAlias] };
            }

            static void AddPackageToPrune(string id, string version, Dictionary<string, PrunePackageReference> frameworkInfo)
            {
                if (!frameworkInfo.ContainsKey(id))
                {
                    frameworkInfo.Add(id, PrunePackageReference.Create(id, version!));
                }
            }
        }

        internal static void AddPackageDownloads(PackageSpec spec, IEnumerable<IMSBuildItem> items)
        {
            var splitChars = new[] { ';' };

            foreach (var item in GetItemByType(items, "DownloadDependency"))
            {
                var id = item.GetProperty("Id");
                var versionString = item.GetProperty("VersionRange");
                if (string.IsNullOrEmpty(versionString))
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_PackageDownload_NoVersion, id));
                }

                var versions = versionString.Split(splitChars, StringSplitOptions.RemoveEmptyEntries);

                foreach (var version in versions)
                {
                    var versionRange = GetVersionRange(version, defaultValue: VersionRange.All);

                    if (!(versionRange.HasLowerAndUpperBounds && versionRange.MinVersion.Equals(versionRange.MaxVersion)))
                    {
                        throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Error_PackageDownload_OnlyExactVersionsAreAllowed, id, versionRange.OriginalString));
                    }

                    var downloadDependency = new DownloadDependency(id, versionRange);

                    var frameworks = GetFrameworks(item);
                    foreach (var framework in frameworks)
                    {
                        AddDownloadDependencyIfNotExist(spec, framework, downloadDependency);
                    }
                }
            }
        }

        private static (LibraryIncludeFlags includeType, LibraryIncludeFlags suppressParent) GetLibraryDependencyIncludeFlags(IMSBuildItem item)
        {
            return GetLibraryDependencyIncludeFlags(item.GetProperty("IncludeAssets"), item.GetProperty("ExcludeAssets"), item.GetProperty("PrivateAssets"));
        }

        private static LibraryIncludeFlags GetIncludeFlags(string value, LibraryIncludeFlags defaultValue)
        {
            var parts = MSBuildStringUtility.Split(value);

            if (parts.Length > 0)
            {
                return LibraryIncludeFlagUtils.GetFlags(parts);
            }
            else
            {
                return defaultValue;
            }
        }

        private static void AddFrameworkReferences(PackageSpec spec, IEnumerable<IMSBuildItem> items)
        {
            foreach (var item in GetItemByType(items, "FrameworkReference"))
            {
                var frameworkReference = item.GetProperty("Id");
                var frameworks = GetFrameworks(item);

                var privateAssets = item.GetProperty("PrivateAssets");

                foreach (var framework in frameworks)
                {
                    AddFrameworkReferenceIfNotExists(spec, framework, frameworkReference, privateAssets);
                }
            }
        }

        private static bool AddFrameworkReferenceIfNotExists(PackageSpec spec, string targetAlias, string frameworkReference, string privateAssetsValue)
        {
            var index = spec.TargetFrameworks.SingleIndex(e => e.TargetAlias.Equals(targetAlias, StringComparison.Ordinal));
            TargetFrameworkInformation frameworkInfo = spec.TargetFrameworks[index];

            if (!frameworkInfo
                .FrameworkReferences
                .Select(f => f.Name)
                .Contains(frameworkReference, ComparisonUtility.FrameworkReferenceNameComparer))
            {
                var privateAssets = FrameworkDependencyFlagsUtils.GetFlags(MSBuildStringUtility.Split(privateAssetsValue));
                FrameworkDependency[] newFrameworkReferences = [.. frameworkInfo.FrameworkReferences, new FrameworkDependency(frameworkReference, privateAssets)];
                spec.TargetFrameworks[index] = new TargetFrameworkInformation(frameworkInfo) { FrameworkReferences = newFrameworkReferences };
                return true;
            }
            return false;
        }

        private static VersionRange GetVersionRange(IMSBuildItem item, VersionRange defaultValue, string propertyName = "VersionRange")
        {
            var rangeString = item.GetProperty(propertyName);
            return GetVersionRange(rangeString, defaultValue);
        }

        private static VersionRange GetVersionRange(string rangeString, VersionRange defaultValue)
        {
            if (!string.IsNullOrEmpty(rangeString))
            {
                return VersionRange.Parse(rangeString);
            }

            return defaultValue;
        }

        private static PackageSpec GetProjectJsonSpec(IMSBuildItem specItem)
        {
            PackageSpec result;
            var projectPath = specItem.GetProperty("ProjectPath");
            var projectName = Path.GetFileNameWithoutExtension(projectPath);
            var projectJsonPath = specItem.GetProperty("ProjectJsonPath");

            // Read project.json
            result = JsonPackageSpecReader.GetPackageSpec(projectName, projectJsonPath);

            result.RestoreMetadata = new ProjectRestoreMetadata();
            result.RestoreMetadata.ProjectJsonPath = projectJsonPath;
            result.RestoreMetadata.ProjectName = projectName;
            return result;
        }

        private static PackageSpec GetBaseSpec(IMSBuildItem specItem, ProjectStyle projectStyle, IEnumerable<IMSBuildItem> items)
        {
            var spec = new PackageSpec();
            spec.RestoreMetadata = projectStyle == ProjectStyle.PackagesConfig
                ? new PackagesConfigProjectRestoreMetadata()
                : new ProjectRestoreMetadata();
            spec.FilePath = specItem.GetProperty("ProjectPath");
            spec.RestoreMetadata.ProjectName = specItem.GetProperty("ProjectName");

            if (projectStyle == ProjectStyle.DotnetCliTool || projectStyle == ProjectStyle.Unknown || projectStyle == ProjectStyle.PackagesConfig)
            {
                var tfmProperty = specItem.GetProperty("TargetFrameworks");
                if (!string.IsNullOrEmpty(tfmProperty))
                {
                    var needsAlias = projectStyle == ProjectStyle.DotnetCliTool;
                    spec.TargetFrameworks.Add(
                        new TargetFrameworkInformation()
                        {
                            FrameworkName = NuGetFramework.Parse(tfmProperty),
                            TargetAlias = needsAlias ? tfmProperty : string.Empty
                        });
                }
            }
            else
            {
                spec.TargetFrameworks.AddRange(GetTargetFrameworkInformation(spec.FilePath, projectStyle, items).Distinct());
            }
            return spec;
        }

        private static HashSet<string> GetFrameworks(IMSBuildItem item)
        {
            return GetTargetFrameworkStrings(item);
        }

        private static HashSet<string> GetTargetFrameworkStrings(IMSBuildItem item)
        {
            var frameworks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var frameworksString = item.GetProperty("TargetFrameworks");
            if (!string.IsNullOrEmpty(frameworksString))
            {
                frameworks.UnionWith(MSBuildStringUtility.Split(frameworksString));
            }

            return frameworks;
        }

        private static IEnumerable<IMSBuildItem> GetItemByType(IEnumerable<IMSBuildItem> items, string type)
        {
            return items.Where(e => e.IsType(type));
        }

        private static bool IsType(this IMSBuildItem item, string type)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(type, item?.GetProperty("Type"));
        }

        /// <summary>
        /// Return the parsed version or 1.0.0 if the property does not exist.
        /// </summary>
        private static NuGetVersion GetVersion(IMSBuildItem item)
        {
            var versionString = item.GetProperty("Version");
            NuGetVersion version = null;

            if (string.IsNullOrEmpty(versionString))
            {
                // Default to 1.0.0 if the property does not exist
                version = new NuGetVersion(1, 0, 0);
            }
            else
            {
                // Snapshot versions are not allowed in .NETCore
                version = NuGetVersion.Parse(versionString);
            }

            return version;
        }

        public static void Dump(IEnumerable<IMSBuildItem> items, ILogger log)
        {
            foreach (var item in items)
            {
                log.LogDebug($"Item: {item.Identity}");

                foreach (var key in item.Properties)
                {
                    var val = item.GetProperty(key);

                    if (!string.IsNullOrEmpty(val))
                    {
                        log.LogDebug($"  {key}={val}");
                    }
                }
            }
        }

        private static WarningProperties GetWarningProperties(IMSBuildItem specItem)
        {
            return WarningProperties.GetWarningProperties(
                treatWarningsAsErrors: specItem.GetProperty("TreatWarningsAsErrors"),
                warningsAsErrors: specItem.GetProperty("WarningsAsErrors"),
                noWarn: specItem.GetProperty("NoWarn"),
                warningsNotAsErrors: specItem.GetProperty("WarningsNotAsErrors"));
        }

        private static RestoreLockProperties GetRestoreLockProperties(IMSBuildItem specItem)
        {
            return new RestoreLockProperties(
                specItem.GetProperty("RestorePackagesWithLockFile"),
                specItem.GetProperty("NuGetLockFilePath"),
                IsPropertyTrue(specItem, "RestoreLockedMode"));
        }

        public static RestoreAuditProperties GetRestoreAuditProperties(IMSBuildItem specItem, IEnumerable<IMSBuildItem> allItems, HashSet<string> suppressionItems)
        {
            string enableAudit = specItem.GetProperty("NuGetAudit");
            string auditLevel = specItem.GetProperty("NuGetAuditLevel");
            string auditMode = GetAuditMode(specItem, allItems);

            if (enableAudit != null || auditLevel != null || auditMode != null
                || (suppressionItems != null && suppressionItems.Count > 0))
            {
                return new RestoreAuditProperties()
                {
                    EnableAudit = enableAudit,
                    AuditLevel = auditLevel,
                    AuditMode = auditMode,
                    SuppressedAdvisories = suppressionItems?.Count > 0 ? suppressionItems : null
                };
            }

            return null;

            // We want to set NuGetAuditMode to "all" if a multi-targeting project targets .NET 10 or higher.
            // However, that can only be done by an "inner build" evaulation, but we read other audit settings
            // from the project evaluation, not inner-builds. So, check the inner builds if any TFM sets mode
            // to "all", otherwise use the project's "outer build" mode.
            string GetAuditMode(IMSBuildItem project, IEnumerable<IMSBuildItem> tfms)
            {
                foreach (var item in tfms.NoAllocEnumerate())
                {
                    string auditMode = item.GetProperty("NuGetAuditMode");
                    if (string.Equals(auditMode, "all", StringComparison.OrdinalIgnoreCase))
                    {
                        return auditMode;
                    }
                }

                string projectAuditMode = project.GetProperty("NuGetAuditMode");
                return projectAuditMode;
            }
        }

        public static NuGetVersion GetSdkAnalysisLevel(string sdkAnalysisLevel)
        {
            if (string.IsNullOrEmpty(sdkAnalysisLevel))
            {
                return null;
            }

            if (!NuGetVersion.TryParse(sdkAnalysisLevel, out NuGetVersion version))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Invalid_AttributeValue, "SdkAnalysisLevel", sdkAnalysisLevel, "9.0.100"));
            }

            return version;
        }

        public static bool GetUsingMicrosoftNETSdk(string usingMicrosoftNETSdk)
        {
            if (string.IsNullOrEmpty(usingMicrosoftNETSdk))
            {
                return false;
            }

            if (!bool.TryParse(usingMicrosoftNETSdk, out var result))
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, Strings.Invalid_AttributeValue, "UsingMicrosoftNETSdk", usingMicrosoftNETSdk, "false"));
            }

            return result;
        }

        private static HashSet<string> GetAuditSuppressions(IEnumerable<IMSBuildItem> items)
        {
            IEnumerable<string> suppressions = GetItemByType(items, "NuGetAuditSuppress")
                                                    .Select(i => i.GetProperty("Id"));

            return (suppressions != null && suppressions.Any())
                    ? new HashSet<string>(suppressions)
                    : null;
        }

        /// <summary>
        /// Convert http:/url to http://url
        /// If not needed the same path is returned. This is to work around
        /// issues with msbuild dropping slashes from paths on linux and osx.
        /// </summary>
        public static string FixSourcePath(string s)
        {
            var result = s;

            if (result.IndexOf("/", StringComparison.Ordinal) >= -1 && result.IndexOf(DoubleSlash, StringComparison.Ordinal) == -1)
            {
                for (var i = 0; i < HttpPrefixes.Length; i++)
                {
                    result = FixSourcePath(result, HttpPrefixes[i], DoubleSlash);
                }

                // For non-windows machines use file:///
                var fileSlashes = RuntimeEnvironmentHelper.IsWindows ? DoubleSlash : "///";
                result = FixSourcePath(result, "file:", fileSlashes);
            }

            return result;
        }

        private static string FixSourcePath(string s, string prefixWithoutSlashes, string slashes)
        {
            if (s.Length >= (prefixWithoutSlashes.Length + 2)
                && s.StartsWith($"{prefixWithoutSlashes}/", StringComparison.OrdinalIgnoreCase)
                && !s.StartsWith($"{prefixWithoutSlashes}{DoubleSlash}", StringComparison.OrdinalIgnoreCase))
            {
                // original prefix casing + // + rest of the path
                return s.Substring(0, prefixWithoutSlashes.Length) + slashes + s.Substring(prefixWithoutSlashes.Length + 1);
            }

            return s;
        }

        internal static bool IsPropertyFalse(IMSBuildItem item, string propertyName, bool defaultValue = false)
        {
            string value = item.GetProperty(propertyName);

            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            return string.Equals(value, bool.FalseString, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsPropertyTrue(IMSBuildItem item, string propertyName, bool defaultValue = false)
        {
            string value = item.GetProperty(propertyName);

            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            return string.Equals(value, bool.TrueString, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Function used to display errors and warnings at the end of restore operation.
        /// The errors and warnings are read from the assets file based on restore result.
        /// </summary>
        /// <param name="messages">Messages to log.</param>
        /// <param name="logger">Logger used to display warnings and errors.</param>
        public static Task ReplayWarningsAndErrorsAsync(IEnumerable<IAssetsLogMessage> messages, ILogger logger)
        {
            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            var logMessages = messages?.Select(m => m.AsRestoreLogMessage()) ??
                              Enumerable.Empty<RestoreLogMessage>();

            return logger.LogMessagesAsync(logMessages);
        }

        private static Dictionary<string, Dictionary<string, CentralPackageVersion>> CreateCentralVersionDependencies(IEnumerable<IMSBuildItem> items,
            IList<TargetFrameworkInformation> specFrameworks)
        {
            IEnumerable<IMSBuildItem> centralVersions = GetItemByType(items, "CentralPackageVersion")?.Distinct(MSBuildItemIdentityComparer.Default).ToList();
            var result = new Dictionary<string, Dictionary<string, CentralPackageVersion>>();

            foreach (IMSBuildItem cv in centralVersions)
            {
                HashSet<string> tfms = GetFrameworks(cv);
                string version = cv.GetProperty("VersionRange");
                CentralPackageVersion centralPackageVersion = new CentralPackageVersion(cv.GetProperty("Id"), string.IsNullOrWhiteSpace(version) ? VersionRange.All : VersionRange.Parse(version));

                if (tfms.Count > 0)
                {
                    AddCentralPackageVersion(result, centralPackageVersion, tfms);
                }
                else
                {
                    AddCentralPackageVersion(result, centralPackageVersion, specFrameworks.Select(f => f.TargetAlias));
                }
            }

            return result;
        }

        private static void AddCentralPackageVersion(Dictionary<string, Dictionary<string, CentralPackageVersion>> centralPackageVersions,
            CentralPackageVersion centralPackageVersion,
            IEnumerable<string> frameworks)
        {
            foreach (var framework in frameworks)
            {
                if (!centralPackageVersions.TryGetValue(framework, out Dictionary<string, CentralPackageVersion> versions))
                {
                    versions = new Dictionary<string, CentralPackageVersion>(StringComparer.OrdinalIgnoreCase);

                    centralPackageVersions.Add(framework, versions);
                }

                versions[centralPackageVersion.Name] = centralPackageVersion;
            }
        }

        private static ProjectStyle GetProjectStyle(IMSBuildItem projectSpecItem)
        {
            var typeString = projectSpecItem.GetProperty("ProjectStyle");
            var restoreType = ProjectStyle.Unknown;

            if (!string.IsNullOrEmpty(typeString))
            {
                Enum.TryParse(typeString, ignoreCase: true, result: out restoreType);
            }

            return restoreType;
        }

        /// <summary>
        /// Determines the current settings for central package management for the specified project.
        /// </summary>
        /// <param name="projectSpecItem">The <see cref="IMSBuildItem" /> to get the central package management settings from.</param>
        /// <param name="projectStyle">The <see cref="ProjectStyle?" /> of the specified project.  Specify <see langword="null" /> when the project does not define a restore style.</param>
        /// <returns>A <see cref="Tuple{T1, T2, T3, T4}" /> containing values indicating whether or not central package management is enabled, if the ability to override a package version </returns>
        public static (bool IsEnabled, bool IsVersionOverrideDisabled, bool IsCentralPackageTransitivePinningEnabled, bool isCentralPackageFloatingVersionsEnabled) GetCentralPackageManagementSettings(IMSBuildItem projectSpecItem, ProjectStyle projectStyle)
        {
            if (projectStyle == ProjectStyle.PackageReference)
            {
                bool isEnabled = IsPropertyTrue(projectSpecItem, "_CentralPackageVersionsEnabled");
                bool isVersionOverrideDisabled = IsPropertyFalse(projectSpecItem, "CentralPackageVersionOverrideEnabled");
                bool isCentralPackageTransitivePinningEnabled = IsPropertyTrue(projectSpecItem, "CentralPackageTransitivePinningEnabled");
                bool isCentralPackageFloatingVersionsEnabled = IsPropertyTrue(projectSpecItem, "CentralPackageFloatingVersionsEnabled");
                return (isEnabled, isVersionOverrideDisabled, isCentralPackageTransitivePinningEnabled, isCentralPackageFloatingVersionsEnabled);
            }

            return (false, false, false, false);
        }

        private static void AddCentralPackageVersions(PackageSpec spec, IEnumerable<IMSBuildItem> items)
        {
            var centralVersionsDependencies = CreateCentralVersionDependencies(items, spec.TargetFrameworks);
            foreach ((var targetAlias, var versions) in centralVersionsDependencies)
            {
                var index = spec.TargetFrameworks.FirstIndex(f => targetAlias.Equals(f.TargetAlias, StringComparison.OrdinalIgnoreCase));
                var frameworkInfo = spec.TargetFrameworks[index];

                spec.TargetFrameworks[index] = new TargetFrameworkInformation(frameworkInfo)
                {
                    CentralPackageVersions = versions,
                };
            }
        }
    }
}
