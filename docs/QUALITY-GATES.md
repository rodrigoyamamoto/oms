# Engineering Quality Gates

## Purpose

This document is the single source of truth for the **objective, repeatable engineering quality gate** that every OMS task must satisfy **before** producing a final handoff or requesting human pull request approval.

Quality and transparency take priority over obtaining a particular review label. Never conceal limitations, suppress diagnostics, or inflate test counts merely to avoid review notes.

---

## 1. Mandatory Pre-Handoff Execution Sequence

Every implementation task must execute this seven-step sequence in order:

1. **Step 1 — Implementation**: Implement only the approved task scope within the designated architectural boundaries.
2. **Step 2 — Self-Review**: Inspect the actual `git diff` line by line against Sections 2A–2I of this quality gate (do not rely on task summaries).
3. **Step 3 — Behavioural Verification**: Execute all applicable restore, Debug/Release build, formatting, unit test, and integration test commands via `/verify-change`, examining edge cases and failure paths.
4. **Step 4 — Independent Read-Only Review**: Delegate an independent review via `/review-change` (or a read-only subagent) that inspects the actual implementation and test source files against this quality gate.
5. **Step 5 — Findings Resolution**: Classify all review findings. Fix all correctness defects, missing requirements, weak tests, architecture violations, and unnecessary complexity before handoff, then re-run affected verification checks. Do not blindly implement purely subjective preferences that add complexity.
6. **Step 6 — Final Quality Gate**: Re-evaluate the final `git diff` and verification outputs on the exact commit that will be pushed.
7. **Step 7 — Handoff**: Produce the final handoff report (including the mandatory `Engineering Quality Gate` evidence section) and update the pull request.

---

## 2. Quality Gate Criteria (A–I)

### A. Requirements and Scope

- **Checks**:
  - Confirm every requirement in the task prompt is implemented or explicitly reported as incomplete/deferred.
  - Verify no unrequested functionality, extra endpoints, or speculative abstractions were added.
  - Confirm explicit exclusions (`.NET Aspire`, `MassTransit`, direct `Polly` API usage, and task-specific exclusions) are respected.
  - Verify consistency with accepted ADRs in [`docs/adr/`](adr/README.md) and document justification for any deviation.
- **Acceptance Criterion**: The implementation matches the approved scope without hidden changes or unreported omissions.

### B. Domain Modelling and Correctness

- **Checks** *(when domain logic is touched)*:
  - **Identity & Ownership**: Internally generated entity IDs use `Guid.CreateVersion7()`; externally supplied IDs (such as `ProductId`) are validated against `Guid.Empty` and preserved without regeneration.
  - **Aggregate Encapsulation**: Child entities (such as `OrderItem`) are instantiated and owned exclusively by their aggregate root (`Order`); collections are exposed as read-only (`ReadOnlyCollection<T>`) and cannot be mutated via source-list changes or interface casting.
  - **Invariants & State Transitions**: Can invalid state be constructed? Can callers bypass aggregate rules via public constructors or setters? Are lifecycle transitions guarded so invalid or terminal transitions fail explicitly?
  - **Failure Atomicity**: Can a failed validation or state transition leave an aggregate partially mutated? All validation must complete before mutating fields.
  - **Value Objects**: Value objects (such as `Money`) are immutable, enforce value-based equality, guard against `default` uninitialised struct states, and avoid duplicated business rules.
- **Acceptance Criterion**: The implementation preserves all documented invariants across both success and failure paths.

### C. Result Pattern and Error Handling

- **Checks** *(when `Result`, `Result<TValue>`, or domain errors are touched)*:
  - **State Validity by Construction**: A successful `Result` cannot carry a failure `Error`; a failed `Result` cannot carry `Error.None` or `default(Error)`; a successful `Result<TValue>` cannot wrap `null`; a failed `Result<TValue>` throws `InvalidOperationException` when `.Value` is accessed.
  - **Result vs Exception Boundary**: Expected business and validation failures return `Result` / `Result<TValue>`; unexpected technical failures and programming contract violations (`ArgumentNullException` on null collection arguments, `default(Money)` arithmetic, `checked` overflow) throw exceptions and are never silently swallowed into generic business errors.
  - **Centralised, Transport-Independent Errors**: Error codes (e.g. `Orders.EmptyItems`, `Orders.InvalidTransition`, `Money.InvalidCurrency`) are stable, defined in domain error classes (`OrderErrors`, `MoneyErrors`), not scattered as inline literals, and free of HTTP status codes.
- **Acceptance Criterion**: Business failures are explicit, predictable, and cannot be mistaken for success or partially mutate state.

### D. Code Design, Maintainability, and Anti-Pattern Review

- **Checks**:
  - Prefer the smallest design that correctly protects the required behaviour (do not equate fewer lines of code with better engineering if clarity suffers, and do not add abstractions solely for hypothetical future tasks).
  - Verify every class, struct, interface, and method has a concrete responsibility—no generic repositories, Unit-of-Work wrappers around `DbContext`, decorative marker interfaces (`IAggregateRoot`), pass-through wrappers, or custom framework/compiler tools.
  - Restrict visibility (`internal` by default inside service assemblies; `private` constructors where factories enforce invariants).
  - Ensure zero temporary probes, dead code, or obsolete helpers remain in the working tree.
  - **Mandatory Anti-Pattern & Modern Practices Check**: Before committing, explicitly evaluate the actual code against current .NET 10, EF Core 10, Npgsql, and PostgreSQL guidance:
    1. *Is any implementation a recognised anti-pattern for this use case?* (e.g. generic repository/Unit-of-Work over `DbContext`, weakening domain encapsulation for ORM mapping, indiscriminate entity graph updates, uncontrolled lazy loading, synchronous database I/O in async paths, unnecessary explicit transactions where `SaveChangesAsync` already provides atomicity, or substituting `EnsureCreated()` / EF Core `InMemory` for real PostgreSQL migrations).
    2. *Is there a simpler, idiomatic .NET 10 / EF Core 10 implementation, or are we duplicating platform capabilities?*
    3. *Are we relying on outdated guidance or deprecated APIs?* (e.g. obsolete `UseXminAsConcurrencyToken()` instead of `.IsRowVersion()`, or obsolete `PostgreSqlBuilder()` parameterless constructors).
- **Acceptance Criterion**: The code is direct, idiomatic C# 14 / .NET 10, free of recognised anti-patterns, deprecated APIs, and speculative indirection.

### E. Dependency and Architecture Discipline

- **Checks**:
  - Inspect `.csproj` files and `Directory.Packages.props` to confirm no unapproved NuGet packages or project references were added.
  - Inspect `using` directives and type references across `Domain`, `Application`, `Infrastructure`, and `Api` / `Worker` files:
    - `Domain` must not depend on `Application`, `Infrastructure`, `Api`, ASP.NET Core, EF Core, `MediatR`, or `RabbitMQ.Client`.
    - `Application` must not depend on `Infrastructure` or `Api`.
  - **Honest Enforcement Scope**: Acknowledge that in our single-assembly-per-service architecture ([ADR-0001](adr/0001-independent-service-boundaries.md)), layer dependency discipline relies on direct source/diff inspection during code review plus lightweight reflection checks (such as verifying `Domain` types remain `internal`), without maintaining custom IL/bytecode parsers or claiming reflection inspects method bodies.
- **Acceptance Criterion**: No unexplained or prohibited dependency has been introduced across layers or packages.

### F. Meaningful Testing

- **Checks**:
  - Evaluate tests by the **risks and invariants they protect**, not by test count or coverage percentage (see [`docs/TESTING.md`](TESTING.md)).
  - For each behaviour, verify:
    1. What defect would this test detect, and would it fail if the invariant were removed?
    2. Does it assert externally observable outcomes (returned `Result`, `Error.Code`, aggregate `Status`, `Items`, `Total`) rather than internal implementation details?
    3. Are happy paths, invalid inputs, boundary values (`0`, negative, empty), duplicate entries, currency mismatches, invalid state transitions, state preservation after failure, and collection immutability covered?
  - **Controlled Regression Probe (When Practical)**: For critical or suspicious invariants, temporarily break the invariant in production code, run `dotnet test` to confirm the test fails, restore the code, and confirm `dotnet test` passes again—leaving zero temporary changes behind. Never claim mutation testing was performed unless actually executed.
- **Acceptance Criterion**: Important business invariants and failure modes are protected by deterministic tests with demonstrable regression value.

### G. Infrastructure and Distributed Systems (When Applicable)

When a task introduces persistence, messaging, HTTP endpoints, or background workers, verify the corresponding failure modes:
- **Persistence**: Explicit transaction boundaries, EF Core entity mappings/materialisation, optimistic concurrency (`xmin`), database constraints (`CHECK` / unique), migrations, and Testcontainers PostgreSQL verification.
- **Messaging**: Duplicate delivery, Inbox deduplication, bounded retries, Dead-Letter Queue (`*.dlq`) routing, Outbox atomicity, and `BasicAckAsync` ordering after database commit.
- **HTTP APIs**: Request validation, response status/body semantics, mapping of domain `Error` codes to ProblemDetails, and no leakage of internal stack traces.
- **Security, Secrets & Configuration Handling**:
  - Inspect `appsettings.json`, `appsettings.*.json`, `compose.yaml`, `.env.example`, `.gitignore`, and the full Git commit history to verify zero passwords, credential-bearing connection strings, API keys, or tokens are tracked.
  - Ensure `.env` and local credential files are ignored by `.gitignore`, and `.env.example` contains obvious non-working placeholders only (such as `<replace_with_local_postgres_password>`).
  - Keep container credentials external to `compose.yaml` (using required variable interpolation `${POSTGRES_PASSWORD:?...}`) and use native **.NET User Secrets** (`dotnet user-secrets`, backed by `<UserSecretsId>` in the service `.csproj`) for local API connection strings in `Development`.
  - Verify connection strings and passwords are never logged, included in exception messages returned to HTTP callers, or printed by startup diagnostics, and that `Production` ProblemDetails responses never expose sensitive infrastructure details.
  - If a genuine credential was ever committed to Git history, report the exposure immediately and require credential rotation rather than assuming removing it in a later commit is sufficient.
- **Acceptance Criterion**: Tests, configuration, and implementation match the real failure modes and security boundaries of the infrastructure introduced in that task.

### H. Engineering Standards

- **Checks**:
  - `net10.0`, C# `14.0`, `<Nullable>enable</Nullable>`, `<EnableNETAnalyzers>true</EnableNETAnalyzers>`, and `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>` remain intact.
  - Both `dotnet build Oms.slnx -c Debug` and `dotnet build Oms.slnx -c Release` complete with **0 warnings and 0 errors** under `TreatWarningsAsErrors=true`.
  - `dotnet format Oms.slnx --verify-no-changes` passes with exit code `0`.
  - No `NoWarn`, `WarningsNotAsErrors`, `#pragma warning disable`, `[SuppressMessage]`, or `.editorconfig` severity downgrades were introduced.
  - XML documentation is generated for production projects (`GenerateDocumentationFile=true`) and disabled for test projects (`GenerateDocumentationFile=false`).
  - Documentation and comments use concise British English.
- **Acceptance Criterion**: All build, formatting, and analyser checks pass with zero warnings and zero suppressions.

### I. Repository and PR Hygiene

- **Checks**:
  - Branch is based on the expected `main` commit and contains only changes belonging to the current task.
  - Full `git diff` and `git status --ignored` have been inspected to confirm no build artefacts (`bin/`, `obj/`), IDE files (`.idea/`), temporary probes, or local configs are staged.
  - Documentation and PR description match the actual committed code, and verification outputs were recorded from the final commit.
- **Acceptance Criterion**: The pull request is clean, reproducible, and ready for human technical review.

---

## 3. Objective Review Verdicts

Both self-reviews and independent read-only reviews (`reviewer` subagent / `/review-change`) must classify their outcome using one of three objective verdicts:

### `APPROVED`
Use **only** when:
- Every applicable quality gate (A–I) passes.
- All task requirements are implemented.
- Verification commands are complete and successful on the final code.
- No known correctness defects, unprotected invariants, missing required tests, or architecture violations remain.
- No significant unnecessary complexity remains, and all review findings requiring changes have been resolved.
- Do not invent speculative warnings or generic improvement suggestions merely to populate a notes section.

### `APPROVED WITH NOTES`
Use when:
- The implementation is acceptable for the current approved scope and all blocking gates pass, **but** there are specific, evidenced, non-blocking limitations or intentionally deferred concerns that do not contradict task requirements.
- The reviewer explains clearly why proceeding is safe. This should be an exception for genuine caveats rather than a default verdict.

### `NOT APPROVED`
Use when:
- Any task requirement is missing or incorrect, an important invariant is unprotected, a correctness or architecture defect remains, verification fails or is incomplete, tests lack regression value, or important review findings remain unresolved.
- Never downgrade a blocking defect into an optional note merely to obtain approval.

---

## 4. Mandatory Handoff Section (`Engineering Quality Gate`)

Every task handoff report and pull request update must include a concise **`Engineering Quality Gate`** section reporting:

1. **Applicable Quality Areas Reviewed**: Which of Gates A–I applied and were checked.
2. **Design or Architecture Concerns Identified**: Any complexity or boundary issues found and how they were handled.
3. **Anti-Pattern & Modern Practices Review**: Issues found, deprecated APIs or anti-patterns avoided/corrected, and any unresolved concerns.
4. **Meaningful Regression Risks Tested**: Key invariants, failure paths, and any controlled regression probes executed.
5. **Commands Executed and Actual Results**: Exact build, format, and test commands with exit codes and test counts on the final commit.
6. **Independent Review Outcome & Resolution**: The independent reviewer's verdict (`APPROVED`, `APPROVED WITH NOTES`, or `NOT APPROVED`), findings raised, and how confirmed issues were resolved.
7. **Unexecuted Checks & Remaining Limitations**: Any checks that were not applicable or any risks requiring human attention.
