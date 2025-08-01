// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.DependencyModel;
using Microsoft.NET.Build.Tasks;
using Newtonsoft.Json.Linq;
using static Microsoft.NET.Publish.Tests.PublishTestUtils;
using static Microsoft.NET.Publish.Tests.ILLinkTestUtils;

namespace Microsoft.NET.Publish.Tests
{
    // this test class is split up arbitrarily so Helix can run tests in multiple workitems
    public class GivenThatWeWantToRunILLink1 : SdkTest
    {
        public GivenThatWeWantToRunILLink1(ITestOutputHelper log) : base(log)
        {
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_only_runs_when_switch_is_enabled(string targetFramework)
        {
            if (targetFramework == "net5.0" &&
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                //  https://github.com/dotnet/sdk/issues/49665
                return;
            }

            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            Directory.Exists(linkedDirectory).Should().BeFalse();

            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");
            var unusedFrameworkDll = Path.Combine(publishDirectory, $"{UnusedFrameworkAssembly}.dll");

            // Linker inputs are kept, including unused assemblies
            File.Exists(publishedDll).Should().BeTrue();
            File.Exists(unusedDll).Should().BeTrue();
            File.Exists(unusedFrameworkDll).Should().BeTrue();

            var depsFile = Path.Combine(publishDirectory, $"{projectName}.deps.json");
            DoesDepsFileHaveAssembly(depsFile, referenceProjectName).Should().BeTrue();
            DoesDepsFileHaveAssembly(depsFile, UnusedFrameworkAssembly).Should().BeTrue();
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [InlineData("netcoreapp3.0", true)]
        [InlineData("netcoreapp3.0", false)]
        [InlineData("net5.0", false)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, false)]
        public void ILLink_runs_and_creates_linked_app(string targetFramework, bool referenceClassLibAsPackage)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName, referenceProjectName, referenceClassLibAsPackage);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework + referenceClassLibAsPackage)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            Directory.Exists(linkedDirectory).Should().BeTrue();

            var linkedDll = Path.Combine(linkedDirectory, $"{projectName}.dll");
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");
            var unusedFrameworkDll = Path.Combine(publishDirectory, $"{UnusedFrameworkAssembly}.dll");

            // Intermediate assembly is kept by linker and published, but not unused assemblies
            File.Exists(linkedDll).Should().BeTrue();
            File.Exists(publishedDll).Should().BeTrue();
            File.Exists(unusedDll).Should().BeFalse();
            File.Exists(unusedFrameworkDll).Should().BeFalse();

            var depsFile = Path.Combine(publishDirectory, $"{projectName}.deps.json");
            DoesDepsFileHaveAssembly(depsFile, projectName).Should().BeTrue();
            DoesDepsFileHaveAssembly(depsFile, referenceProjectName).Should().BeFalse();
            DoesDepsFileHaveAssembly(depsFile, UnusedFrameworkAssembly).Should().BeFalse();
        }

        //  https://github.com/dotnet/sdk/issues/49665
        //  ILLINK : Failed to load /private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/10.0.0-preview.6.25315.102/libhostpolicy.dylib, error : dlopen(/private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/10.0.0-preview.6.25315.102/libhostpolicy.dylib, 0x0001): tried: '/private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/10.0.0-preview.6.25315.102/libhostpolicy.dylib' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64')), 
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_links_simple_app_without_analysis_warnings_and_it_runs(string targetFramework)
        {
            foreach (var trimMode in new[] { "copyused", "link" })
            {
                var projectName = "HelloWorld";
                var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

                var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName);
                testProject.AdditionalProperties["PublishTrimmed"] = "true";
                var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework + trimMode);

                var publishCommand = new PublishCommand(testAsset);
                publishCommand.Execute($"/p:RuntimeIdentifier={rid}", $"/p:TrimMode={trimMode}", "/p:SuppressTrimAnalysisWarnings=true")
                    .Should().Pass()
                    .And.NotHaveStdOutContaining("warning IL2075")
                    .And.NotHaveStdOutContaining("warning IL2026");

                var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid);
                var exe = Path.Combine(publishDirectory.FullName, $"{testProject.Name}{Constants.ExeSuffix}");

                var command = new RunExeCommand(Log, exe)
                    .Execute().Should().Pass()
                    .And.HaveStdOutContaining("Hello world");

                CheckILLinkVersion(testAsset, targetFramework);
            }
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void PublishTrimmed_fails_when_no_matching_pack_is_found(string targetFramework)
        {
            var projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    project.Root.Add(new XElement(ns + "ItemGroup",
                        new XElement("KnownILLinkPack",
                            new XAttribute("Remove", "Microsoft.NET.ILLink.Tasks"))));
                });

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true")
                .Should().Fail()
                .And.HaveStdOutContaining($"error {Strings.PublishTrimmedRequiresVersion30}");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [InlineData("netcoreapp2.0", true)]
        [InlineData("netcoreapp2.1", true)]
        [InlineData("netstandard2.1", true)]
        [InlineData("netcoreapp3.0", false)]
        [InlineData("netcoreapp3.1", false)]
        [InlineData("net5.0", false)]
        [InlineData("net6.0", false)]
        public void PublishTrimmed_fails_for_unsupported_target_framework(string targetFramework, bool shouldFail)
        {
            var projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName);
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            testProject.AdditionalProperties["NoWarn"] = "NETSDK1138"; // Silence warning about targeting EOL TFMs
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);
            var publishCommand = new PublishCommand(testAsset);
            var result = publishCommand.Execute($"/p:RuntimeIdentifier={rid}");
            if (shouldFail) {
                result.Should().Fail()
                    .And.HaveStdOutContaining($"error {Strings.PublishTrimmedRequiresVersion30}");
            } else {
                result.Should().Pass()
                    .And.NotHaveStdOutContaining("warning");
            }
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [InlineData("netstandard2.0", true)]
        [InlineData("netstandard2.1", true)]
        [InlineData("netcoreapp3.1", true)]
        [InlineData("net5.0", true)]
        [InlineData("net6.0", false)]
        [InlineData("netstandard2.0;net5.0", true)] // None of these TFMs are supported for trimming
        [InlineData("netstandard2.0;net6.0", false)] // net6.0 is the min TFM supported for trimming and targeting.
        [InlineData("netstandard2.0;net8.0", false)] // Net8.0 is supported for trimming and targeting.
        [InlineData("netstandard2.0;net9.0", true)] // Net8.0 is supported for trimming, but leaves a "gap" for the supported net6.0/net7.0 TFMs.
        [InlineData("alias-ns2", true)]
        [InlineData("alias-n6", false)]
        [InlineData("alias-n6;alias-n8", false)] // If all TFMs are supported, there's no warning even though the project uses aliases.
        [InlineData("alias-ns2;alias-n6", true)] // This is correctly multi-targeted, but the logic can't detect this due to the alias so it still warns.
        public void IsTrimmable_warns_when_expected_for_not_correctly_multitargeted_libraries(string targetFrameworks, bool shouldWarn)
        {
            var projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFrameworks);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFrameworks, projectName);
            testProject.AdditionalProperties["IsTrimmable"] = "true";
            testProject.AdditionalProperties["CheckEolTargetFramework"] = "false"; // Silence warning about targeting EOL TFMs
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFrameworks)
                .WithProjectChanges(AddTargetFrameworkAliases);

            var buildCommand = new BuildCommand(testAsset);
            var resultAssertion = buildCommand.Execute("/p:CheckEolTargetFramework=false")
                .Should().Pass();
            if (shouldWarn) {
                resultAssertion
                    // Note: can't check for Strings.IsTrimmableUnsupported because each line of
                    // the message gets prefixed with a file path by MSBuild.
                    .And.HaveStdOutContaining($"warning NETSDK1212")
                    .And.HaveStdOutContaining($"<IsTrimmable Condition=\"$([MSBuild]::IsTargetFrameworkCompatible('$(TargetFramework)', 'net8.0'))\">true</IsTrimmable>");
            } else {
                resultAssertion.And.NotHaveStdOutContaining($"warning");
            }
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("netstandard2.1")]
        public void RequiresILLinkPack_errors_for_unsupported_target_framework(string targetFramework)
        {
            var projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName);
            testProject.AdditionalProperties["_RequiresILLinkPack"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should().Fail()
                .And.HaveStdOutContaining($"error {Strings.ILLinkNoValidRuntimePackageError}");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("netstandard2.0")]
        [InlineData("netstandard2.1")]
        public void ILLink_can_use_latest_with_unsupported_target_framework(string targetFramework)
        {
            var projectName = "TrimmableNetstandardLibrary";
            var testAsset = _testAssetsManager.CopyTestAsset(projectName)
                .WithSource()
                .WithTargetFramework(targetFramework);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute("/p:IsTrimmable=true")
                .Should().Pass()
                .And.HaveStdOutContaining("warning IL2026");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void PrepareForILLink_can_set_IsTrimmable(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => SetMetadata(project, referenceProjectName, "IsTrimmable", "True"));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedIsTrimmableDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");

            File.Exists(publishedDll).Should().BeTrue();
            // Check that the unused trimmable assembly was removed
            File.Exists(unusedIsTrimmableDll).Should().BeFalse();
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void PrepareForILLink_can_set_TrimMode(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName, referenceProjectName, referenceProjectIdentifier: targetFramework);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => SetMetadata(project, referenceProjectName, "TrimMode", "link"));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedTrimModeLinkDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");

            File.Exists(publishedDll).Should().BeTrue();
            // Check that the unused "link" assembly was removed.
            File.Exists(unusedTrimModeLinkDll).Should().BeFalse();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("net5.0", "link")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "copyused")]
        [InlineData("net6.0", "full")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "full")]
        [InlineData("net6.0", "partial")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "partial")]
        public void ILLink_respects_global_TrimMode(string targetFramework, string trimMode)
        {
            if ((targetFramework == "net5.0" || targetFramework == "net6.0") &&
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                //  https://github.com/dotnet/sdk/issues/49665
                return;
            }

            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName, referenceProjectName, referenceProjectIdentifier: targetFramework);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework + trimMode)
                .WithProjectChanges(project => SetGlobalTrimMode(project, trimMode))
                .WithProjectChanges(project => SetMetadata(project, referenceProjectName, "IsTrimmable", "True"))
                .WithProjectChanges(project => AddRootDescriptor(project, $"{referenceProjectName}.xml"));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var isTrimmableDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");

            File.Exists(publishedDll).Should().BeTrue();
            File.Exists(isTrimmableDll).Should().BeTrue();
            DoesImageHaveMethod(isTrimmableDll, "UnusedMethodToRoot").Should().BeTrue();
            if (trimMode is "link" or "full" or "partial")
            {
                // Check that the assembly was trimmed at the member level
                DoesImageHaveMethod(isTrimmableDll, "UnusedMethod").Should().BeFalse();
            }
            else
            {
                // Check that the assembly was trimmed at the assembxly level
                DoesImageHaveMethod(isTrimmableDll, "UnusedMethod").Should().BeTrue();
            }
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_roots_IntermediateAssembly(string targetFramework)
        {
            var projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => SetGlobalTrimMode(project, "link"))
                .WithProjectChanges(project => SetMetadata(project, projectName, "IsTrimmable", "True"));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");

            // The assembly is trimmed but its entry point is kept
            DoesImageHaveMethod(publishedDll, "UnusedMethod").Should().BeFalse();
            DoesImageHaveMethod(publishedDll, "Main").Should().BeTrue();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ILLink_respects_TrimmableAssembly(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName, referenceProjectName);
            testProject.AddItem("TrimmableAssembly", "Include", referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedTrimmableDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");

            File.Exists(publishedDll).Should().BeTrue();
            // Check that the unused assembly was removed.
            File.Exists(unusedTrimmableDll).Should().BeFalse();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData("net6.0")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ILLink_respects_IsTrimmable_attribute(string targetFramework)
        {
            if (targetFramework == "net6.0" &&
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                //  https://github.com/dotnet/sdk/issues/49665
                return;
            }

            string projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testProject = CreateTestProjectWithIsTrimmableAttributes(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var unusedTrimmableDll = Path.Combine(publishDirectory, "UnusedTrimmableAssembly.dll");
            var unusedNonTrimmableDll = Path.Combine(publishDirectory, "UnusedNonTrimmableAssembly.dll");

            // Only unused non-trimmable assemblies are kept
            File.Exists(unusedTrimmableDll).Should().BeFalse();
            if (targetFramework == "net6.0")
            {
                // In net6.0 the default is to keep assemblies not marked trimmable
                DoesImageHaveMethod(unusedNonTrimmableDll, "UnusedMethod").Should().BeTrue();
            }
            else
            {
                // In net7.0+ the default is to keep assemblies not marked trimmable
                File.Exists(unusedNonTrimmableDll).Should().BeFalse();
            }
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net6Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_IsTrimmable_metadata_can_override_attribute(string targetFramework)
        {
            string projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testProject = CreateTestProjectWithIsTrimmableAttributes(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => SetGlobalTrimMode(project, "partial"))
                .WithProjectChanges(project => SetMetadata(project, "UnusedTrimmableAssembly", "IsTrimmable", "false"))
                .WithProjectChanges(project => SetMetadata(project, "UnusedNonTrimmableAssembly", "IsTrimmable", "true"));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/v:n").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var unusedTrimmableDll = Path.Combine(publishDirectory, "UnusedTrimmableAssembly.dll");
            var unusedNonTrimmableDll = Path.Combine(publishDirectory, "UnusedNonTrimmableAssembly.dll");

            // Attributed IsTrimmable assembly with IsTrimmable=false metadata should be kept
            DoesImageHaveMethod(unusedTrimmableDll, "UnusedMethod").Should().BeTrue();
            // Unattributed assembly with IsTrimmable=true should be trimmed
            File.Exists(unusedNonTrimmableDll).Should().BeFalse();
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [InlineData("net6.0")]
        public void ILLink_TrimMode_applies_to_IsTrimmable_assemblies(string targetFramework)
        {
            string projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testProject = CreateTestProjectWithIsTrimmableAttributes(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var trimmableDll = Path.Combine(publishDirectory, "TrimmableAssembly.dll");
            var nonTrimmableDll = Path.Combine(publishDirectory, "NonTrimmableAssembly.dll");
            var unusedTrimmableDll = Path.Combine(publishDirectory, "UnusedTrimmableAssembly.dll");
            var unusedNonTrimmableDll = Path.Combine(publishDirectory, "UnusedNonTrimmableAssembly.dll");

            // Trimmable assemblies are trimmed at member level
            DoesImageHaveMethod(trimmableDll, "UnusedMethod").Should().BeFalse();
            DoesImageHaveMethod(trimmableDll, "UsedMethod").Should().BeTrue();
            File.Exists(unusedTrimmableDll).Should().BeFalse();
            // Non-trimmable assemblies still get copied
            DoesImageHaveMethod(nonTrimmableDll, "UnusedMethod").Should().BeTrue();
            DoesImageHaveMethod(unusedNonTrimmableDll, "UnusedMethod").Should().BeTrue();
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "full")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, "partial")]
        public void ILLink_TrimMode_new_options(string targetFramework, string trimMode)
        {
            string projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testProject = CreateTestProjectWithIsTrimmableAttributes(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework + trimMode)
                .WithProjectChanges(project => SetGlobalTrimMode(project, trimMode));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "-bl").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var trimmableDll = Path.Combine(publishDirectory, "TrimmableAssembly.dll");
            var nonTrimmableDll = Path.Combine(publishDirectory, "NonTrimmableAssembly.dll");
            var unusedTrimmableDll = Path.Combine(publishDirectory, "UnusedTrimmableAssembly.dll");
            var unusedNonTrimmableDll = Path.Combine(publishDirectory, "UnusedNonTrimmableAssembly.dll");

            // Trimmable assemblies are trimmed at member level
            DoesImageHaveMethod(trimmableDll, "UnusedMethod").Should().BeFalse();
            DoesImageHaveMethod(trimmableDll, "UsedMethod").Should().BeTrue();
            File.Exists(unusedTrimmableDll).Should().BeFalse();
            if (trimMode is "full")
            {
                DoesImageHaveMethod(nonTrimmableDll, "UnusedMethod").Should().BeFalse();
                File.Exists(unusedNonTrimmableDll).Should().BeFalse();
            }
            else if (trimMode is "partial")
            {
                DoesImageHaveMethod(nonTrimmableDll, "UnusedMethod").Should().BeTrue();
                DoesImageHaveMethod(unusedNonTrimmableDll, "UnusedMethod").Should().BeTrue();
            }
            else
            {
                Assert.Fail("unexpected value");
            }
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ILLink_can_set_TrimmerDefaultAction(string targetFramework)
        {
            string projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testProject = CreateTestProjectWithIsTrimmableAttributes(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => SetTrimmerDefaultAction(project, "link"));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;

            var trimmableDll = Path.Combine(publishDirectory, "TrimmableAssembly.dll");
            var nonTrimmableDll = Path.Combine(publishDirectory, "NonTrimmableAssembly.dll");
            var unusedTrimmableDll = Path.Combine(publishDirectory, "UnusedTrimmableAssembly.dll");
            var unusedNonTrimmableDll = Path.Combine(publishDirectory, "UnusedNonTrimmableAssembly.dll");

            // Trimmable assemblies are trimmed at member level
            DoesImageHaveMethod(trimmableDll, "UnusedMethod").Should().BeFalse();
            File.Exists(unusedTrimmableDll).Should().BeFalse();
            // Unattributed assemblies are trimmed at member level
            DoesImageHaveMethod(nonTrimmableDll, "UnusedMethod").Should().BeFalse();
            File.Exists(unusedNonTrimmableDll).Should().BeFalse();
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [InlineData("net5.0")]
        public void ILLink_analysis_warnings_are_disabled_by_default(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}")
                .Should().Pass()
                // trim analysis warnings are disabled
                .And.NotHaveStdOutMatching(@"warning IL\d\d\d\d");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        //  ILLINK : Failed to load /private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/7.0.0/libhostpolicy.dylib, error : dlopen(/private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/7.0.0/libhostpolicy.dylib, 0x0001): tried: '/private/tmp/helix/working/A452091E/p/d/shared/Microsoft.NETCore.App/7.0.0/libhostpolicy.dylib' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64')), 
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net6Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_analysis_warnings_are_enabled_by_default(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            // Minimal verbosity prevents desktop MSBuild.exe from duplicating the warnings in the warning summary
            var result = publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/v:m");
            result.Should().Pass();
            // trim analysis warnings are enabled
            var expectedWarnings = new List<string> {
                // ILLink warnings
                "Trim analysis warning IL2075.*Program.IL_2075",
                "Trim analysis warning IL2026.*Program.IL_2026.*Testing analysis warning IL2026",
                "Trim analysis warning IL2043.*Program.IL_2043.get",
                "Trim analysis warning IL2026.*Program.Base.IL_2046",
                "Trim analysis warning IL2046.*Program.Derived.IL_2046",
                "Trim analysis warning IL2093.*Program.Derived.IL_2093",
                // analyzer warnings
                "warning IL2075.*Type.GetMethod",
                "warning IL2026.*Program.IL_2026.*Testing analysis warning IL2026",
                "warning IL2043.*Program.IL_2043.get",
                "warning IL2026.*Program.Base.IL_2046",
                "warning IL2046.*Program.Derived.IL_2046",
                "warning IL2093.*Program.Derived.IL_2093"
            };
            ValidateWarningsOnHelloWorldApp(publishCommand, result, expectedWarnings, targetFramework, rid, useRegex: true);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_accepts_option_to_enable_analysis_warnings(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            testProject.AdditionalProperties["SuppressTrimAnalysisWarnings"] = "false";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}")
                .Should().Pass()
                .And.HaveStdOutMatching("warning IL2075.*Program.IL_2075")
                .And.HaveStdOutMatching("warning IL2026.*Program.IL_2026.*Testing analysis warning IL2026")
                .And.HaveStdOutMatching("warning IL2043.*Program.IL_2043.get")
                .And.HaveStdOutMatching("warning IL2046.*Program.Derived.IL_2046")
                .And.HaveStdOutMatching("warning IL2093.*Program.Derived.IL_2093");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_accepts_option_to_disable_analysis_warnings(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            testProject.AdditionalProperties["SuppressTrimAnalysisWarnings"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}")
                .Should().Pass()
                .And.NotHaveStdOutContaining("warning IL2075")
                .And.NotHaveStdOutContaining("warning IL2026")
                .And.NotHaveStdOutContaining("warning IL2043")
                .And.NotHaveStdOutContaining("warning IL2046")
                .And.NotHaveStdOutContaining("warning IL2093");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_accepts_option_to_enable_analysis_warnings_without_PublishTrimmed(string targetFramework)
        {
            if (targetFramework == "net5.0" &&
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                //  https://github.com/dotnet/sdk/issues/49665
                return;
            }

            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            testProject.AdditionalProperties["EnableTrimAnalyzer"] = "true";
            testProject.AdditionalProperties["EnableNETAnalyzers"] = "false";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            // Minimal verbosity prevents desktop MSBuild.exe from duplicating the warnings in the warning summary
            var result = publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/v:m");
            result.Should().Pass();
            var expectedWarnings = new List<string> {
                // analyzer warnings
                "warning IL2075.*Type.GetMethod",
                "warning IL2026.*Program.IL_2026.*Testing analysis warning IL2026",
                "warning IL2043.*Program.IL_2043.get",
                "warning IL2026.*Program.Base.IL_2046",
                "warning IL2046.*Program.Derived.IL_2046",
                "warning IL2093.*Program.Derived.IL_2093"
            };
            ValidateWarningsOnHelloWorldApp(publishCommand, result, expectedWarnings, targetFramework, rid, useRegex: true);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ILLink_shows_single_warning_for_packagereferences_only(string targetFramework)
        {
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testAssetName = "TrimmedAppWithReferences";
            var testAsset = _testAssetsManager
                .CopyTestAsset(testAssetName, identifier: targetFramework)
                .WithSource();

            var publishCommand = new PublishCommand(testAsset, "App");
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}")
                .Should().Pass()
                .And.HaveStdOutMatching("IL2026: App.Program.Main.*Program.RUC")
                .And.HaveStdOutMatching("IL2026: ProjectReference.ProjectReferenceLib.Method.*ProjectReferenceLib.RUC")
                .And.HaveStdOutMatching("IL2026: TransitiveProjectReference.TransitiveProjectReferenceLib.Method.*TransitiveProjectReferenceLib.RUC")
                .And.NotHaveStdOutMatching("IL2026:.*PackageReference.PackageReferenceLib")
                .And.HaveStdOutMatching("IL2104.*'PackageReference'")
                .And.NotHaveStdOutMatching("IL2104.*'App'")
                .And.NotHaveStdOutMatching("IL2104.*'ProjectReference'")
                .And.NotHaveStdOutMatching("IL2104.*'TransitiveProjectReference'");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ILLink_accepts_option_to_show_all_warnings(string targetFramework)
        {
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testAssetName = "TrimmedAppWithReferences";
            var testAsset = _testAssetsManager
                .CopyTestAsset(testAssetName, identifier: targetFramework)
                .WithSource();

            var publishCommand = new PublishCommand(testAsset, "App");
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:TrimmerSingleWarn=false")
                .Should().Pass()
                .And.HaveStdOutMatching("IL2026: App.Program.Main.*Program.RUC")
                .And.HaveStdOutMatching("IL2026: ProjectReference.ProjectReferenceLib.Method.*ProjectReferenceLib.RUC")
                .And.HaveStdOutMatching("IL2026: TransitiveProjectReference.TransitiveProjectReferenceLib.Method.*TransitiveProjectReferenceLib.RUC")
                .And.HaveStdOutMatching("IL2026:.*PackageReference.PackageReferenceLib")
                .And.NotHaveStdOutContaining("IL2104");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ILLink_can_show_single_warning_per_assembly(string targetFramework)
        {
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testAssetName = "TrimmedAppWithReferences";
            var testAsset = _testAssetsManager
                .CopyTestAsset(testAssetName, identifier: targetFramework)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    SetMetadata(project, "PackageReference", "TrimmerSingleWarn", "false");
                    SetMetadata(project, "ProjectReference", "TrimmerSingleWarn", "true");
                    SetMetadata(project, "App", "TrimmerSingleWarn", "true");
                });

            var publishCommand = new PublishCommand(testAsset, "App");
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:TrimmerSingleWarn=false")
                .Should().Pass()
                .And.NotHaveStdOutMatching("IL2026: App.Program.Main.*Program.RUC")
                .And.NotHaveStdOutMatching("IL2026: ProjectReference.ProjectReferenceLib.Method.*ProjectReferenceLib.RUC")
                .And.HaveStdOutMatching("IL2026: TransitiveProjectReference.TransitiveProjectReferenceLib.Method.*TransitiveProjectReferenceLib.RUC")
                .And.HaveStdOutMatching("IL2026:.*PackageReference.PackageReferenceLib")
                .And.NotHaveStdOutMatching("IL2104.*'PackageReference'")
                .And.HaveStdOutMatching("IL2104.*'App'")
                .And.HaveStdOutMatching("IL2104.*'ProjectReference'")
                .And.NotHaveStdOutMatching("IL2104.*'TransitiveProjectReference'");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_errors_fail_the_build(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            // Set up a project with an invalid feature substitution, just to produce an error.
            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName);
            testProject.SourceFiles[$"{projectName}.xml"] = $@"
<linker>
  <assembly fullname=""{projectName}"">
    <type fullname=""Program"" feature=""featuremissingvalue"" />
  </assembly>
</linker>
";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => AddRootDescriptor(project, $"{projectName}.xml"));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:SuppressTrimAnalysisWarnings=false")
                .Should().Fail()
                .And.HaveStdOutContaining("error IL1001")
                .And.HaveStdOutContaining(Strings.ILLinkFailed);

            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateLinkDir = Path.Combine(intermediateDirectory, "linked");

            var linkSemaphore = Path.Combine(intermediateLinkDir, "Link.semaphore");
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");

            File.Exists(linkSemaphore).Should().BeFalse();
            File.Exists(publishedDll).Should().BeFalse();
        }
    }

    // this test class is split up arbitrarily so Helix can run tests in multiple workitems
    public class GivenThatWeWantToRunILLink2 : SdkTest
    {
        public GivenThatWeWantToRunILLink2(ITestOutputHelper log) : base(log)
        {
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net6Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_verify_analysis_warnings_hello_world_app_trim_mode_copyused(string targetFramework)
        {
            var projectName = "AnalysisWarningsOnHelloWorldApp";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            // Please keep list below sorted and de-duplicated
            var expectedWarnings = new List<string> {
                "ILLink : Trim analysis warning IL2026: Internal.Runtime.InteropServices.ComActivator.GetClassFactoryForTypeInternal(ComActivationContextInternal*",
                "ILLink : Trim analysis warning IL2026: Internal.Runtime.InteropServices.ComponentActivator.GetFunctionPointer(IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr",
                "ILLink : Trim analysis warning IL2026: System.ComponentModel.Design.DesigntimeLicenseContextSerializer.DeserializeUsingBinaryFormatter(DesigntimeLicenseContextSerializer.StreamWrapper, String, RuntimeLicenseContext",
                "ILLink : Trim analysis warning IL2026: System.Resources.ManifestBasedResourceGroveler.CreateResourceSet(Stream, Assembly",
                "ILLink : Trim analysis warning IL2026: System.StartupHookProvider.ProcessStartupHooks(",
                "ILLink : Trim analysis warning IL2063: System.RuntimeType.GetInterface(String, Boolean",
                "ILLink : Trim analysis warning IL2065: System.Runtime.Serialization.FormatterServices.InternalGetSerializableMembers(Type",
            };
            if (Net6Plus.Any(tfm => (string)tfm[0] == targetFramework))
            {
                expectedWarnings.AddRange(new string[] {
                    "ILLink : Trim analysis warning IL2026: Internal.Runtime.InteropServices.InMemoryAssemblyLoader.LoadInMemoryAssembly(IntPtr, IntPtr",
                });
            }
            if (Net7Plus.Any(tfm => (string)tfm[0] == targetFramework))
            {
                expectedWarnings.AddRange(new string[] {
                    "ILLink : Trim analysis warning IL2026: Internal.Runtime.InteropServices.InMemoryAssemblyLoader.LoadInMemoryAssembly(IntPtr, IntPtr",
                    "ILLink : Trim analysis warning IL2026: Internal.Runtime.InteropServices.InMemoryAssemblyLoader.LoadInMemoryAssemblyInContextWhenSupported(IntPtr, IntPtr"
                });
            }
            // windows-only COM warnings
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (Net9Plus.Any(tfm => (string)tfm[0] == targetFramework))
                {
                    expectedWarnings.AddRange(new string[] {
                        "ILLink : Trim analysis warning IL2026: System.ComponentModel.TypeDescriptor.NodeFor(Object, Boolean): Using member 'System.ComponentModel.TypeDescriptor.ComObjectType.get' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. COM type descriptors are not trim-compatible.",
                        "ILLink : Trim analysis warning IL2026: System.ComponentModel.TypeDescriptor.NodeFor(Object, Boolean): Using member 'System.ComponentModel.TypeDescriptor.ComObjectType.get' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. COM type descriptors are not trim-compatible."
                    });
                }
                if (Net10Plus.Any(tfm => (string)tfm[0] == targetFramework))
                {
                    expectedWarnings.AddRange(new string[] {
                        "ILLink : Trim analysis warning IL2045: Internal.Runtime.InteropServices.ComActivator.BasicClassFactory.CreateValidatedInterfaceType(Type, Guid&, Object): Attribute 'System.Runtime.InteropServices.ClassInterfaceAttribute' is being referenced in code but the trimmer was instructed to remove all instances of this attribute. If the attribute instances are necessary make sure to either remove the trimmer attribute XML portion which removes the attribute instances, or override the removal by using the trimmer XML descriptor to keep the attribute type (which in turn keeps all of its instances).",
                        "ILLink : Trim analysis warning IL2045: Internal.Runtime.InteropServices.ComActivator.BasicClassFactory.CreateValidatedInterfaceType(Type, Guid&, Object): Attribute 'System.Runtime.InteropServices.ClassInterfaceAttribute' is being referenced in code but the trimmer was instructed to remove all instances of this attribute. If the attribute instances are necessary make sure to either remove the trimmer attribute XML portion which removes the attribute instances, or override the removal by using the trimmer XML descriptor to keep the attribute type (which in turn keeps all of its instances).",
                        "ILLink : Trim analysis warning IL2045: Internal.Runtime.InteropServices.ComActivator.BasicClassFactory.CreateValidatedInterfaceType(Type, Guid&, Object): Attribute 'System.Runtime.InteropServices.ClassInterfaceAttribute' is being referenced in code but the trimmer was instructed to remove all instances of this attribute. If the attribute instances are necessary make sure to either remove the trimmer attribute XML portion which removes the attribute instances, or override the removal by using the trimmer XML descriptor to keep the attribute type (which in turn keeps all of its instances)."
                    });
                }
            }

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            var result = publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:TrimMode=copyused", "/p:TrimmerSingleWarn=false", "/p:EnableTrimAnalyzer=false",
                // Minimal verbosity prevents desktop MSBuild.exe from duplicating the warnings in the warning summary
                "/v:m");
            result.Should().Pass();
            ValidateWarningsOnHelloWorldApp(publishCommand, result, expectedWarnings, targetFramework, rid);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net6Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_verify_analysis_warnings_framework_assemblies(string targetFramework)
        {
            var projectName = "AnalysisWarningsOnHelloWorldApp";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            // Please keep list below sorted and de-duplicated
            var expectedWarnings = new List<string> {
                "ILLink : Trim analysis warning IL2026: Internal.Runtime.InteropServices.ComponentActivator.GetFunctionPointer(IntPtr, IntPtr, IntPtr, IntPtr, IntPtr, IntPtr",
                "ILLink : Trim analysis warning IL2026: Internal.Runtime.InteropServices.InMemoryAssemblyLoader.LoadInMemoryAssembly(IntPtr, IntPtr",
                "ILLink : Trim analysis warning IL2026: System.ComponentModel.Design.DesigntimeLicenseContextSerializer.DeserializeUsingBinaryFormatter(DesigntimeLicenseContextSerializer.StreamWrapper, String, RuntimeLicenseContext",
                "ILLink : Trim analysis warning IL2026: System.ComponentModel.Design.DesigntimeLicenseContextSerializer.SerializeWithBinaryFormatter(Stream, String, DesigntimeLicenseContext",
                "ILLink : Trim analysis warning IL2026: System.Data.DataSet.System.Xml.Serialization.IXmlSerializable.GetSchema(",
                "ILLink : Trim analysis warning IL2026: System.Data.DataSet.System.Xml.Serialization.IXmlSerializable.ReadXml(XmlReader",
                "ILLink : Trim analysis warning IL2026: System.Data.DataSet.System.Xml.Serialization.IXmlSerializable.WriteXml(XmlWriter",
                "ILLink : Trim analysis warning IL2026: System.Data.DataTable.System.Xml.Serialization.IXmlSerializable.GetSchema(",
                "ILLink : Trim analysis warning IL2026: System.Data.DataTable.System.Xml.Serialization.IXmlSerializable.ReadXml(XmlReader",
                "ILLink : Trim analysis warning IL2026: System.Data.DataTable.System.Xml.Serialization.IXmlSerializable.WriteXml(XmlWriter",
                "ILLink : Trim analysis warning IL2026: System.Resources.ManifestBasedResourceGroveler.CreateResourceSet(Stream, Assembly",
                "ILLink : Trim analysis warning IL2026: System.StartupHookProvider.ProcessStartupHooks(",
            };
            if (targetFramework is "net6.0")
            {
                expectedWarnings.AddRange(new string[] {
                    "ILLink : Trim analysis warning IL2055: System.Runtime.Serialization.ClassDataContract.UnadaptedClassType.get",
                    "ILLink : Trim analysis warning IL2067: System.Runtime.Serialization.SurrogateDataContract.GetUninitializedObject(Type"
                });
            }
            if (Net7Plus.Any(tfm => (string)tfm[0] == targetFramework))
            {
                expectedWarnings.AddRange(new string[] {
                    "ILLink : Trim analysis warning IL2026: Internal.Runtime.InteropServices.ComActivator.GetClassFactoryForTypeInternal(ComActivationContextInternal*",
                    "ILLink : Trim analysis warning IL2026: Internal.Runtime.InteropServices.InMemoryAssemblyLoader.LoadInMemoryAssemblyInContextWhenSupported(IntPtr, IntPtr",
                    "ILLink : Trim analysis warning IL2026: System.Linq.Queryable: Using member 'System.Linq.EnumerableRewriter.s_seqMethods' which has 'RequiresUnreferencedCodeAttribute'",
                    "ILLink : Trim analysis warning IL2026: System.Transactions.DtcProxyShim.DtcProxyShimFactory.ConnectToProxyCore(String, Guid, Object, Boolean&, Byte[]&, ResourceManagerShim&",
                    "ILLink : Trim analysis warning IL2045: System.Runtime.InteropServices.Marshal.GenerateProgIdForType(Type",
                    "ILLink : Trim analysis warning IL2045: System.Runtime.InteropServices.Marshal.GenerateProgIdForType(Type",
                    "ILLink : Trim analysis warning IL2045: System.Runtime.InteropServices.ComAwareEventInfo.GetDataForComInvocation(EventInfo, Guid&, Int32&",
                    "ILLink : Trim analysis warning IL2045: System.Runtime.InteropServices.ComAwareEventInfo.GetDataForComInvocation(EventInfo, Guid&, Int32&",
                    "ILLink : Trim analysis warning IL2045: System.Runtime.InteropServices.ComAwareEventInfo.GetDataForComInvocation(EventInfo, Guid&, Int32&",
                    "ILLink : Trim analysis warning IL2045: System.Runtime.InteropServices.ComAwareEventInfo.GetDataForComInvocation(EventInfo, Guid&, Int32&"
                });
            }
            if (Net8Plus.Any(tfm => (string)tfm[0] == targetFramework))
            {
                expectedWarnings.AddRange(new string[] {
                    "ILLink : Trim analysis warning IL2026: System.Runtime.InteropServices: Using member 'System.Runtime.InteropServices.Marshalling.ComImportInteropInterfaceDetailsStrategy.s_attributeUsageAllowMultipleProperty' which has 'RequiresUnreferencedCodeAttribute'",
                    "ILLink : Trim analysis warning IL2026: System.Runtime.InteropServices: Using member 'System.Runtime.InteropServices.Marshalling.ComImportInteropInterfaceDetailsStrategy.s_attributeUsageCtor' which has 'RequiresUnreferencedCodeAttribute'",
                    "ILLink : Trim analysis warning IL2026: System.Runtime.InteropServices: Using member 'System.Runtime.InteropServices.Marshalling.ComImportInteropInterfaceDetailsStrategy.s_attributeBaseClassCtor' which has 'RequiresUnreferencedCodeAttribute'",
                    "ILLink : Trim analysis warning IL2026: System.Runtime.InteropServices: Using member 'System.Runtime.InteropServices.Marshalling.ComImportInteropInterfaceDetailsStrategy.Instance' which has 'RequiresUnreferencedCodeAttribute'",
                    "ILLink : Trim analysis warning IL2045: System.Runtime.InteropServices.ComAwareEventInfo.GetDataForComInvocation(EventInfo, Guid&, Int32&",
                    "ILLink : Trim analysis warning IL2112: System.Data.Common.DbConnectionStringBuilder.System.ComponentModel.ICustomTypeDescriptor.GetConverter(",
                    "ILLink : Trim analysis warning IL2112: System.Data.Common.DbConnectionStringBuilder.System.ComponentModel.ICustomTypeDescriptor.GetDefaultEvent(",
                    "ILLink : Trim analysis warning IL2112: System.Data.Common.DbConnectionStringBuilder.System.ComponentModel.ICustomTypeDescriptor.GetDefaultProperty(",
                    "ILLink : Trim analysis warning IL2112: System.Data.Common.DbConnectionStringBuilder.System.ComponentModel.ICustomTypeDescriptor.GetEditor(Type",
                    "ILLink : Trim analysis warning IL2112: System.Data.Common.DbConnectionStringBuilder.System.ComponentModel.ICustomTypeDescriptor.GetEvents(Attribute[]",
                    "ILLink : Trim analysis warning IL2112: System.Data.Common.DbConnectionStringBuilder.System.ComponentModel.ICustomTypeDescriptor.GetProperties(Attribute[]",
                    "ILLink : Trim analysis warning IL2112: System.Data.Common.DbConnectionStringBuilder.System.ComponentModel.ICustomTypeDescriptor.GetProperties("
                });
            }
            if (Net9Plus.Any(tfm => (string)tfm[0] == targetFramework))
            {
                expectedWarnings.AddRange(new string[] {
                    "ILLink : Trim analysis warning IL2026: System.ComponentModel.TypeDescriptor.NodeFor(Object, Boolean): Using member 'System.ComponentModel.TypeDescriptor.ComObjectType.get' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. COM type descriptors are not trim-compatible.",
                    "ILLink : Trim analysis warning IL2026: System.ComponentModel.TypeDescriptor.NodeFor(Object, Boolean): Using member 'System.ComponentModel.TypeDescriptor.ComObjectType.get' which has 'RequiresUnreferencedCodeAttribute' can break functionality when trimming application code. COM type descriptors are not trim-compatible.",
                    "ILLink : Trim analysis warning IL2026: System.ComponentModel.AmbientValueAttribute.AmbientValueAttribute(Type, String): Using member",
                    "ILLink : Trim analysis warning IL2026: System.ComponentModel.DefaultValueAttribute.DefaultValueAttribute(Type, String): Using member"
                });
            }
            if (Net10Plus.Any(tfm => (string)tfm[0] == targetFramework))
            {
                expectedWarnings.AddRange(new string[] {
                    "ILLink : Trim analysis warning IL2067: System.ComponentModel.DefaultValueAttribute.DefaultValueAttribute(Type, String): 'typeToConvert' argument does not satisfy 'DynamicallyAccessedMemberTypes.All' in call to 'System.ComponentModel.DefaultValueAttribute"
                });
                // windows-only COM warnings
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    expectedWarnings.AddRange(new string[] {
                        "ILLink : Trim analysis warning IL2045: Internal.Runtime.InteropServices.ComActivator.BasicClassFactory.CreateValidatedInterfaceType(Type, Guid&, Object): Attribute 'System.Runtime.InteropServices.ClassInterfaceAttribute' is being referenced in code but the trimmer was instructed to remove all instances of this attribute. If the attribute instances are necessary make sure to either remove the trimmer attribute XML portion which removes the attribute instances, or override the removal by using the trimmer XML descriptor to keep the attribute type (which in turn keeps all of its instances).",
                        "ILLink : Trim analysis warning IL2045: Internal.Runtime.InteropServices.ComActivator.BasicClassFactory.CreateValidatedInterfaceType(Type, Guid&, Object): Attribute 'System.Runtime.InteropServices.ClassInterfaceAttribute' is being referenced in code but the trimmer was instructed to remove all instances of this attribute. If the attribute instances are necessary make sure to either remove the trimmer attribute XML portion which removes the attribute instances, or override the removal by using the trimmer XML descriptor to keep the attribute type (which in turn keeps all of its instances).",
                        "ILLink : Trim analysis warning IL2045: Internal.Runtime.InteropServices.ComActivator.BasicClassFactory.CreateValidatedInterfaceType(Type, Guid&, Object): Attribute 'System.Runtime.InteropServices.ClassInterfaceAttribute' is being referenced in code but the trimmer was instructed to remove all instances of this attribute. If the attribute instances are necessary make sure to either remove the trimmer attribute XML portion which removes the attribute instances, or override the removal by using the trimmer XML descriptor to keep the attribute type (which in turn keeps all of its instances)."
                    });
                }
            }

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            var result = publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true",
                "/p:TrimMode=copy", "/p:_TrimmerDefaultAction=copy", "/p:TrimmerSingleWarn=false", "/p:EnableTrimAnalyzer=false",
                // Minimal verbosity prevents desktop MSBuild.exe from duplicating the warnings in the warning summary
                "/v:m");
            result.Should().Pass();
            ValidateWarningsOnHelloWorldApp(publishCommand, result, expectedWarnings, targetFramework, rid);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net6Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_verify_analysis_warnings_hello_world_app_trim_mode_link(string targetFramework)
        {
            var projectName = "AnalysisWarningsOnHelloWorldApp";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            var result = publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:TrimmerSingleWarn=false");
            result.Should().Pass();
            ValidateWarningsOnHelloWorldApp(publishCommand, result, new List<string>(), targetFramework, rid);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificFact(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void ILLink_verify_analysis_warnings_hello_world_app_trim_mode_link_5_0()
        {
            string targetFramework = "net5.0";
            var projectName = "AnalysisWarningsOnHelloWorldApp";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            var result = publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:TrimmerSingleWarn=false",
                "/p:TrimMode=link");
            result.Should().Pass();
            ValidateWarningsOnHelloWorldApp(publishCommand, result, new List<string>(), targetFramework, rid);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void TrimmingOptions_are_defaulted_correctly_on_trimmed_apps(string targetFramework)
        {
            var projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: projectName + targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true")
                .Should().Pass();

            string outputDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            string runtimeConfigFile = Path.Combine(outputDirectory, $"{projectName}.runtimeconfig.json");
            string runtimeConfigContents = File.ReadAllText(runtimeConfigFile);


            if (Version.TryParse(targetFramework.TrimStart("net".ToCharArray()), out Version parsedVersion) &&
                parsedVersion.Major >= 6)
            {
                JObject runtimeConfig = JObject.Parse(runtimeConfigContents);
                JToken configProperties = runtimeConfig["runtimeOptions"]["configProperties"];
                configProperties["Microsoft.Extensions.DependencyInjection.VerifyOpenGenericServiceTrimmability"].Value<bool>()
                    .Should().BeTrue();
                configProperties["System.ComponentModel.TypeConverter.EnableUnsafeBinaryFormatterInDesigntimeLicenseContextSerialization"].Value<bool>()
                    .Should().BeFalse();
                configProperties["System.Resources.ResourceManager.AllowCustomResourceTypes"].Value<bool>()
                    .Should().BeFalse();
                configProperties["System.Runtime.InteropServices.BuiltInComInterop.IsSupported"].Value<bool>()
                    .Should().BeFalse();
                configProperties["System.Runtime.InteropServices.EnableConsumingManagedCodeFromNativeHosting"].Value<bool>()
                    .Should().BeFalse();
                configProperties["System.Runtime.InteropServices.EnableCppCLIHostActivation"].Value<bool>()
                    .Should().BeFalse();
                configProperties["System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization"].Value<bool>()
                    .Should().BeFalse();
                configProperties["System.StartupHookProvider.IsSupported"].Value<bool>()
                    .Should().BeFalse();
                configProperties["System.Text.Encoding.EnableUnsafeUTF7Encoding"].Value<bool>()
                    .Should().BeFalse();
                configProperties["System.Threading.Thread.EnableAutoreleasePool"].Value<bool>()
                    .Should().BeFalse();

                if (parsedVersion.Major >= 8)
                {
                    configProperties["System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault"].Value<bool>()
                        .Should().BeFalse();
                }

                if (parsedVersion.Major >= 9)
                {
                    configProperties["System.ComponentModel.TypeDescriptor.IsComObjectDescriptorSupported"].Value<bool>()
                        .Should().BeFalse();
                }
            }
            else
            {
                runtimeConfigContents.Should().NotContain("Microsoft.Extensions.DependencyInjection.VerifyOpenGenericServiceTrimmability");
                runtimeConfigContents.Should().NotContain("System.ComponentModel.TypeConverter.EnableUnsafeBinaryFormatterInDesigntimeLicenseContextSerialization");
                runtimeConfigContents.Should().NotContain("System.Resources.ResourceManager.AllowCustomResourceTypes");
                runtimeConfigContents.Should().NotContain("System.Runtime.InteropServices.BuiltInComInterop.IsSupported");
                runtimeConfigContents.Should().NotContain("System.Runtime.InteropServices.EnableConsumingManagedCodeFromNativeHosting");
                runtimeConfigContents.Should().NotContain("System.Runtime.InteropServices.EnableCppCLIHostActivation");
                runtimeConfigContents.Should().NotContain("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization");
                runtimeConfigContents.Should().NotContain("System.StartupHookProvider.IsSupported");
                runtimeConfigContents.Should().NotContain("System.Text.Encoding.EnableUnsafeUTF7Encoding");
                runtimeConfigContents.Should().NotContain("System.Threading.Thread.EnableAutoreleasePool");
            }
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_accepts_root_descriptor(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project))
                .WithProjectChanges(project => AddRootDescriptor(project, $"{referenceProjectName}.xml"));

            var publishCommand = new PublishCommand(testAsset);
            // Inject extra arguments to prevent the linker from
            // keeping the entire referenceProject assembly. The
            // linker by default runs in a conservative mode that
            // keeps all used assemblies, but in this case we want to
            // check whether the root descriptor actually roots only
            // the specified method.
            var extraArgs = $"--action link {referenceProjectName}";
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true",
                                   $"/p:_ExtraTrimmerArgs={extraArgs}", "/v:n").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");

            // With root descriptor, linker keeps specified roots but removes unused methods
            File.Exists(publishedDll).Should().BeTrue();
            File.Exists(unusedDll).Should().BeTrue();
            DoesImageHaveMethod(unusedDll, "UnusedMethod").Should().BeFalse();
            DoesImageHaveMethod(unusedDll, "UnusedMethodToRoot").Should().BeTrue();
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [InlineData("_TrimmerBeforeFieldInit")]
        [InlineData("_TrimmerOverrideRemoval")]
        [InlineData("_TrimmerUnreachableBodies")]
        [InlineData("_TrimmerUnusedInterfaces")]
        [InlineData("_TrimmerIPConstProp")]
        [InlineData("_TrimmerSealer")]
        public void ILLink_error_on_nonboolean_optimization_flag(string property)
        {
            var projectName = "HelloWorld";
            var targetFramework = "net5.0";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: property);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", $"/p:{property}=NonBool")
                .Should().Fail().And.HaveStdOutContaining("MSB4030");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificFact(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void ILLink_respects_feature_settings_from_host_config()
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var targetFramework = "net5.0";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName, referenceProjectName,
                // Reference the classlib to ensure its XML is processed.
                addAssemblyReference: true,
                // Set up a conditional feature substitution for the "FeatureDisabled" property
                modifyReferencedProject: (referencedProject) => AddFeatureDefinition(referencedProject, referenceProjectName));
            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project))
                // Set a matching RuntimeHostConfigurationOption, with Trim = "true"
                .WithProjectChanges(project => AddRuntimeConfigOption(project, trim: true))
                .WithProjectChanges(project => AddRootDescriptor(project, $"{referenceProjectName}.xml"));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true",
                                    $"/p:_ExtraTrimmerArgs=--action link {referenceProjectName}").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var referenceDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");

            File.Exists(referenceDll).Should().BeTrue();
            DoesImageHaveMethod(referenceDll, "FeatureAPI").Should().BeTrue();
            DoesImageHaveMethod(referenceDll, "get_FeatureDisabled").Should().BeFalse();
            // Check that this method is removed when the feature is disabled
            DoesImageHaveMethod(referenceDll, "FeatureImplementation").Should().BeFalse();
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificFact(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void ILLink_ignores_host_config_settings_with_link_false()
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var targetFramework = "net5.0";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName, referenceProjectName,
                // Reference the classlib to ensure its XML is processed.
                addAssemblyReference: true,
                // Set up a conditional feature substitution for the "FeatureDisabled" property
                modifyReferencedProject: (referencedProject) => AddFeatureDefinition(referencedProject, referenceProjectName));
            var testAsset = _testAssetsManager.CreateTestProject(testProject)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project))
                // Set a matching RuntimeHostConfigurationOption, with Trim = "false"
                .WithProjectChanges(project => AddRuntimeConfigOption(project, trim: false))
                .WithProjectChanges(project => AddRootDescriptor(project, $"{referenceProjectName}.xml"));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true",
                                    $"/p:_ExtraTrimmerArgs=--action link {referenceProjectName}").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var referenceDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");

            File.Exists(referenceDll).Should().BeTrue();
            DoesImageHaveMethod(referenceDll, "FeatureAPI").Should().BeTrue();
            DoesImageHaveMethod(referenceDll, "get_FeatureDisabled").Should().BeTrue();
            // Check that the feature substitution did not apply
            DoesImageHaveMethod(referenceDll, "FeatureImplementation").Should().BeTrue();
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_runs_incrementally(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);

            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateLinkDir = Path.Combine(intermediateDirectory, "linked");

            var linkSemaphore = Path.Combine(intermediateLinkDir, "Link.semaphore");

            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();
            DateTime semaphoreFirstModifiedTime = File.GetLastWriteTimeUtc(linkSemaphore);

            WaitForUtcNowToAdvance();

            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();
            DateTime semaphoreSecondModifiedTime = File.GetLastWriteTimeUtc(linkSemaphore);

            semaphoreFirstModifiedTime.Should().Be(semaphoreSecondModifiedTime);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [InlineData("netcoreapp3.1")]
        [InlineData("net5.0")]
        [InlineData("net6.0")]
        public void ILLink_old_defaults_keep_nonframework(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute("/v:n", $"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            Directory.Exists(linkedDirectory).Should().BeTrue();

            var linkedDll = Path.Combine(linkedDirectory, $"{projectName}.dll");
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");
            var unusedFrameworkDll = Path.Combine(publishDirectory, $"{UnusedFrameworkAssembly}.dll");

            File.Exists(linkedDll).Should().BeTrue();
            File.Exists(publishedDll).Should().BeTrue();
            File.Exists(unusedDll).Should().BeTrue();
            File.Exists(unusedFrameworkDll).Should().BeFalse();

            var depsFile = Path.Combine(publishDirectory, $"{projectName}.deps.json");
            DoesDepsFileHaveAssembly(depsFile, projectName).Should().BeTrue();
            DoesDepsFileHaveAssembly(depsFile, referenceProjectName).Should().BeTrue();
            DoesDepsFileHaveAssembly(depsFile, UnusedFrameworkAssembly).Should().BeFalse();
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificFact(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void ILLink_net7_defaults_trim_nonframework()
        {
            string targetFramework = "net7.0";
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute("/v:n", $"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            Directory.Exists(linkedDirectory).Should().BeTrue();

            var linkedDll = Path.Combine(linkedDirectory, $"{projectName}.dll");
            var publishedDll = Path.Combine(publishDirectory, $"{projectName}.dll");
            var unusedDll = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");
            var unusedFrameworkDll = Path.Combine(publishDirectory, $"{UnusedFrameworkAssembly}.dll");

            File.Exists(linkedDll).Should().BeTrue();
            File.Exists(publishedDll).Should().BeTrue();
            File.Exists(unusedDll).Should().BeFalse();
            File.Exists(unusedFrameworkDll).Should().BeFalse();

            var depsFile = Path.Combine(publishDirectory, $"{projectName}.deps.json");
            DoesDepsFileHaveAssembly(depsFile, projectName).Should().BeTrue();
            DoesDepsFileHaveAssembly(depsFile, referenceProjectName).Should().BeFalse();
            DoesDepsFileHaveAssembly(depsFile, UnusedFrameworkAssembly).Should().BeFalse();
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_does_not_include_leftover_artifacts_on_second_run(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName, referenceProjectName, referenceProjectIdentifier: targetFramework);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project))
                .WithProjectChanges(project => AddRootDescriptor(project, $"{referenceProjectName}.xml"));

            var publishCommand = new PublishCommand(testAsset);

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateLinkDir = Path.Combine(intermediateDirectory, "linked");
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            var linkSemaphore = Path.Combine(intermediateLinkDir, "Link.semaphore");

            // Link, keeping classlib
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();
            DateTime semaphoreFirstModifiedTime = File.GetLastWriteTimeUtc(linkSemaphore);

            var publishedDllKeptFirstTimeOnly = Path.Combine(publishDirectory, $"{referenceProjectName}.dll");
            var linkedDllKeptFirstTimeOnly = Path.Combine(linkedDirectory, $"{referenceProjectName}.dll");
            File.Exists(linkedDllKeptFirstTimeOnly).Should().BeTrue();
            File.Exists(publishedDllKeptFirstTimeOnly).Should().BeTrue();

            // Delete kept dll from publish output (works around lack of incremental publish)
            File.Delete(publishedDllKeptFirstTimeOnly);

            // Remove root descriptor to change the linker behavior.
            WaitForUtcNowToAdvance();
            // File.SetLastWriteTimeUtc(Path.Combine(testAsset.TestRoot, testProject.Name, $"{projectName}.cs"), DateTime.UtcNow);
            testAsset = testAsset.WithProjectChanges(project => RemoveRootDescriptor(project));

            // Link, discarding classlib
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();
            DateTime semaphoreSecondModifiedTime = File.GetLastWriteTimeUtc(linkSemaphore);

            // Check that the linker actually ran again
            semaphoreFirstModifiedTime.Should().NotBe(semaphoreSecondModifiedTime);

            File.Exists(linkedDllKeptFirstTimeOnly).Should().BeFalse();
            File.Exists(publishedDllKeptFirstTimeOnly).Should().BeFalse();

            // "linked" intermediate directory does not pollute the publish output
            Directory.Exists(Path.Combine(publishDirectory, "linked")).Should().BeFalse();
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_keeps_symbols_by_default(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            var intermediatePdb = Path.Combine(intermediateDirectory, $"{projectName}.pdb");
            var linkedPdb = Path.Combine(linkedDirectory, $"{projectName}.pdb");
            var publishedPdb = Path.Combine(publishDirectory, $"{projectName}.pdb");

            File.Exists(linkedPdb).Should().BeTrue();

            var intermediatePdbSize = new FileInfo(intermediatePdb).Length;
            var linkedPdbSize = new FileInfo(linkedPdb).Length;
            var publishPdbSize = new FileInfo(publishedPdb).Length;

            if (targetFramework != "net10.0")
            {
                // Check disabled for net10.0 due to https://github.com/dotnet/sdk/issues/48633
                linkedPdbSize.Should().BeLessThanOrEqualTo(intermediatePdbSize);
            }
            publishPdbSize.Should().Be(linkedPdbSize);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_removes_symbols_when_debugger_support_is_disabled(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:DebuggerSupport=false").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            var intermediatePdb = Path.Combine(intermediateDirectory, $"{projectName}.pdb");
            var linkedPdb = Path.Combine(linkedDirectory, $"{projectName}.pdb");
            var publishedPdb = Path.Combine(publishDirectory, $"{projectName}.pdb");

            File.Exists(intermediatePdb).Should().BeTrue();
            File.Exists(linkedPdb).Should().BeFalse();
            File.Exists(publishedPdb).Should().BeFalse();
        }
    }

    // this test class is split up arbitrarily so Helix can run tests in multiple workitems
    public class GivenThatWeWantToRunILLink3 : SdkTest
    {
        public GivenThatWeWantToRunILLink3(ITestOutputHelper log) : base(log)
        {
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_accepts_option_to_remove_symbols(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:TrimmerRemoveSymbols=true").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            var intermediatePdb = Path.Combine(intermediateDirectory, $"{projectName}.pdb");
            var linkedPdb = Path.Combine(linkedDirectory, $"{projectName}.pdb");
            var publishedPdb = Path.Combine(publishDirectory, $"{projectName}.pdb");

            File.Exists(intermediatePdb).Should().BeTrue();
            File.Exists(linkedPdb).Should().BeFalse();
            File.Exists(publishedPdb).Should().BeFalse();
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_symbols_option_can_override_defaults_from_debugger_support(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework)
                .WithProjectChanges(project => EnableNonFrameworkTrimming(project));

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true",
                                    "/p:DebuggerSupport=false", "/p:TrimmerRemoveSymbols=false").Should().Pass();

            var publishDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var intermediateDirectory = publishCommand.GetIntermediateDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            var linkedDirectory = Path.Combine(intermediateDirectory, "linked");

            var intermediatePdb = Path.Combine(intermediateDirectory, $"{projectName}.pdb");
            var linkedPdb = Path.Combine(linkedDirectory, $"{projectName}.pdb");
            var publishedPdb = Path.Combine(publishDirectory, $"{projectName}.pdb");

            File.Exists(linkedPdb).Should().BeTrue();

            var intermediatePdbSize = new FileInfo(intermediatePdb).Length;
            var linkedPdbSize = new FileInfo(linkedPdb).Length;
            var publishPdbSize = new FileInfo(publishedPdb).Length;

            if (targetFramework != "net10.0")
            {
                // Check disabled for net10.0 due to https://github.com/dotnet/sdk/issues/48633
                linkedPdbSize.Should().BeLessThanOrEqualTo(intermediatePdbSize);
            }
            publishPdbSize.Should().Be(linkedPdbSize);
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_can_treat_warnings_as_errors(string targetFramework)
        {
            if (targetFramework == "net5.0" &&
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                //  https://github.com/dotnet/sdk/issues/49665
                return;
            }

            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:SuppressTrimAnalysisWarnings=false",
                                    "/p:WarningsAsErrors=IL2075")
                .Should().Fail()
                .And.HaveStdOutContaining("error IL2075")
                .And.HaveStdOutContaining("warning IL2026");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_can_treat_warnings_not_as_errors(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            testProject.AdditionalProperties["WarningsNotAsErrors"] += "IL2026;IL2046;IL2075";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:SuppressTrimAnalysisWarnings=false",
                                    "/p:TreatWarningsAsErrors=true", "/p:EnableTrimAnalyzer=false")
                .Should().Fail()
                .And.HaveStdOutContaining("warning IL2026")
                .And.HaveStdOutContaining("warning IL2046")
                .And.HaveStdOutContaining("warning IL2075")
                .And.HaveStdOutContaining("error IL2043")
                .And.NotHaveStdOutContaining("error IL2026")
                .And.NotHaveStdOutContaining("error IL2046")
                .And.NotHaveStdOutContaining("error IL2075")
                .And.NotHaveStdOutContaining("warning IL2043");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_can_ignore_warnings(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:SuppressTrimAnalysisWarnings=false",
                                    "/p:NoWarn=IL2075", "/p:WarnAsError=IL2075")
                .Should().Pass()
                .And.NotHaveStdOutContaining("warning IL2075")
                .And.NotHaveStdOutContaining("error IL2075")
                .And.HaveStdOutContaining("warning IL2026");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_respects_analysis_level(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:SuppressTrimAnalysisWarnings=false",
                                    "/p:AnalysisLevel=0.0")
                .Should().Pass().And.NotHaveStdOutMatching(@"warning IL\d\d\d\d");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_respects_warning_level_independently(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:PublishTrimmed=true", "/p:SuppressTrimAnalysisWarnings=false",
                                    // This tests the linker only. The analyzer doesn't respect ILLinkWarningLevel.
                                    "/p:EnableTrimAnalyzer=false",
                                    "/p:ILLinkWarningLevel=0")
                .Should().Pass()
                .And.NotHaveStdOutContaining("warning IL2075");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net5Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_can_treat_warnings_as_errors_independently(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}", "/p:SelfContained=true", "/p:PublishTrimmed=true", "/p:SuppressTrimAnalysisWarnings=false",
                                    "/p:TreatWarningsAsErrors=true", "/p:ILLinkTreatWarningsAsErrors=false", "/p:EnableTrimAnalyzer=false")
                .Should().Pass()
                .And.HaveStdOutContaining("warning IL2026")
                .And.HaveStdOutContaining("warning IL2046")
                .And.HaveStdOutContaining("warning IL2075")
                .And.NotHaveStdOutContaining("error IL2026")
                .And.NotHaveStdOutContaining("error IL2046")
                .And.NotHaveStdOutContaining("error IL2075");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_error_on_portable_app(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName, referenceProjectName, setSelfContained: false);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute("/p:PublishTrimmed=true")
                .Should().Fail()
                .And.HaveStdOutContaining(Strings.ILLinkNotSupportedError);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [InlineData("net5.0")]
        [InlineData("netcoreapp3.1")]
        public void ILLink_displays_informational_warning_up_to_net5_by_default(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute("/p:PublishTrimmed=true", $"/p:RuntimeIdentifier={rid}")
                .Should().Pass().And.HaveStdOutContainingIgnoreCase("https://aka.ms/dotnet-illink");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net6Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_displays_informational_warning_when_trim_analysis_warnings_are_suppressed_on_net6plus(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute("/p:PublishTrimmed=true", $"/p:RuntimeIdentifier={rid}", "/p:SuppressTrimAnalysisWarnings=true")
                .Should().Pass().And.HaveStdOutContainingIgnoreCase("https://aka.ms/dotnet-illink")
                .And.HaveStdOutContainingIgnoreCase("This process might take a while");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(Net6Plus), MemberType = typeof(PublishTestUtils))]
        public void ILLink_dont_display_informational_warning_by_default_on_net6plus(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute("/p:PublishTrimmed=true", $"/p:RuntimeIdentifier={rid}")
                .Should().Pass().And.NotHaveStdErrContaining("https://aka.ms/dotnet-illink")
                .And.HaveStdOutContainingIgnoreCase("This process might take a while");
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificTheory(TestPlatforms.Any & ~TestPlatforms.OSX)]
        [MemberData(nameof(SupportedTfms), MemberType = typeof(PublishTestUtils))]
        public void ILLink_dont_display_time_awareness_message_on_incremental_build(string targetFramework)
        {
            var projectName = "HelloWorld";
            var referenceProjectName = "ClassLibForILLink";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(_testAssetsManager, targetFramework, projectName, referenceProjectName);
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute("/p:PublishTrimmed=true", $"/p:RuntimeIdentifier={rid}")
                .Should().Pass().And.HaveStdOutContainingIgnoreCase("This process might take a while");

            publishCommand.Execute("/p:PublishTrimmed=true", $"/p:RuntimeIdentifier={rid}")
                .Should().Pass().And.NotHaveStdErrContaining("This process might take a while");
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework, true)]
        [InlineData(ToolsetInfo.CurrentTargetFramework, false)]
        public void Build_respects_IsTrimmable_property(string targetFramework, bool isExe)
        {
            var projectName = "AnalysisWarnings";

            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName, isExe);
            testProject.AdditionalProperties["IsTrimmable"] = "true";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            testProject.AdditionalProperties["RuntimeIdentifier"] = rid;
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework + isExe);

            var buildCommand = new BuildCommand(testAsset);
            // IsTrimmable enables analysis warnings during build
            buildCommand.Execute()
                .Should().Pass()
                .And.HaveStdOutMatching("warning IL2026.*Program.IL_2026.*Testing analysis warning IL2026");

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: rid).FullName;
            var assemblyPath = Path.Combine(outputDirectory, $"{projectName}.dll");
            var runtimeConfigPath = Path.Combine(outputDirectory, $"{projectName}.runtimeconfig.json");

            // injects the IsTrimmable attribute
            AssemblyInfo.Get(assemblyPath)["AssemblyMetadataAttribute"].Should().Be("IsTrimmable:True");

            // just setting IsTrimmable doesn't enable feature settings
            // (these only affect apps, and wouldn't make sense for libraries either)
            if (isExe)
            {
                JObject runtimeConfig = JObject.Parse(File.ReadAllText(runtimeConfigPath));
                JToken configProperties = runtimeConfig["runtimeOptions"]["configProperties"];
                if (configProperties != null)
                    configProperties["System.StartupHookProvider.IsSupported"].Should().BeNull();
            }
        }

        [RequiresMSBuildVersionTheory("17.0.0.32901")]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void Build_respects_PublishTrimmed_property(string targetFramework)
        {
            var projectName = "AnalysisWarnings";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testProject = CreateTestProjectWithAnalysisWarnings(targetFramework, projectName);
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var buildCommand = new BuildCommand(testAsset);
            // PublishTrimmed enables analysis warnings during build
            buildCommand.Execute($"-p:RuntimeIdentifier={rid}")
                .Should().Pass()
                .And.HaveStdOutMatching("warning IL2026.*Program.IL_2026.*Testing analysis warning IL2026");

            var outputDirectory = buildCommand.GetOutputDirectory(targetFramework, runtimeIdentifier: rid).FullName;
            var assemblyPath = Path.Combine(outputDirectory, $"{projectName}.dll");
            var runtimeConfigPath = Path.Combine(outputDirectory, $"{projectName}.runtimeconfig.json");

            // runtimeconfig has trim settings
            JObject runtimeConfig = JObject.Parse(File.ReadAllText(runtimeConfigPath));
            JToken configProperties = runtimeConfig["runtimeOptions"]["configProperties"];

            configProperties["System.StartupHookProvider.IsSupported"].Value<bool>().Should().BeFalse();

            // Build with PublishTrimmed enabled should disable System.Text.Json reflection
            configProperties["System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault"].Value<bool>().Should().BeFalse();

            // just setting PublishTrimmed doesn't inject the IsTrimmable attribute
            AssemblyInfo.Get(assemblyPath).ContainsKey("AssemblyMetadataAttribute").Should().BeFalse();
        }
    }

    internal static class ILLinkTestUtils
    {
        public static string UnusedFrameworkAssembly = "System.IO";

        public static bool DoesImageHaveMethod(string path, string methodNameToCheck)
        {
            using (FileStream fs = new(path, FileMode.Open, FileAccess.Read))
            using (var peReader = new PEReader(fs))
            {
                var metadataReader = peReader.GetMetadataReader();
                foreach (var handle in metadataReader.MethodDefinitions)
                {
                    var methodDefinition = metadataReader.GetMethodDefinition(handle);
                    string methodName = metadataReader.GetString(methodDefinition.Name);
                    if (methodName == methodNameToCheck)
                        return true;
                }
            }
            return false;
        }

        public static bool DoesDepsFileHaveAssembly(string depsFilePath, string assemblyName)
        {
            DependencyContext dependencyContext;
            using (var fs = File.OpenRead(depsFilePath))
            {
                dependencyContext = new DependencyContextJsonReader().Read(fs);
            }

            return dependencyContext.RuntimeLibraries.Any(l =>
                l.RuntimeAssemblyGroups.Any(rag =>
                    rag.AssetPaths.Any(f =>
                        Path.GetFileName(f) == $"{assemblyName}.dll")));
        }

        public static void AddRootDescriptor(XDocument project, string rootDescriptorFileName)
        {
            var ns = project.Root.Name.Namespace;

            var itemGroup = new XElement(ns + "ItemGroup");
            project.Root.Add(itemGroup);
            itemGroup.Add(new XElement(ns + "TrimmerRootDescriptor",
                                       new XAttribute("Include", rootDescriptorFileName)));
        }

        public static void RemoveRootDescriptor(XDocument project)
        {
            var ns = project.Root.Name.Namespace;

            project.Root.Elements(ns + "ItemGroup")
                .Where(ig => ig.Elements(ns + "TrimmerRootDescriptor").Any())
                .First().Remove();
        }

        public static void SetMetadata(XDocument project, string assemblyName, string key, string value)
        {
            var ns = project.Root.Name.Namespace;
            var targetName = "SetTrimmerMetadata";
            var target = project.Root.Elements(ns + "Target")
                .Where(e => e.Attribute("Name")?.Value == targetName)
                .FirstOrDefault();

            if (target == null)
            {
                target = new XElement(ns + "Target",
                    new XAttribute("BeforeTargets", "PrepareForILLink"),
                    new XAttribute("Name", targetName));
                project.Root.Add(target);
            }

            target.Add(new XElement(ns + "ItemGroup",
                new XElement("ManagedAssemblyToLink",
                    new XAttribute("Condition", $"'%(FileName)' == '{assemblyName}'"),
                    new XAttribute(key, value))));
        }

        public static void SetGlobalTrimMode(XDocument project, string trimMode)
        {
            var ns = project.Root.Name.Namespace;

            var properties = new XElement(ns + "PropertyGroup");
            project.Root.Add(properties);
            properties.Add(new XElement(ns + "TrimMode",
                                        trimMode));
        }

        public static void SetTrimmerDefaultAction(XDocument project, string action)
        {
            var ns = project.Root.Name.Namespace;

            var properties = new XElement(ns + "PropertyGroup");
            project.Root.Add(properties);
            properties.Add(new XElement(ns + "TrimmerDefaultAction", action));
        }

        public static void EnableNonFrameworkTrimming(XDocument project)
        {
            // Used to override the default linker options for testing
            // purposes. The default roots non-framework assemblies,
            // but we want to ensure that the linker is running
            // end-to-end by checking that it strips code from our
            // test projects.
            SetGlobalTrimMode(project, "link");
            var ns = project.Root.Name.Namespace;

            var target = new XElement(ns + "Target",
                                      new XAttribute("BeforeTargets", "PrepareForILLink"),
                                      new XAttribute("Name", "_EnableNonFrameworkTrimming"));
            project.Root.Add(target);
            var items = new XElement(ns + "ItemGroup");
            target.Add(items);
            items.Add(new XElement("ManagedAssemblyToLink",
                                   new XElement("Condition", "true"),
                                   new XElement("IsTrimmable", "true")));
            items.Add(new XElement(ns + "TrimmerRootAssembly",
                                   new XAttribute("Include", "@(IntermediateAssembly->'%(FullPath)')")));
        }

        public static void AddFeatureDefinition(TestProject testProject, string assemblyName)
        {
            const string substitutionsFilename = "ILLink.Substitutions.xml";

            // Add a feature definition that replaces the FeatureDisabled property when DisableFeature is true.
            testProject.EmbeddedResources[substitutionsFilename] = $@"
<linker>
  <assembly fullname=""{assemblyName}"" feature=""DisableFeature"" featurevalue=""true"">
    <type fullname=""ClassLib"">
      <method signature=""System.Boolean get_FeatureDisabled()"" body=""stub"" value=""true"" />
    </type>
  </assembly>
</linker>
";

            testProject.AddItem("EmbeddedResource", new Dictionary<string, string>
            {
                ["Include"] = substitutionsFilename,
                ["LogicalName"] = substitutionsFilename
            });
        }

        public static void AddRuntimeConfigOption(XDocument project, bool trim)
        {
            var ns = project.Root.Name.Namespace;

            project.Root.Add(new XElement(ns + "ItemGroup",
                                new XElement("RuntimeHostConfigurationOption",
                                    new XAttribute("Include", "DisableFeature"),
                                    new XAttribute("Value", "true"),
                                    new XAttribute("Trim", trim.ToString()))));
        }

        public static TestProject CreateTestProjectWithAnalysisWarnings(string targetFramework, string projectName, bool isExe = true)
        {
            var testProject = new TestProject()
            {
                Name = projectName,
                TargetFrameworks = targetFramework,
                IsExe = isExe,
                SelfContained = "true"
            };

            // Don't error when generators/analyzers can't be loaded.
            // This can occur when running tests against FullFramework MSBuild
            // if the build machine has an MSBuild install with an older version of Roslyn
            // than the generators in the SDK reference. We aren't testing the generators here
            // and this failure will occur more clearly in other places when it's
            // actually an important failure, so don't error out here.
            // If we're actually testing that we build with no warnings, the test will still fail.
            testProject.AdditionalProperties["WarningsNotAsErrors"] = "CS9057;";

            testProject.SourceFiles[$"{projectName}.cs"] = @"
using System;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
public class Program
{
    public static void Main()
    {
        IL_2075();
        IL_2026();
        _ = IL_2043;
        new Derived().IL_2046();
        new Derived().IL_2093();
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)]
    public static string typeName;

    public static void IL_2075()
    {
        _ = Type.GetType(typeName).GetMethod(""SomeMethod"");
    }

    [RequiresUnreferencedCode(""Testing analysis warning IL2026"")]
    public static void IL_2026()
    {
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)]
    public static string IL_2043 {
        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)]
        get => null;
    }

    public class Base
    {
        [RequiresUnreferencedCode(""Testing analysis warning IL2046"")]
        public virtual void IL_2046() {}

        public virtual string IL_2093() => null;
    }

    public class Derived : Base
    {
        public override void IL_2046() {}

        [return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)]
        public override string IL_2093() => null;
    }
}
";

            return testProject;
        }

        public static TestProject CreateTestProjectWithIsTrimmableAttributes(
            string targetFramework,
            string projectName)
        {
            var testProject = new TestProject()
            {
                Name = projectName,
                TargetFrameworks = targetFramework,
                IsExe = true,
                SelfContained = "true"
            };

            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            testProject.SourceFiles[$"{projectName}.cs"] = @"
using System;
public class Program
{
    public static void Main()
    {
        TrimmableAssembly.UsedMethod();
        NonTrimmableAssembly.UsedMethod();
    }
}";

            var trimmableProject = new TestProject()
            {
                Name = "TrimmableAssembly",
                TargetFrameworks = targetFramework
            };

            trimmableProject.SourceFiles["TrimmableAssembly.cs"] = @"
using System.Reflection;

[assembly: AssemblyMetadata(""IsTrimmable"", ""True"")]

public static class TrimmableAssembly
{
    public static void UsedMethod()
    {
    }

    public static void UnusedMethod()
    {
    }
}";
            testProject.ReferencedProjects.Add(trimmableProject);

            var nonTrimmableProject = new TestProject()
            {
                Name = "NonTrimmableAssembly",
                TargetFrameworks = targetFramework
            };

            nonTrimmableProject.SourceFiles["NonTrimmableAssembly.cs"] = @"
public static class NonTrimmableAssembly
{
    public static void UsedMethod()
    {
    }

    public static void UnusedMethod()
    {
    }
}";
            testProject.ReferencedProjects.Add(nonTrimmableProject);

            var unusedTrimmableProject = new TestProject()
            {
                Name = "UnusedTrimmableAssembly",
                TargetFrameworks = targetFramework
            };

            unusedTrimmableProject.SourceFiles["UnusedTrimmableAssembly.cs"] = @"
using System.Reflection;

[assembly: AssemblyMetadata(""IsTrimmable"", ""True"")]

public static class UnusedTrimmableAssembly
{
    public static void UnusedMethod()
    {
    }
}
";
            testProject.ReferencedProjects.Add(unusedTrimmableProject);

            var unusedNonTrimmableProject = new TestProject()
            {
                Name = "UnusedNonTrimmableAssembly",
                TargetFrameworks = targetFramework
            };

            unusedNonTrimmableProject.SourceFiles["UnusedNonTrimmableAssembly.cs"] = @"
public static class UnusedNonTrimmableAssembly
{
    public static void UnusedMethod()
    {
    }
}
";
            testProject.ReferencedProjects.Add(unusedNonTrimmableProject);

            return testProject;
        }

        public static TestProject CreateTestProjectForILLinkTesting(
            TestAssetsManager testAssetsManager,
            string targetFrameworks,
            string mainProjectName,
            string referenceProjectName = null,
            bool usePackageReference = true,
            [CallerMemberName] string callingMethod = "",
            string referenceProjectIdentifier = null,
            Action<TestProject> modifyReferencedProject = null,
            bool addAssemblyReference = false,
            bool setSelfContained = true)
        {
            var testProject = new TestProject()
            {
                Name = mainProjectName,
                TargetFrameworks = targetFrameworks,
                IsExe = true
            };

            if (setSelfContained)
            {
                testProject.SelfContained = "true";
            }

            testProject.SourceFiles[$"{mainProjectName}.cs"] = @"
using System;
namespace HelloWorld
{
    public class Program
    {
        public static void Main()
        {
            Console.WriteLine(""Hello world"");
        }

        public static void UnusedMethod()
        {
        }
";

            if (addAssemblyReference)
            {
                testProject.SourceFiles[$"{mainProjectName}.cs"] += @"
        public static void UseClassLib()
        {
            ClassLib.UsedMethod();
        }
";
            }

            testProject.SourceFiles[$"{mainProjectName}.cs"] += @"
    }
}";

            if (referenceProjectName == null)
            {
                if (addAssemblyReference)
                    throw new ArgumentException("Adding an assembly reference requires a project to reference.");
                return testProject;
            }

            var referenceProject = new TestProject()
            {
                Name = referenceProjectName,
                // NOTE: If using a package reference for the reference project, it will be retrieved
                // from the nuget cache. Set the reference project TFM to the lowest common denominator
                // of these tests to prevent conflicts.
                TargetFrameworks = usePackageReference ? "netcoreapp3.0" : targetFrameworks,
            };
            referenceProject.SourceFiles[$"{referenceProjectName}.cs"] = @"
using System;

public class ClassLib
{
    public static void UsedMethod()
    {
    }

    public void UnusedMethod()
    {
    }

    public void UnusedMethodToRoot()
    {
    }

    public static bool FeatureDisabled { get; }

    public static void FeatureAPI()
    {
        if (FeatureDisabled)
            return;

        FeatureImplementation();
    }

    public static void FeatureImplementation()
    {
    }
}
";
            if (modifyReferencedProject != null)
                modifyReferencedProject(referenceProject);

            if (usePackageReference)
            {
                var referenceAsset = testAssetsManager.CreateTestProject(referenceProject, callingMethod, referenceProjectIdentifier ?? targetFrameworks);
                testProject.ReferencedProjects.Add(referenceAsset.TestProject);
            }
            else
            {
                testProject.ReferencedProjects.Add(referenceProject);
            }


            testProject.SourceFiles[$"{referenceProjectName}.xml"] = $@"
<linker>
  <assembly fullname=""{referenceProjectName}"">
    <type fullname=""ClassLib"">
      <method name=""UnusedMethodToRoot"" />
      <method name=""FeatureAPI"" />
    </type>
  </assembly>
</linker>
";

            return testProject;
        }

        public static void CheckILLinkVersion(TestAsset testAsset, string targetFramework)
        {
            var getKnownPacks = new GetValuesCommand(testAsset, "KnownILLinkPack", GetValuesCommand.ValueType.Item, targetFramework)
            {
                MetadataNames = new List<string> { "TargetFramework", "ILLinkPackVersion" }
            };
            getKnownPacks.Execute().Should().Pass();
            var knownPacks = getKnownPacks.GetValuesWithMetadata();
            var expectedVersion = knownPacks
                .Where(i => i.metadata["TargetFramework"] == targetFramework)
                .Select(i => i.metadata["ILLinkPackVersion"])
                .Single();

            var illinkTargetsCommand = new GetValuesCommand(testAsset, "ILLinkTargetsPath", targetFramework: targetFramework);
            illinkTargetsCommand.Properties.Add("PublishTrimmed", "true");
            illinkTargetsCommand.Execute().Should().Pass();
            var illinkTargetsPath = illinkTargetsCommand.GetValues()[0];
            var illinkVersion = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(illinkTargetsPath)));
            illinkVersion.Should().Be(expectedVersion);
        }

        public static void ValidateWarningsOnHelloWorldApp(PublishCommand publishCommand, CommandResult result, List<string> expectedWarnings, string targetFramework, string rid, bool useRegex = false)
        {
            // This checks that there are no unexpected warnings, but does not cause failures for missing expected warnings.
            var warnings = result.StdOut.Split('\n', '\r').Where(line => line.Contains("warning IL"));

            // This should also detect unexpected duplicates of expected warnings.
            // Each expected warning string/regex matches at most one warning.
            List<string> extraWarnings = new();
            foreach (var warning in warnings)
            {
                bool expected = false;
                for (int i = 0; i < expectedWarnings.Count; i++)
                {
                    if ((useRegex && Regex.IsMatch(warning, expectedWarnings[i])) ||
                        (!useRegex && warning.Contains(expectedWarnings[i])))
                    {
                        expectedWarnings.RemoveAt(i);
                        expected = true;
                        break;
                    }
                }

                if (!expected)
                {
                    extraWarnings.Add(warning);
                }
            }

            StringBuilder errorMessage = new();

            if (extraWarnings.Any())
            {
                // Print additional information to recognize which framework assemblies are being used.
                errorMessage.AppendLine($"Target framework from test: {targetFramework}");
                errorMessage.AppendLine($"Runtime identifier: {rid}");

                // Get the array of runtime assemblies inside the publish folder.
                string[] runtimeAssemblies = Directory.GetFiles(publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName, "*.dll");
                var paths = new List<string>(runtimeAssemblies);
                var resolver = new PathAssemblyResolver(paths);
                var mlc = new MetadataLoadContext(resolver, "System.Private.CoreLib");
                using (mlc)
                {
                    Assembly assembly = mlc.LoadFromAssemblyPath(Path.Combine(publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName, "System.Private.CoreLib.dll"));
                    string assemblyVersionInfo = (string)assembly.CustomAttributes.Where(ca => ca.AttributeType.Name == "AssemblyInformationalVersionAttribute").Select(ca => ca.ConstructorArguments[0].Value).FirstOrDefault();
                    errorMessage.AppendLine($"Runtime Assembly Informational Version: {assemblyVersionInfo}");
                }
                errorMessage.AppendLine($"The execution of a hello world app generated a diff in the number of warnings the app produces{Environment.NewLine}");
                errorMessage.AppendLine("Test output contained the following extra linker warnings:");
                foreach (var extraWarning in extraWarnings)
                    errorMessage.AppendLine($"+ {extraWarning}");
            }
            Assert.True(!extraWarnings.Any(), errorMessage.ToString());
        }
    }
}
