# Engineering Standards

## Overview

This document defines the .NET build configuration, coding conventions, dependency governance, and static analysis standards established for the OMS repository.

## .NET SDK and Language Standards

- **Target Framework**: `net10.0` (.NET 10)
- **C# Language Version**: `14.0` (standard stable language features shipped with .NET 10; avoid preview features)
- **Solution Format**: XML-based `.slnx` (`Oms.slnx` at the repository root)
- **Compiler Strictness**:
  - `<Nullable>enable</Nullable>` across all projects via `Directory.Build.props`.
  - `<ImplicitUsings>enable</ImplicitUsings>` to reduce boilerplate while keeping namespace imports clean.
  - `<Deterministic>true</Deterministic>` for reproducible compilation.
  - `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and `<CodeAnalysisTreatWarningsAsErrors>true</CodeAnalysisTreatWarningsAsErrors>` are enabled unconditionally across all configurations (`Debug` and `Release`) to prevent warning debt in any environment.

## Code Style and Formatting

Formatting and code style are enforced via `.editorconfig` and MSBuild properties in `Directory.Build.props`:

- **Line Endings & Encoding**: UTF-8 encoding with `LF` line endings for source, configuration, and documentation files (`CRLF` reserved for `.cmd`, `.bat`, `.ps1`, and legacy `.sln` files via `.gitattributes` and `.editorconfig`).
- **Indentation**: 4 spaces for C# source files; 2 spaces for XML/MSBuild (`.csproj`, `.props`, `.slnx`), JSON, YAML, Markdown, and frontend files.
- **Namespaces**: File-scoped namespaces (`namespace Oms...;`) are required (`warning` severity).
- **Braces**: All control-flow blocks must use explicit braces (`csharp_prefer_braces = true:warning`).
- **Naming Conventions**:
  - Interfaces: `IPascalCase` (`warning`)
  - Types, methods, properties, events, and constants: `PascalCase` (`warning`)
  - Private and `private protected` fields: `_camelCase` (`warning`)
- **Language Style**: Prefer `readonly` fields, pattern matching, collection initialisers, and explicit accessibility modifiers for non-interface members.

## Central Package Management (CPM)

NuGet dependencies are governed centrally through `Directory.Packages.props`:

- `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` is enabled.
- `<CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>` is enabled to allow deterministic security pinning of transitive dependencies when required.
- Project files (`.csproj`) must use `<PackageReference Include="Package.Name" />` **without** a `Version` attribute; all versions are declared once via `<PackageVersion Include="Package.Name" Version="x.y.z" />` in `Directory.Packages.props`.
- **Zero Speculative Packages Rule**: Do not add `<PackageVersion>` entries to `Directory.Packages.props` until a project in the solution actively requires the package.
- **Supply Chain Hygiene**: When adding new packages in future tasks, prefer stable releases published at least 7 days prior and avoid floating version ranges (`*`).

## Static Analysis and SonarQube Readiness

### Roslyn Analysers

`Directory.Build.props` enables the built-in .NET SDK analysers across the solution:

- `<EnableNETAnalyzers>true</EnableNETAnalyzers>`
- `<AnalysisLevel>latest-recommended</AnalysisLevel>`
- `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`

*(Note: Analyser execution timing (`ReportAnalyzer`) is not enabled unconditionally in `Directory.Build.props`; pass `-p:ReportAnalyzer=true` on the command line when diagnosing build or analyser performance.)*

### Diagnostic Discipline & XML Documentation Policy

- **No Silent Diagnostic Suppression**:
  - Never suppress compiler or analyser warnings merely to make a build pass.
  - Never downgrade diagnostic severity in `.editorconfig` or project files to conceal problems.
  - Never introduce broad `<NoWarn>`, `<WarningsNotAsErrors>`, `#pragma warning disable`, or `[SuppressMessage]` workarounds without investigating the root cause and obtaining explicit human approval.
  - Never swallow exceptions or conceal failed operations.
- **XML Documentation Policy**:
  - `Directory.Build.props` sets `<GenerateDocumentationFile>true</GenerateDocumentationFile>` with **no** global `CS1591` suppression.
  - Generating XML documentation files enables Roslyn and SonarQube analysers to inspect complete symbol metadata during builds.
  - Because `CS1591` is not suppressed and warnings are treated as errors across all configurations, all publicly visible types and members (`public` / `protected`) must either provide meaningful XML documentation comments (`/// <summary>...`) or use `internal` visibility where types are implementation details not intended as public contracts.

### SonarQube Preparation

The repository is prepared for future SonarQube / SonarScanner for .NET integration without hardcoding server URLs, project keys, or credentials:

1. **XML Documentation Metadata**: `<GenerateDocumentationFile>true</GenerateDocumentationFile>` ensures the .NET compiler and SonarScanner have full symbol documentation metadata during build analysis.
2. **Deterministic CI Builds**: `<ContinuousIntegrationBuild>` is automatically enabled when `CI`, `GITHUB_ACTIONS`, or `TF_BUILD` environment variables are present.
3. **Git Ignore Hygiene**: `.gitignore` excludes `.sonarqube/`, `.scannerwork/`, `.trx`, and coverage output directories (`coverage/`, `TestResults/`, `*.coveragexml`) so local or CI scanner runs never pollute the working tree.
4. **No Fabricated Configuration**: SonarQube server endpoints, tokens, organisation keys, and quality-gate assertions must be supplied via CI pipeline environment variables or local gitignored settings when a Sonar server is provisioned—never hardcoded in repository files.

## Human Review and Pull Request Standards

- All changes must use `.github/PULL_REQUEST_TEMPLATE.md` when raising pull requests.
- Pull request descriptions and documentation must be written in concise British English.
- Every pull request must record actual verification commands and outcomes, architectural impact, and human review sign-off before merge.
