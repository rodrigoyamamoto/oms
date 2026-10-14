# Testing Strategy & Verification Expectations

## Purpose

This document defines the testing strategy, planned tooling, and verification discipline for the OMS project.

> **Current Status**: The solution (`Oms.slnx`) currently contains no application or test projects. The practices below govern how tests will be structured and executed as capabilities are implemented.

## Planned Testing Stack

- **Test Framework**: **xUnit**
- **Integration Environment**: **Testcontainers** (running real PostgreSQL, RabbitMQ, Redis, and Papercut SMTP containers in Docker for integration tests)
- **Execution**: Standard .NET CLI (`dotnet test`)

## Testing Principles

### 1. Pragmatic Test Pyramid

- **Unit Tests**: Fast, isolated tests verifying domain aggregates, value objects, invariants, state transitions, and pure application logic without external I/O.
- **Integration Tests**: Verify database mappings (EF Core + PostgreSQL), Outbox/Inbox persistence, RabbitMQ message publishing/consumption, idempotency, and HTTP endpoints using ephemeral containers managed by Testcontainers.
- **End-to-End / Contract Checks**: Focused verification of critical cross-capability workflows (such as Order placement through Inventory reservation, Payment processing, and Notification dispatch).

### 2. Behaviour Over Implementation Details

- Test observable domain behaviour, state transitions, persisted outcomes, and published messages rather than internal private methods or excessive mock interactions.
- Prefer real infrastructure via Testcontainers over in-memory database substitutes (such as EF Core InMemory) whenever testing persistence, transactions, or concurrency constraints.

### 3. Determinism and Isolation

- Tests must be deterministic, independent of execution order, and safe to run in parallel where possible.
- Integration tests must clean up or isolate state per test case/collection so repeated runs yield identical results.
- Never rely on wall-clock `Thread.Sleep` for asynchronous messaging assertions; use bounded polling or completion signals.

## Naming and Organisation Conventions

- Test projects must end with `.UnitTests` or `.IntegrationTests` (or `.Tests`), which `Directory.Build.props` automatically configures with `<IsPackable>false</IsPackable>` and `<IsTestProject>true</IsTestProject>`.
- Test method names should clearly express the scenario, action, and expected outcome (for example, `PlaceOrder_WhenStockIsInsufficient_ShouldRejectOrder`).

## Verification Discipline

Both human engineers and AI agents must follow strict honesty when reporting verification status:

1. **Run Applicable Checks**: Execute `dotnet build Oms.slnx`, `dotnet build Oms.slnx -c Release`, `dotnet format Oms.slnx --verify-no-changes` (when source projects exist), and `dotnet test Oms.slnx` (when test projects exist).
2. **Never Fabricate Test or Build Results**: If no `.csproj` or test projects exist in the solution, or if Docker is unavailable for Testcontainers integration tests, state explicitly that compiler/analyser enforcement or tests were **not executed** or **not yet applicable**.
3. **Fail Fast on Warnings Everywhere**: `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and `<CodeAnalysisTreatWarningsAsErrors>true</CodeAnalysisTreatWarningsAsErrors>` are active in both `Debug` and `Release` builds. Fix the root cause of any warning or failure rather than suppressing diagnostics or swallowing exceptions.
