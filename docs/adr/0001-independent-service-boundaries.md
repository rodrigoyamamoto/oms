# ADR 0001: Independent Service Boundaries and Initial Solution Architecture

- **Status**: Accepted
- **Date**: 2026-10-14

## Context

OMS is an educational Order Management System designed to develop and demonstrate senior-level software engineering competencies in modern .NET 10 distributed SaaS applications.

Before creating application projects in `Oms.slnx`, we must decide how the four business capabilities—**Orders**, **Inventory**, **Payments**, and **Notifications**—are partitioned across deployable runtime units and persistent data stores, how internal layers are packaged into .NET assemblies, and how cross-service workflows are coordinated.

Key architectural forces and constraints include:

- The system must provide realistic practice with distributed systems engineering problems: asynchronous messaging over `RabbitMQ.Client`, Transactional Outbox and Inbox patterns, idempotent consumers, eventual consistency, failure recovery, outgoing HTTP resilience (`Microsoft.Extensions.Http.Resilience`), and distributed observability (`OpenTelemetry`).
- The codebase must remain deliberately small, maintainable, and free of speculative abstractions or enterprise framework bloat (`.NET Aspire`, `MassTransit`, and direct `Polly` API usage are explicitly excluded).
- Our build baseline enforces `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and `<GenerateDocumentationFile>true</GenerateDocumentationFile>` without `CS1591` suppression, meaning cross-assembly `public` types require XML documentation comments whereas `internal` types within an assembly do not.
- Development will proceed incrementally, implementing and verifying one service at a time.
- Local development must remain lightweight and self-contained via Docker and Docker Compose.

## Decision

We will structure OMS around the following **approved architectural decisions**:

### 1. Four Independently Deployable Services with Database-per-Service Isolation

1. **Orders (`Oms.Orders.Api`)** — ASP.NET Core Web API owning order lifecycle state and order line items in a dedicated `orders_db` PostgreSQL database.
2. **Inventory (`Oms.Inventory.Api`)** — ASP.NET Core Web API owning stock availability and stock reservations in a dedicated `inventory_db` PostgreSQL database.
3. **Payments (`Oms.Payments.Api`)** — ASP.NET Core Web API owning simulated payment processing and payment attempt audit records in a dedicated `payments_db` PostgreSQL database.
4. **Notifications (`Oms.Notifications.Worker`)** — .NET Background Worker sending transactional emails via `MailKit` and `Papercut SMTP`, using minimal dedicated persistence (`notifications_db`) only where required for reliable idempotent message processing.

Mandatory boundary rules:

- **No Shared Domain Entities**: Domain models are strictly private to their owning service.
- **No Cross-Service Database Access**: Services must never query, join, or mutate another service's database tables or schemas.
- **Database-per-Service Isolation**: Each service that requires persistent state owns its own logical PostgreSQL database, migrations, and Outbox/Inbox tables (`orders_db`, `inventory_db`, `payments_db`, `notifications_db`). During local development and integration testing, a single PostgreSQL container instance may host these separate logical databases to keep resource usage low while preserving complete schema and connection-string isolation.
- **Incremental Delivery**: Services will be built and verified one service at a time rather than scaffolding all four services simultaneously.

### 2. Single Deployable Assembly per Service with Logical Layers (Option B)

Each service is packaged as a **single deployable .NET project** (`Oms.Orders.Api`, `Oms.Inventory.Api`, `Oms.Payments.Api`, `Oms.Notifications.Worker`), accompanied by `Oms.<Service>.UnitTests` and `Oms.<Service>.IntegrationTests`.

- Clean Architecture layers (`Domain`, `Application`, `Infrastructure`, `Api` / `Worker`) are organised as explicit folders and namespaces within the service assembly.
- Domain, application, and infrastructure types default to `internal` visibility (with `InternalsVisibleTo` granted to the service's test projects), keeping implementation details encapsulated and avoiding unnecessary `CS1591` public XML documentation noise across artificial class-library boundaries.
- Layer dependency rules (for example, `Domain` must not reference `Infrastructure`, EF Core, or `RabbitMQ.Client`) are enforced by automated architecture tests in `*.UnitTests`.

### 3. Orders-Mediated Process Coordination (Option 1)

Cross-service order fulfilment uses **Orders-mediated process coordination** over RabbitMQ without a heavyweight Saga framework:

- The `Order` aggregate in `Orders` acts as a lightweight stateful coordinator via its status transitions (`Pending -> AwaitingStock -> AwaitingPayment -> Confirmed`, or `Cancelled` / `Failed`).
- `Orders` issues targeted **integration commands** (`ReserveStock`, `ReleaseStock`, `CommitStock` to `Inventory`; `RequestPayment` to `Payments`) and reacts to published **integration events** (`StockReserved`, `StockReservationFailed` from `Inventory`; `PaymentSucceeded`, `PaymentFailed` from `Payments`).
- `Orders` publishes terminal lifecycle **integration events** (`OrderConfirmed`, `OrderCancelled`) consumed by `Notifications` (and by `Inventory` when committing/releasing stock if event-driven, or via explicit commands). This keeps `Inventory` unaware of payment details and keeps `Payments` unaware of warehouse inventory.

### 4. Zero Shared Projects Initially

- We will start Service 1 (`Orders`) with **zero shared `.csproj` projects** (no `Oms.Common`, `Oms.BuildingBlocks`, or `Oms.Contracts`).
- Integration message contract packaging (local contract records per service vs a dependency-free `Oms.Contracts` project) will be reconsidered when the second service (`Inventory`) is introduced.

## Consequences

### Positive

- **Authentic Distributed Systems Practice**: Forces genuine handling of eventual consistency, partial failures, out-of-order/duplicate message delivery, Transactional Outbox/Inbox persistence, and distributed tracing across real process and network boundaries.
- **Autonomous Data Ownership & Clean Encapsulation**: Each service evolves its schema, migrations, and `internal` domain types independently without risking breaking another service or bloating the solution with 20+ class libraries.
- **Clear Workflow Visibility**: Orders-mediated coordination keeps the order fulfilment state machine explicit in one bounded context (`Orders`) while keeping `Inventory` and `Payments` decoupled from one another.
- **Failure Isolation**: A transient outage in `Notifications` or `Payments` does not prevent `Orders` from accepting new orders or `Inventory` from serving read requests.

### Negative / Trade-offs

- **No ACID Transactions Across Capabilities**: Multi-service workflows cannot use a single database transaction and instead require asynchronous coordination, Outbox/Inbox persistence, and compensating actions (such as `ReleaseStock` when payment fails).
- **Architecture Enforcement via Tests**: Because each service uses a single deployable assembly, inner-layer dependency discipline relies on automated architecture tests in `*.UnitTests` rather than separate `.csproj` references.
- **Operational & Testing Overhead**: Running and testing the full system requires managing multiple processes, multiple database schemas/migrations, and RabbitMQ exchanges/queues (mitigated locally by Docker Compose and in tests by Testcontainers).

## Alternatives Considered

### 1. Single-Process Modular Monolith

- **Description**: Host `Orders`, `Inventory`, `Payments`, and `Notifications` as separate modules inside a single ASP.NET Core host process sharing a single PostgreSQL database.
- **Why Rejected**: Does not exercise real cross-process failure modes, network partitions, independent deployment pipelines, or broker-backed eventual consistency between independent runtimes.

### 2. Four Assemblies per Service (Option A)

- **Description**: Split each service into `.Domain`, `.Application`, `.Infrastructure`, and `.Api` class libraries (up to 24 `.csproj` files including tests).
- **Why Rejected**: Introduces high solution ceremony for a small educational project and forces internal handlers, repositories, and EF Core configurations to become `public` across assembly boundaries, conflicting with our strict `CS1591` XML documentation policy.

### 3. Pure Event Choreography Without Orders Mediation (Option 2)

- **Description**: Have `Payments` subscribe directly to `Inventory`'s `StockReserved` event (carrying monetary totals through `Inventory`) and have `Inventory` subscribe directly to `Payments`'s `PaymentFailed` event.
- **Why Rejected**: Leaks payment/monetary data into `Inventory` messages and couples `Inventory` and `Payments` directly to each other's events rather than routing workflow decisions through `Orders`.
