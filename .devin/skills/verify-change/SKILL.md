---
name: verify-change
description: Identify changed files and execute applicable builds, formatting checks, analysers, and tests with evidence-based reporting
allowed-tools:
  - read
  - grep
  - glob
  - exec
triggers:
  - user
  - model
---

Execute the standard OMS verification procedure for current repository changes.

## Procedure

1. **Identify Changed Files**
   - Check repository status (`git status --short` and `git diff --stat` if Git is initialised; otherwise inspect the workspace file tree via `glob`).
   - List all files created, modified, or deleted in the current task.

2. **Inspect Solution Composition & Diagnostic Integrity**
   - Read `Oms.slnx`, `Directory.Build.props`, and `Directory.Packages.props`.
   - Determine whether any `.csproj` application or test projects currently exist in the solution.
   - Check changed files to ensure no unapproved diagnostic suppressions (`NoWarn`, `WarningsNotAsErrors`, `#pragma warning disable`, `SuppressMessage`) or severity downgrades were introduced to bypass warnings-as-errors.

3. **Execute Applicable Checks**
   - **Solution & MSBuild Validation**: Run `dotnet build Oms.slnx` and `dotnet build Oms.slnx -c Release` to verify solution syntax, MSBuild property evaluation (`Directory.Build.props` / `Directory.Packages.props`), and strict warnings-as-errors enforcement across both `Debug` and `Release` configurations. (If `Oms.slnx` contains no `.csproj` projects yet, explicitly note that compiler and analyser enforcement cannot be demonstrated until a C# project is added.)
   - **Formatting & Code Style**:
     - If one or more `.csproj` projects exist in `Oms.slnx`, run `dotnet format Oms.slnx --verify-no-changes`.
     - If `Oms.slnx` contains no projects yet, record that `dotnet format` requires at least one project and verify `.editorconfig` and XML/Markdown syntax directly.
   - **Automated Tests**:
     - If test projects exist in `Oms.slnx`, run `dotnet test Oms.slnx -c Release --no-build`.
     - If no test projects exist yet, explicitly mark tests as **Not Applicable (no test projects in solution)**.
     - When tests are present, distinguish unit tests, architecture tests, and real-infrastructure integration tests, and summarise which business behaviours/invariants they protect as well as any important failure cases left untested.

4. **Validate Documentation & Configuration Consistency**
   - When documentation or configuration files are modified, verify that all internal Markdown file links resolve to existing files and that XML files (`Oms.slnx`, `Directory.Build.props`, `Directory.Packages.props`) are well-formed.

5. **Report Actual Outcomes**
   - Present a structured verification table showing:
     - **Check Name**
     - **Command / Method Executed**
     - **Status**: `Passed`, `Failed`, `Not Applicable`, or `Not Executed`
     - **Evidence**: Exact exit code and relevant command output.
   - **Strict Rule**: Never report a check as passed without executing it and observing evidence. Always distinguish successful checks from unavailable or unexecuted checks.
