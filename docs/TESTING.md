# Testing Strategy & Verification Expectations

## Purpose

This document defines the behaviour-driven, risk-based testing strategy, planned tooling, and verification discipline for the OMS project.

Tests in OMS must provide **meaningful evidence of correctness**, not merely inflate line or branch coverage metrics. Every non-trivial test must have a clear, defensible purpose: identifying the specific business requirement, domain invariant, failure mode, or architectural boundary it protects, and failing reliably if that guarantee is broken.

> **Current Status**: The `Orders` service foundation (`Oms.Orders.Api`), domain model (`Order`, `OrderItem`, `OrderItemDraft`, `OrderStatus`, `Money`, `Result`, `Error`), EF Core 10 + PostgreSQL persistence (`OrdersDbContext` and `InitialOrdersPersistence` migration), unit/visibility tests (`Oms.Orders.UnitTests`), and real Testcontainers PostgreSQL + host integration tests (`Oms.Orders.IntegrationTests`) are implemented in `Oms.slnx`. Application-layer tests and RabbitMQ messaging integration tests will be added as capabilities are implemented incrementally.

---

## Planned Testing Stack

- **Test Framework**: **xUnit**
- **Integration Environment**: **Testcontainers** (running real PostgreSQL, RabbitMQ, and Papercut SMTP containers—and Redis in a later stage—in Docker for integration tests)
- **Architecture Verification**: Direct source/dependency inspection during review plus lightweight reflection checks in `*.UnitTests` where they provide concrete value
- **Execution**: Standard .NET CLI (`dotnet test`)

---

## Core Testing Principles

### 1. Test Business Behaviour and Invariants, Not Implementation Details

- **Focus on Observable Outcomes**: Assert state transitions, domain invariants, persisted database state, HTTP response contracts, and emitted Outbox/integration messages rather than internal private methods, helper call counts, or trivial getters/setters.
- **Do Not Test the Framework**: Never write tests that merely verify C# auto-properties, EF Core's built-in change tracker in isolation, or MediatR's pipeline dispatch mechanics without exercising real OMS business rules.
- **Detect Meaningful Regressions**: A test is valuable only if it fails when a genuine business rule, consistency guarantee, or contract is broken, while remaining resilient to internal refactoring.

### 2. Risk-Based Prioritisation

Testing effort must be concentrated on **high-risk engineering scenarios** where bugs cause data corruption, financial/inventory discrepancies, or silent distributed failures:

1. **Data Integrity & Transactional Atomicity**: Verifying that aggregate state changes and `outbox_messages` (plus `inbox_messages` deduplication entries) commit or roll back together in a single PostgreSQL transaction, and that database `CHECK` and uniqueness constraints reject invalid state.
2. **Concurrent Operations & Race Conditions**: Verifying that concurrent requests (for example, two simultaneous `ReserveStock` commands competing for the last unit of a SKU) are serialised or rejected via optimistic concurrency (`xmin` / row version) without overselling.
3. **Idempotency & Duplicate Message Delivery**: Verifying that redelivering `ReserveStock`, `StockReserved`, `RequestPayment`, `PaymentSucceeded`, or `OrderConfirmed` is safely deduplicated via the Transactional Inbox and aggregate status guards without double-reserving stock, double-charging payments, or sending duplicate notifications.
4. **Transactional Outbox & Inbox Correctness**: Verifying that unpublished Outbox rows are reliably dispatched to RabbitMQ with publisher confirmations, marked processed, and that consumer Inbox processing commits atomically before `BasicAckAsync`.
5. **Distributed Consistency & Compensation**: Verifying both happy-path state progression (`Pending -> AwaitingStock -> AwaitingPayment -> Confirmed`) and compensating flows (such as `PaymentFailed` triggering `ReleaseStock` and transitioning `Order` to `Cancelled`).
6. **Service Failures, Retries & Recovery**: Verifying bounded retry behaviour on transient database/broker/SMTP errors and routing of poison or unrecoverable messages to Dead-Letter Queues (`*.dlq`) without infinite loops.
7. **Integration Contract Compatibility**: Verifying that integration commands and events serialise and deserialise accurately across service boundaries without breaking consumers.
8. **Architectural Dependency Boundaries**: Verifying that single-assembly logical layers (`Domain`, `Application`, `Infrastructure`, `Api` / `Worker`) respect strict dependency directions.

### 3. Explicit Test Categories & No False Confidence

Never conflate test categories or claim that a mocked dependency proves real infrastructure behaviour:

| Test Category | Scope & Infrastructure | What It Proves | What It Does **Not** Prove |
| :--- | :--- | :--- | :--- |
| **Unit Tests** (`*.UnitTests`) | In-process, zero external I/O. Exercises `Domain` aggregates, value objects, invariants, and pure `Application` logic. | Domain rules, state machine transitions, guard clauses, and calculation accuracy. | Database mappings, SQL constraints, transaction atomicity, or RabbitMQ serialisation. |
| **Architecture Checks** (`*.UnitTests` & Code Review) | Lightweight reflection assertions over `Oms.<Service>.Api` / `Worker` combined with direct source/dependency inspection. | Concrete metadata rules (such as `Domain` types remaining `internal`). | Complete method-body dependency verification (standard reflection only inspects signatures; method-body dependencies are verified via source review). |
| **Integration Tests** (`*.IntegrationTests`) | Real PostgreSQL, RabbitMQ, and Papercut SMTP containers via **Testcontainers**. | EF Core mappings, migrations, `CHECK` / unique constraints, optimistic concurrency, atomic Outbox/Inbox persistence, AMQP routing, and HTTP endpoint contracts. | Cross-service multi-process orchestration unless multiple services are under test. |
| **End-to-End / Workflow Tests** | Multi-service workflow verification across `Orders`, `Inventory`, `Payments`, and `Notifications`. | Complete distributed order fulfilment and compensation flows across real broker and database boundaries. | Exhaustive edge-case permutations (which belong in unit/integration tests). |

### 4. Real Infrastructure Over In-Memory Substitutes

- **Use PostgreSQL via Testcontainers (Docker Prerequisite)**: When testing EF Core repositories/contexts, migrations, transactions, concurrency tokens (`xmin`), raw SQL, or database constraints, always use a real PostgreSQL container via Testcontainers (pinned to `postgres:17.2-alpine`). A running Docker daemon is required to execute `*.IntegrationTests`; if Docker is unavailable, report the environmental blocker explicitly rather than skipping tests silently or substituting `InMemory`.
- **Avoid EF Core InMemory for Persistence Claims**: The EF Core InMemory provider does not enforce relational constraints, foreign keys, PostgreSQL transaction semantics, or concurrency tokens; it must not be used to claim database persistence correctness.
- **Judicious Use of Mocks and Substitutes**: Mocks/substitutes are tools for isolating pure application orchestration or simulating deterministic external failure modes (such as forcing a simulated payment gateway timeout)—not the default testing strategy. Never mock away the very database transaction, query, or messaging behaviour the test is meant to verify.

### 5. Lightweight Architecture Discipline (Option B)

Because OMS uses a **single deployable assembly per service** ([ADR-0001](adr/0001-independent-service-boundaries.md)), logical layers (`Domain`, `Application`, `Infrastructure`, `Api` / `Worker`) share one `.csproj`:

- **Layer Dependency Rules**:
  - Types in `*.Domain.*` must not depend on `*.Application.*`, `*.Infrastructure.*`, `*.Api.*`, ASP.NET Core, `Microsoft.EntityFrameworkCore`, `MediatR`, or `RabbitMQ.Client`.
  - Types in `*.Application.*` must not depend on `*.Infrastructure.*` or `*.Api.*`.
  - Domain, application, and infrastructure implementation types remain `internal` unless explicitly required to be `public`.
- **Pragmatic Verification & Honest Limitations**:
  - Keep automated architecture assertions in `*.UnitTests` small and focused on concrete rules that standard reflection can reliably check without custom infrastructure (such as verifying that all `Domain` types are `internal`).
  - Do **not** build custom IL/bytecode parsers or maintain complex reflection frameworks in test projects. Because standard .NET reflection over type signatures cannot reliably detect every dependency inside method bodies, layer dependency rules are verified through direct source-code and `using`-directive inspection during the mandatory [`docs/QUALITY-GATES.md`](QUALITY-GATES.md) review.

### 6. Code Coverage as a Diagnostic Metric, Not a Target

- Code coverage is a diagnostic signal used to spot untested critical paths—not a primary quality KPI.
- Do **not** introduce arbitrary coverage percentage gates or write superficial tests (testing DTO properties, constructors, or trivial pass-through code) merely to inflate coverage numbers or satisfy SonarQube metrics.
- A smaller suite of high-signal behavioural, concurrency, idempotency, and integration tests is strictly preferred over a high-coverage suite of brittle, mock-heavy unit tests.

### 7. Determinism and Isolation

- Tests must be deterministic, independent of execution order, and isolated per test case or collection so repeated runs yield identical results.
- Never rely on wall-clock `Thread.Sleep` for asynchronous Outbox or RabbitMQ assertions; use bounded polling with short intervals or deterministic completion signals.

---

## Naming and Organisation Conventions

- Test projects must end with `.UnitTests` or `.IntegrationTests` (or `.Tests`), which `Directory.Build.props` automatically configures with `<IsPackable>false</IsPackable>` and `<IsTestProject>true</IsTestProject>`.
- Test method names must use `PascalCase` without underscores (satisfying Roslyn's `CA1707` rule) and clearly express the scenario, action, and expected business or technical guarantee (for example, `ReserveStockWhenConcurrentRequestsExceedAvailableQuantityShouldAllowOnlyOneReservation`).

---

## Verification & Reporting Discipline

Both human engineers and AI agents must follow strict honesty when reporting test and verification status:

1. **Run Applicable Checks**: Execute `dotnet build Oms.slnx`, `dotnet build Oms.slnx -c Release`, `dotnet format Oms.slnx --verify-no-changes` (when source projects exist), and `dotnet test Oms.slnx -c Release --no-build` (when test projects exist).
2. **Explain What Each Test Protects**: In implementation tasks and pull requests, agents must explicitly state:
   - Which business behaviours, invariants, concurrency/idempotency guarantees, or architectural rules the added tests protect.
   - Which test categories were executed (unit, architecture, real-infrastructure integration).
   - **Important failure cases or scenarios intentionally left untested** (for example, if Docker was unavailable or a cross-service scenario is deferred to a later task).
3. **Never Fabricate Test or Build Results**: If no `.csproj` or test projects exist in the solution, or if Docker is unavailable for Testcontainers integration tests, state explicitly that compiler/analyser enforcement or integration tests were **not executed** or **not yet applicable**.
4. **Fail Fast on Warnings Everywhere**: `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and `<CodeAnalysisTreatWarningsAsErrors>true</CodeAnalysisTreatWarningsAsErrors>` are active in both `Debug` and `Release` builds. Fix the root cause of any warning or failure rather than suppressing diagnostics or swallowing exceptions.
