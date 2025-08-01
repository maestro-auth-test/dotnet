// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;

namespace Microsoft.DotNet.Cli.Run.Tests
{
    public class GivenDotnetRootEnv : SdkTest
    {
        private static Version Version6_0 = new(6, 0);

        public GivenDotnetRootEnv(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyTheory]
        [InlineData(ToolsetInfo.CurrentTargetFramework)]
        public void ItShouldSetDotnetRootToDirectoryOfMuxer(string targetFramework)
        {
            string expectDotnetRoot = TestContext.Current.ToolsetUnderTest.DotNetRoot;
            string expectOutput = GetExpectOutput(expectDotnetRoot, targetFramework);

            var projectRoot = SetupDotnetRootEchoProject(null, targetFramework);

            var runCommand = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(projectRoot);

            runCommand.EnvironmentToRemove.Add("DOTNET_ROOT");
            runCommand.EnvironmentToRemove.Add("DOTNET_ROOT(x86)");

            runCommand.Execute("--no-build")
                .Should().Pass()
                .And.HaveStdOutContaining(expectOutput);
        }

        //  https://github.com/dotnet/sdk/issues/49665
        //  Failed to load /private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, error: dlopen(/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib, 0x0001): tried: '/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64')), '/System/Volumes/Preboot/Cryptexes/OS/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (no such file), '/private/tmp/helix/working/B3F609DC/p/d/shared/Microsoft.NETCore.App/9.0.0/libhostpolicy.dylib' (mach-o file, but is an incompatible architecture (have 'x86_64', need 'arm64'))
        [PlatformSpecificFact(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public void WhenDotnetRootIsSetItShouldSetDotnetRootToDirectoryOfMuxer()
        {
            string expectDotnetRoot = "OVERRIDE VALUE";

            var projectRoot = SetupDotnetRootEchoProject();
            var processArchitecture = RuntimeInformation.ProcessArchitecture.ToString().ToUpperInvariant();
            var runCommand = new DotnetCommand(Log, "run")
                .WithWorkingDirectory(projectRoot);

            if (Environment.Is64BitProcess)
            {
                runCommand = runCommand.WithEnvironmentVariable("DOTNET_ROOT", expectDotnetRoot);
                runCommand.EnvironmentToRemove.Add("DOTNET_ROOT(x86)");
            }
            else
            {
                runCommand = runCommand.WithEnvironmentVariable("DOTNET_ROOT(x86)", expectDotnetRoot);
                runCommand.EnvironmentToRemove.Add("DOTNET_ROOT");
            }

            runCommand.EnvironmentToRemove.Add($"DOTNET_ROOT_{processArchitecture}");
            runCommand
                .Execute("--no-build")
                .Should().Pass()
                .And.HaveStdOutContaining(GetExpectOutput(expectDotnetRoot));
        }

        private string SetupDotnetRootEchoProject([CallerMemberName] string callingMethod = null, string targetFramework = null)
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("TestAppEchoDotnetRoot", callingMethod, allowCopyIfPresent: true)
                .WithSource()
                .WithTargetFrameworkOrFrameworks(targetFramework ?? null, false)
                .Restore(Log);

            new BuildCommand(testAsset)
                .Execute($"{(!string.IsNullOrEmpty(targetFramework) ? "/p:TargetFramework=" + targetFramework : string.Empty)}")
                .Should()
                .Pass();

            return testAsset.Path;
        }

        private static string GetExpectOutput(string expectDotnetRoot, string targetFramework = null)
        {
            string expectOutput;
            string processArchitecture = RuntimeInformation.ProcessArchitecture.ToString().ToUpperInvariant();
            if (!string.IsNullOrEmpty(targetFramework) && Version.Parse(targetFramework.AsSpan(3)) >= Version6_0)
            {
                expectOutput = $"DOTNET_ROOT='';DOTNET_ROOT(x86)='';DOTNET_ROOT_{processArchitecture}='{expectDotnetRoot}'";
            }
            else if (Environment.Is64BitProcess)
            {
                expectOutput = @$"DOTNET_ROOT='{expectDotnetRoot}';DOTNET_ROOT(x86)='';DOTNET_ROOT_{processArchitecture}=''";
            }
            else
            {
                expectOutput = @$"DOTNET_ROOT='';DOTNET_ROOT(x86)='{expectDotnetRoot}';DOTNET_ROOT_{processArchitecture}=''";
            }

            return expectOutput;
        }
    }
}
