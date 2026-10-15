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

Execute the standard OMS verification procedure and pre-handoff quality gate checks defined in [`docs/QUALITY-GATES.md`](../../../docs/QUALITY-GATES.md).

## Procedure

1. **Identify Changed Files & Self-Review Diff**
   - Check repository status (`git status --short` and `git diff --stat`).
   - Inspect the full `git diff` against `docs/QUALITY-GATES.md` (Areas A–I: scope, domain invariants, Result pattern, code design, dependencies/architecture, meaningful tests, infrastructure, engineering standards, and PR hygiene).

2. **Inspect Solution Composition & Diagnostic Integrity**
   - Read `Oms.slnx`, `Directory.Build.props`, and `Directory.Packages.props`.
   - Check changed files to ensure no unapproved diagnostic suppressions (`NoWarn`, `WarningsNotAsErrors`, `#pragma warning disable`, `SuppressMessage`), severity downgrades, or prohibited layer dependencies were introduced.

3. **Execute Applicable Checks**
   - **Restore & Build Validation**: Run `dotnet restore Oms.slnx`, `dotnet build Oms.slnx -c Debug`, and `dotnet build Oms.slnx -c Release` to verify zero warnings and zero errors across both configurations.
   - **Formatting & Code Style**: Run `dotnet format Oms.slnx --verify-no-changes`.
   - **Automated Tests**:
     - Run `dotnet test Oms.slnx -c Release --no-build`.
     - Distinguish unit tests, lightweight architecture checks, and integration tests, and summarise which business behaviours/invariants they protect as well as any important failure cases or limitations left untested.

4. **Validate Documentation & Configuration Consistency**
   - When documentation or configuration files are modified, verify that all internal Markdown file links resolve to existing files and that XML files (`Oms.slnx`, `Directory.Build.props`, `Directory.Packages.props`) are well-formed.

5. **Report Actual Outcomes (`Engineering Quality Gate`)**
   - Present the verification results and `Engineering Quality Gate` summary required by `docs/QUALITY-GATES.md` Section 4.
   - **Strict Rule**: Never report a check as passed without executing it on the final code and observing evidence. Always distinguish successful checks from unavailable or unexecuted checks.
