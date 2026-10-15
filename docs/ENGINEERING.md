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

## Domain Error Handling: Result Pattern vs Exceptions

OMS uses a minimal, internally owned Result Pattern (`Result`, `Result<TValue>`, and `Error`) inside each service's domain layer rather than third-party Result packages (such as `Ardalis.Result`):

- **Why Internally Owned**: Avoids external dependency coupling inside the domain layer, eliminates exposure to third-party API/licensing churn, and keeps the abstraction small, immutable, and free of HTTP-specific status codes or monadic pipeline bloat.
- **When to Return `Result` / `Result<TValue>` (Expected Business & Validation Failures)**:
  - Domain factory validation (such as empty order items, invalid quantities, non-positive unit prices, duplicate products, mixed currencies, or invalid currency codes in `Money.Create` / `Order.Create`).
  - Domain state machine transitions (such as attempting `ConfirmPayment()`, `MarkStockReserved()`, or `Cancel()` from an invalid or terminal `OrderStatus`).
  - Failed domain operations must never partially mutate aggregate state.
- **When Exceptions Remain Appropriate (Unexpected Technical & Programming Failures)**:
  - Guarding programming preconditions and contract violations (`ArgumentNullException` on null collection references, accessing `Result<TValue>.Value` on a failed result, or calling arithmetic operators on an uninitialised `default(Money)` or mismatched currency).
  - Arithmetic overflow (`checked` decimal calculations) and unexpected runtime or infrastructure failures.
  - Never catch arbitrary technical exceptions and disguise them as business `Result` failures.
- **Centralised Domain Error Definitions**:
  - Domain errors are defined per bounded context in static error classes (such as `OrderErrors` and `MoneyErrors`) with stable, machine-readable codes (`Orders.EmptyItems`, `Orders.DuplicateProduct`, `Orders.InvalidTransition`, `Money.InvalidCurrency`) and human-readable descriptions.
  - Domain errors remain independent of HTTP status codes; translating domain error codes into HTTP ProblemDetails responses belongs strictly to the API layer.

## EF Core Persistence & Migration Conventions

- **Infrastructure Boundary**: EF Core `DbContext` types, Fluent API `IEntityTypeConfiguration<T>` classes, and migrations live under `Infrastructure/Persistence/` inside the owning service assembly. Domain types must never reference `Microsoft.EntityFrameworkCore` or carry ORM data annotations.
- **Configuration & Connection Strings**: Database connection strings are bound via standard .NET `IConfiguration` (`ConnectionStrings:OrdersDatabase` in `appsettings.Development.json` or environment variables). Never hardcode credentials or run `Database.Migrate()` / `Database.EnsureCreated()` automatically during normal application startup.
- **Local Migration Commands** (using the local `dotnet-ef` tool manifest):
  ```bash
  # Restore local tools (dotnet-ef)
  dotnet tool restore

  # Add a new migration for OrdersDbContext
  dotnet ef migrations add <MigrationName> --project src/Orders/Oms.Orders.Api/Oms.Orders.Api.csproj --startup-project src/Orders/Oms.Orders.Api/Oms.Orders.Api.csproj --output-dir Infrastructure/Persistence/Migrations

  # Apply migrations to a running local PostgreSQL instance
  dotnet ef database update --project src/Orders/Oms.Orders.Api/Oms.Orders.Api.csproj --startup-project src/Orders/Oms.Orders.Api/Oms.Orders.Api.csproj
  ```

## Central Package Management (CPM)

NuGet dependencies are governed centrally through `Directory.Packages.props`:

- `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>` is enabled.
- `<CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>` is enabled to allow deterministic security pinning of transitive dependencies when required.
- Project files (`.csproj`) must use `<PackageReference Include="Package.Name" />` **without** a `Version` attribute; all versions are declared once via `<PackageVersion Include="Package.Name" Version="x.y.z" />` in `Directory.Packages.props`.
- **Zero Speculative Packages Rule**: Do not add `<PackageVersion>` entries to `Directory.Packages.props` until a project in the solution actively requires the package.
- **Supply Chain Hygiene**: When adding new packages in future tasks, prefer stable releases published at least 7 days prior and avoid floating version ranges (`*`).
- **MediatR In-Process CQRS & Licensing**:
  - `MediatR` (`14.2.0`) is used strictly for in-process CQRS request/response dispatching (`ISender`). It is registered via `AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<Program>())` without extra DI wrapper packages.
  - Starting with MediatR v13+, Lucky Penny Software uses a dual/commercial licensing model (`LicenseKey` on `MediatRServiceConfiguration`). Never hardcode or commit a licence key in source code or tracked `appsettings*.json` files; if a licence key is configured in external environments, supply it via user secrets or environment variables. Do not suppress licensing diagnostics in test output.

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
  - `Directory.Build.props` sets `<GenerateDocumentationFile>true</GenerateDocumentationFile>` for production projects with **no** global `CS1591` suppression.
  - Generating XML documentation files in production assemblies enables Roslyn and SonarQube analysers to inspect complete symbol metadata during builds.
  - Because `CS1591` is not suppressed and warnings are treated as errors across all configurations, all publicly visible types and members (`public` / `protected`) in production projects must either provide meaningful XML documentation comments (`/// <summary>...`) or use `internal` visibility where types are implementation details not intended as public contracts.
  - For test projects (`*.Tests`, `*.UnitTests`, `*.IntegrationTests`), `Directory.Build.props` sets `<GenerateDocumentationFile>false</GenerateDocumentationFile>`. xUnit requires test classes and `[Fact]` / `[Theory]` methods to be `public`, and self-describing test names should not carry redundant XML comment boilerplate solely to satisfy `CS1591`. All other compiler, Roslyn, and code-style analysers remain enforced as errors in test projects.

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
