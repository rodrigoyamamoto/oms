# ADR 0001: Independent Service Boundaries and Database-per-Service Topology

- **Status**: Accepted
- **Date**: 2026-10-14

## Context

OMS is an educational Order Management System designed to develop and demonstrate senior-level software engineering competencies in modern .NET 10 distributed SaaS applications.

Before creating application projects in `Oms.slnx`, we must decide how the four business capabilities—**Orders**, **Inventory**, **Payments**, and **Notifications**—are partitioned across deployable runtime units and persistent data stores.

Key architectural forces and constraints include:

- The system must provide realistic practice with distributed systems engineering problems: asynchronous messaging over `RabbitMQ.Client`, Transactional Outbox and Inbox patterns, idempotent consumers, eventual consistency, failure recovery, resilience (`Microsoft.Extensions.Http.Resilience`), and distributed observability (`OpenTelemetry`).
- The codebase must remain deliberately small, maintainable, and free of speculative abstractions or enterprise framework bloat (`.NET Aspire`, `MassTransit`, and direct `Polly` API usage are explicitly excluded).
- Development will proceed incrementally, implementing and verifying one service at a time.
- Local development must remain lightweight and self-contained via Docker and Docker Compose.

## Decision

We will structure OMS as **four independently deployable services** with **strict service boundaries and isolated data ownership**:

1. **Orders (`Oms.Orders.*`)** — ASP.NET Core Web API owning order lifecycle state and order line items in a dedicated `orders_db` PostgreSQL database.
2. **Inventory (`Oms.Inventory.*`)** — ASP.NET Core Web API owning stock availability and stock reservations in a dedicated `inventory_db` PostgreSQL database.
3. **Payments (`Oms.Payments.*`)** — ASP.NET Core Web API owning simulated payment processing and payment attempt audit records in a dedicated `payments_db` PostgreSQL database.
4. **Notifications (`Oms.Notifications.*`)** — .NET Background Worker sending transactional emails via `MailKit` and `Papercut SMTP`, using minimal dedicated persistence (`notifications_db`) only where required for reliable idempotent message processing.

The following boundary rules are mandatory:

- **No Shared Domain Entities**: Domain models are strictly private to their owning service.
- **No Cross-Service Database Access**: Services must never query, join, or mutate another service's database tables or schemas.
- **Database-per-Service Isolation**: Each service that requires persistent state owns its own logical PostgreSQL database, migrations, and Outbox/Inbox tables (`orders_db`, `inventory_db`, `payments_db`, `notifications_db`). During local development and integration testing, a single PostgreSQL container instance may host these separate logical databases to keep resource usage low while preserving complete schema and connection-string isolation.
- **Incremental Delivery**: Services will be built and verified one service at a time rather than scaffolding all four services simultaneously.

## Consequences

### Positive

- **Authentic Distributed Systems Practice**: Forces genuine handling of eventual consistency, partial failures, out-of-order/duplicate message delivery, Transactional Outbox/Inbox persistence, and distributed tracing across real process and network boundaries.
- **Autonomous Data Ownership**: Each service evolves its schema and EF Core migrations independently without risking breaking another service's queries or transactions.
- **Independent Deployment & Scaling**: API services (`Orders`, `Inventory`, `Payments`) and the background worker (`Notifications`) can be deployed, restarted, and scaled independently based on workload characteristics.
- **Failure Isolation**: A transient outage in `Notifications` or `Payments` does not prevent `Orders` from accepting new orders or `Inventory` from serving read requests.

### Negative / Trade-offs

- **No ACID Transactions Across Capabilities**: Multi-service workflows (such as reserving stock and authorising payment for an order) cannot use a single database transaction and instead require asynchronous coordination and compensating actions.
- **Operational & Testing Overhead**: Running and testing the full system requires managing multiple processes, multiple database schemas/migrations, and RabbitMQ exchanges/queues (mitigated locally by Docker Compose and in tests by Testcontainers).
- **Data Duplication by Reference**: Services reference external concepts by primitive identifiers (such as `OrderId` or `Sku`) and snapshot necessary values (such as `UnitPrice` on `OrderItem`) rather than relying on foreign-key joins.

## Alternatives Considered

### 1. Single-Process Modular Monolith

- **Description**: Host `Orders`, `Inventory`, `Payments`, and `Notifications` as separate modules inside a single ASP.NET Core host process, sharing a single PostgreSQL database (isolated by database schemas) and communicating via in-process module calls or an in-memory bus.
- **Why Rejected**: While a modular monolith is often a pragmatic starting point for commercial greenfield products, it does not exercise real cross-process failure modes, network partitions, independent deployment pipelines, or true broker-backed eventual consistency between independent runtimes—which are primary educational objectives of the OMS project.

### 2. Shared Database Across Multiple Service Processes

- **Description**: Run four separate service executables that read and write to a single shared PostgreSQL schema and share EF Core entity definitions.
- **Why Rejected**: Creates tight schema coupling, hidden contention, and unclear ownership of invariants, combining the operational complexity of distributed processes with the coupling of a monolith.
