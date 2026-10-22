# AGENTS.md

This file provides guidance to AI coding agents when working with code in this repository.

## Project Overview

NetEscapades.ProjectInfo is a C# incremental source generator. The generator is currently a skeleton: it is registered but does not emit any source yet.

## Build Commands

This project uses [Nuke](https://nuke.build/) for builds. The solution file is `NetEscapades.ProjectInfo.sln`. It requires the .NET 10 SDK (pinned in `global.json`); the test projects multi-target older runtimes (net48, netcoreapp2.1 through net10.0), so those runtimes must be installed. The Nuget integration tests and `_build` are deliberately not in the solution (the package doesn't exist until it is packed); they are driven by the `TestPackage` target.

```bash
# Build
./build.cmd Compile          # Windows
./build.sh Compile           # Unix

# Run all tests (unit + integration)
./build.cmd Test
./build.sh Test

# Run a single test project directly
dotnet test tests/NetEscapades.ProjectInfo.Tests

# Pack NuGet packages
./build.cmd Pack

# Full CI pipeline (clean + test + package tests)
./build.cmd Clean Test TestPackage
```

## Architecture

### NuGet Package Structure

- **NetEscapades.ProjectInfo.Generators** â€” the source generator (netstandard2.0), packed into `analyzers/dotnet/cs`. Also ships `buildTransitive` props/targets.

### Source Generator (`src/NetEscapades.ProjectInfo.Generators/`)

- **ProjectInfoGenerator.cs** â€” `IIncrementalGenerator` entry point.
- **TrackingNames.cs** â€” String constants for incremental generator step tracking, used in tests to verify caching behavior.
- **EquatableArray.cs**, **HashCode.cs**, **LocationInfo.cs** â€” helpers for building cache-friendly pipeline models.

### Key Design Constraints

- **Generators target netstandard2.0** â€” must run inside any version of the Roslyn compiler. Cannot use modern .NET APIs directly; uses the `Polyfill` package for compatibility.
- **Incremental generator caching** â€” pipeline models and outputs must correctly implement equality. Tests verify this by running the generator twice and asserting all tracked outputs are `Cached` or `Unchanged` on the second run.
- **No Roslyn symbols in outputs** â€” the generator must not store `ISymbol`, `Compilation`, or `SyntaxNode` objects in pipeline outputs (would break caching). `TestHelpers.AssertObjectGraph` validates this.

## Testing

- **NetEscapades.ProjectInfo.Tests** â€” unit tests. `TestHelpers.GetGeneratedOutput()` runs the generator twice over source strings and checks caching. Snapshot tests use Verify.Xunit.
- **NetEscapades.ProjectInfo.IntegrationTests** â€” references the generator project directly.
- **NetEscapades.ProjectInfo.Nuget.IntegrationTests** â€” shares the integration test sources but references the packed NuGet package (run via the `TestPackage` target).

## Version Management

- Version is defined in `version.props` (`VersionPrefix` + `VersionSuffix`).
- `Constants.cs` in the generators project also contains the version string.
- Both must be updated together when changing versions, along with `releasenotes.props` and `CHANGELOG.md`.
