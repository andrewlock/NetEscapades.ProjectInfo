using System;
using System.Collections.Generic;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[ShutdownDotNetAfterServerBuild]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main () => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution] readonly Solution Solution;

    AbsolutePath OutputDirectory => RootDirectory / "artifacts";
    AbsolutePath ArtifactsDirectory => OutputDirectory / "packages";
    AbsolutePath TestResultsDirectory => OutputDirectory / "results";

    [Parameter] readonly string NuGetToken;
    [Parameter] readonly AbsolutePath PackagesDirectory = RootDirectory / "packages";

    const string NugetOrgUrl = "https://api.nuget.org/v3/index.json";
    bool IsTag => GitHubActions.Instance?.Ref?.StartsWith("refs/tags/") ?? false;

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            OutputDirectory.CreateOrCleanDirectory();
            if (!string.IsNullOrEmpty(PackagesDirectory))
            {
                PackagesDirectory.CreateOrCleanDirectory();
            }
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .When(_ => !string.IsNullOrEmpty(PackagesDirectory), x=>x.SetPackageDirectory(PackagesDirectory))
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .When(_ => IsServerBuild, x => x.SetProperty("ContinuousIntegrationBuild", "true"))
                .EnableNoRestore());
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .When(_ => IsServerBuild, x => x.SetProperty("ContinuousIntegrationBuild", "true"))
                .When(_ => IsServerBuild, x => x
                    .SetLoggers("trx")
                    .SetResultsDirectory(TestResultsDirectory))
                .EnableNoBuild()
                .EnableNoRestore());
        });

    Target Pack => _ => _
        .DependsOn(Compile)
        .After(Test)
        .Produces(ArtifactsDirectory)
        .Executes(() =>
        {
            DotNetPack(s => s
                .SetConfiguration(Configuration)
                .SetOutputDirectory(ArtifactsDirectory)
                .EnableNoBuild()
                .EnableNoRestore()
                .When(_ => IsServerBuild, x => x.SetProperty("ContinuousIntegrationBuild", "true"))
                .SetProject(Solution));
        });

    Target TestPackage => _ => _
        .DependsOn(Pack)
        .After(Test)
        .Executes(() =>
        {
            var projectFiles = new[]
            {
                RootDirectory / "tests" / "NetEscapades.ProjectInfo.Nuget.IntegrationTests" / "NetEscapades.ProjectInfo.Nuget.IntegrationTests.csproj",
            };

            if (!string.IsNullOrEmpty(PackagesDirectory))
            {
                (PackagesDirectory / "netescapades.projectinfo").DeleteDirectory();
            }

            DotNetRestore(s => s
                .When(_ => !string.IsNullOrEmpty(PackagesDirectory), x => x.SetPackageDirectory(PackagesDirectory))
                .SetConfigFile(RootDirectory / "NuGet.integration-tests.config")
                .CombineWith(projectFiles, (s, p) => s.SetProjectFile(p)));

            DotNetBuild(s => s
                .When(_ => !string.IsNullOrEmpty(PackagesDirectory), x=>x.SetPackageDirectory(PackagesDirectory))
                .EnableNoRestore()
                .SetConfiguration(Configuration)
                .CombineWith(projectFiles, (s, p) => s.SetProjectFile(p)));

            DotNetTest(s => s
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .EnableNoRestore()
                .When(_ => IsServerBuild, x => x
                    .SetLoggers("trx")
                    .SetResultsDirectory(TestResultsDirectory))
                .CombineWith(projectFiles, (s, p) => s.SetProjectFile(p)));
        });

    Target GenerateReadmes => _ => _
        .Before(Pack)
        .Executes(() =>
        {
            DotNetToolRestore();
            DotNet("tool run mdsnippets", RootDirectory / "docs");
        });

    Target PushToNuGet => _ => _
        .DependsOn(Pack)
        .OnlyWhenStatic(() => IsTag && IsServerBuild && IsWin)
        .Requires(() => NuGetToken)
        .After(TestPackage)
        .Executes(() =>
        {
            var packages = ArtifactsDirectory.GlobFiles("*.nupkg");
            DotNetNuGetPush(s => s
                .SetApiKey(NuGetToken)
                .SetSource(NugetOrgUrl)
                .EnableSkipDuplicate()
                .CombineWith(packages, (x, package) => x
                    .SetTargetPath(package)));
        });
}
