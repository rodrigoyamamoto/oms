# Architecture Overview

## Purpose

OMS is an educational Order Management System built to practise senior-level software engineering in modern .NET SaaS applications. This document defines the architectural principles, planned capabilities, technical constraints, and open decisions that govern future implementation work.

> **Current Status**: Only the repository foundation (`Oms.slnx` and build configuration) exists at this stage. No domain models, application handlers, API endpoints, or infrastructure components have been implemented yet.

## Planned Business Capabilities

The system will model four primary business capabilities:

1. **Orders** — Order placement, validation, lifecycle state transitions, and saga/process coordination.
2. **Inventory** — Stock tracking, reservation management, and release/commit operations.
3. **Payments** — Payment intent creation, authorisation, settlement, and compensation handling.
4. **Notifications** — Customer and operational notifications triggered by domain or integration events via SMTP (`MailKit` with `Papercut SMTP` in local development).

## Architectural Principles

### Pragmatic Clean Architecture & DDD

- **Domain-Centric Modelling**: Use Domain-Driven Design (DDD) tactical patterns (aggregates, entities, value objects, domain events) where domain complexity warrants them.
- **Pragmatic Layering**: Separate domain logic and application use cases from external infrastructure (persistence, messaging, HTTP, SMTP) without introducing ceremonial pass-through layers or speculative abstractions.
- **CQRS with MediatR**: Separate command handling (state mutation) from query handling (read projections) using `MediatR` where appropriate. Keep handlers focused on a single responsibility.
- **Deliberate Simplicity**: The codebase must remain deliberately small and comprehensible. Every abstraction must justify its existence through current requirements rather than hypothetical future needs.

### Consistency, Messaging, and Idempotency

- **Transactional Boundaries**: Each aggregate defines a transactional consistency boundary persisted via Entity Framework Core and PostgreSQL.
- **Eventual Consistency Across Boundaries**: Cross-capability coordination will rely on asynchronous messaging over RabbitMQ using `RabbitMQ.Client` directly.
- **Transactional Outbox & Inbox**:
  - **Outbox**: Domain/integration messages must be persisted in the same database transaction as the state change before being published to RabbitMQ, preventing dual-write inconsistencies.
  - **Inbox / Idempotent Consumers**: Message consumers must be idempotent and tolerate at-least-once delivery, duplicate messages, and out-of-order arrival.

### Resilience and Observability

- **Resilience**: Outbound HTTP communication and transient fault handling must use `Microsoft.Extensions.Http.Resilience` (and underlying `Microsoft.Extensions.Resilience` abstractions).
- **Caching**: Distributed caching and short-lived coordination state are planned to use Redis where required.
- **Observability**: Distributed tracing, metrics, and structured logging will be instrumented via OpenTelemetry across HTTP boundaries, database operations, and RabbitMQ message flows.

## Explicit Architectural Exclusions

The following libraries and frameworks are **prohibited** in this repository:

| Excluded Technology | Rationale & Required Alternative |
| :--- | :--- |
| **.NET Aspire** | Excluded so container orchestration, service discovery, and OpenTelemetry wiring are configured explicitly via standard .NET, Docker Compose, and Kubernetes manifests. |
| **MassTransit** | Excluded so asynchronous messaging, topology configuration, Outbox/Inbox persistence, and idempotency are implemented directly with `RabbitMQ.Client`. |
| **Direct Polly API usage** | Excluded in favour of the standard `Microsoft.Extensions.Http.Resilience` pipeline configuration APIs. |

## Open Architectural Decisions

The following decisions remain intentionally unresolved at this stage and must be evaluated and recorded in [`docs/adr/`](adr/README.md) before creating application projects:

1. **Physical Service Boundaries**: Whether capabilities (`Orders`, `Inventory`, `Payments`, `Notifications`) are deployed initially as a modular monolith with strict module boundaries or as separate deployable services.
2. **Deployment Topology**: How services, PostgreSQL databases/schemas, RabbitMQ exchanges/queues, and the React frontend are packaged in Docker Compose and, in a later stage, Kubernetes.
3. **Contract Evolution & Schema Management**: How integration event schemas and EF Core migrations are versioned and applied across environments.
