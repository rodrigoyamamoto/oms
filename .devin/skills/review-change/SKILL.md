---
name: review-change
description: Perform an independent read-only review of repository changes for correctness, architecture compliance, maintainability, security, and test adequacy
agent: reviewer
triggers:
  - user
  - model
---

Perform a rigorous, independent, read-only review of the changes in the OMS repository.

## Review Mandate

- **Read-Only**: Do not modify files, apply fixes, or approve your own changes.
- Inspect the actual implementation and test source files (not just task summaries) against [`docs/QUALITY-GATES.md`](../../../docs/QUALITY-GATES.md), `AGENTS.md`, `docs/ARCHITECTURE.md`, `docs/ENGINEERING.md`, `docs/TESTING.md`, and `docs/AI-WORKFLOW.md`.

## Evaluation Checklist

Evaluate the changes across the quality-gate areas in `docs/QUALITY-GATES.md`:

1. **Requirements & Scope (Gate A)**: Matches approved scope; respects explicit exclusions; no speculative abstractions or unrequested features.
2. **Domain Modelling & Result Pattern (Gates B & C)**: Aggregate encapsulation, UUID v7 identity ownership, invariant enforcement, failure atomicity (no partial state mutation on failed operations), `Money` value semantics, `Result`/`Result<TValue>`/`Error` state validity, and centralised transport-independent domain errors.
3. **Code Design & Maintainability (Gate D)**: Smallest clear design protecting required behaviour; `internal` visibility by default; British English; zero temporary probes or unnecessary wrappers/parsers.
4. **Dependency & Architecture Discipline (Gate E)**: No unapproved NuGet packages; `Domain` has zero references to `Application`, `Infrastructure`, `Api`, ASP.NET Core, EF Core, `MediatR`, or `RabbitMQ.Client`; honest reporting of lightweight architecture checks.
5. **Meaningful Testing (Gate F)**: Tests protect real business invariants, boundary values, error codes, and state preservation after failure with genuine regression value (not superficial coverage padding).
6. **Infrastructure, Engineering Standards & PR Hygiene (Gates G, H & I)**: Applicable infrastructure failure modes covered; zero warnings/errors; zero diagnostic suppressions; clean diff and accurate documentation.

## Output Format & Objective Verdicts

Report findings with exact file paths and line numbers, grouped by severity (`Blocking / High` vs `Medium / Low`), and conclude with one of the three objective verdicts defined in `docs/QUALITY-GATES.md` Section 3:

- **`APPROVED`**: Every applicable quality gate passes, all requirements are met, verification is complete, and no known correctness, architecture, testing, or complexity issues remain. (Do not invent speculative notes merely to populate a notes section.)
- **`APPROVED WITH NOTES`**: Acceptable for the approved scope, with specific, evidenced, non-blocking limitations or intentionally deferred concerns explained clearly.
- **`NOT APPROVED`**: A requirement is missing, an invariant is unprotected, a correctness/architecture/complexity defect remains, or verification is incomplete.
