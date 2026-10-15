# AI Agent Instructions (`AGENTS.md`)

This file is the primary entry point for AI coding agents working in the OMS repository. Keep changes minimal, accurate, and aligned with the documented standards.

## Project State

- The solution root is `Oms.slnx` in the repository root directory.
- `Oms.Orders.Api` (`GET /health/live`, `POST /api/orders`, `GET /api/orders/{id}`, `Oms.Orders.Domain` model, `Oms.Orders.Application` CQRS handlers `CreateOrderCommand` and `GetOrderByIdQuery` via MediatR and `IOrderStore`, and `Oms.Orders.Infrastructure.Persistence` with `OrdersDbContext`, `EfOrderStore`, and `InitialOrdersPersistence` migration), local PostgreSQL `compose.yaml`, `Oms.Orders.UnitTests`, and `Oms.Orders.IntegrationTests` are implemented. No distributed messaging infrastructure or authentication exist yet.
- Do not invent or assume undocumented services, packages, or infrastructure.

## Core Guardrails

1. **Language & Tone**: Use professional British English in all documentation, comments, and pull request descriptions.
2. **Scope Control**: Implement only what is requested. Avoid speculative abstractions, premature generalisations, and unrequested dependencies.
3. **Explicit Exclusions**: Never introduce **.NET Aspire**, **MassTransit**, or **direct Polly API calls** (use `Microsoft.Extensions.Http.Resilience` when HTTP resilience is needed).
4. **Package Management**: All NuGet dependencies must use Central Package Management in `Directory.Packages.props`. Never add packages for features that do not yet exist.
5. **Change Governance**: Do not silently alter architectural decisions, install dependencies, modify agent instructions, or commit/push to Git without explicit justification and user approval.
6. **Zero Silent Diagnostic Suppression**: Compiler and analyser warnings are treated as errors across all build configurations (`Debug` and `Release`). Never suppress warnings (`NoWarn`, `WarningsNotAsErrors`, `#pragma warning disable`, `SuppressMessage`), downgrade diagnostic severities, or swallow exceptions to make a build pass without investigating the root cause and obtaining explicit approval.
7. **Meaningful, Risk-Based Testing**: Write behaviour-driven tests that protect real domain invariants, transactional atomicity, concurrency, idempotency, Outbox/Inbox reliability, and architectural boundaries (see [`docs/TESTING.md`](docs/TESTING.md)). Never write superficial tests merely to inflate coverage metrics, never claim mocked dependencies prove real infrastructure behaviour, and always report which guarantees your tests protect alongside any failure cases left untested.
8. **Mandatory Pre-Handoff Quality Gate**: Before producing a final handoff or updating a pull request, execute the 7-step quality gate sequence in [`docs/QUALITY-GATES.md`](docs/QUALITY-GATES.md) (Implementation -> Self-Review -> Behavioural Verification -> Independent Read-Only Review -> Findings Resolution -> Final Quality Gate -> Handoff with the `Engineering Quality Gate` evidence section).
9. **Honest Verification**: Never claim that builds, analysers, or tests passed without executing them and observing evidence. Clearly distinguish between executed checks and checks that are not yet applicable.

## Reference Documentation

Instead of duplicating rules here, consult the authoritative documents below:

- **Architecture & Boundaries**: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md)
- **Engineering & Build Standards**: [`docs/ENGINEERING.md`](docs/ENGINEERING.md)
- **Engineering Quality Gates**: [`docs/QUALITY-GATES.md`](docs/QUALITY-GATES.md)
- **Testing & Verification Expectations**: [`docs/TESTING.md`](docs/TESTING.md)
- **Agent Collaboration, Skills & Review Workflow**: [`docs/AI-WORKFLOW.md`](docs/AI-WORKFLOW.md)
- **Architecture Decision Records (ADRs)**: [`docs/adr/README.md`](docs/adr/README.md)

## Repository Skills & Subagents

- Use `/verify-change` (`.devin/skills/verify-change/SKILL.md`) to verify modified files, build health, formatting, and test outcomes with evidence.
- Use `/review-change` (`.devin/skills/review-change/SKILL.md`) or delegate to the read-only `reviewer` subagent profile (`.devin/agents/reviewer.md`) for independent pre-handoff code and configuration review.
