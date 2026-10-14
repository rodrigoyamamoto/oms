# Order Management System (OMS)

OMS is an educational Order Management System designed to develop and practise senior-level software engineering skills in modern .NET SaaS applications.

## Current Repository Status

This repository currently provides the **engineering foundation, build standards, and AI-assisted development workflow** around an empty .NET 10 solution (`Oms.slnx`).

No business domains, application services, API endpoints, database schemas, or infrastructure containers have been implemented yet.

## Planned Capabilities

As the project progresses through subsequent tasks, the system is planned to cover:

- **Orders** — order lifecycle management, state transitions, and orchestration.
- **Inventory** — stock reservation, availability checks, and fulfilment adjustments.
- **Payments** — payment authorisation, capture, and failure handling.
- **Notifications** — transactional customer and operational notifications.

## Planned Technology Stack

| Area | Planned Technologies |
| :--- | :--- |
| **Runtime & Web** | .NET 10, ASP.NET Core |
| **Architecture & Patterns** | Domain-Driven Design (DDD), pragmatic Clean Architecture, CQRS, MediatR |
| **Data & Persistence** | Entity Framework Core, PostgreSQL, Redis |
| **Messaging & Reliability** | `RabbitMQ.Client`, Outbox / Inbox patterns, idempotent consumers |
| **Resilience** | `Microsoft.Extensions.Http.Resilience` |
| **Observability** | OpenTelemetry |
| **Notifications** | MailKit, Papercut SMTP |
| **Frontend** | React, TypeScript |
| **Testing** | xUnit, Testcontainers |
| **Containerisation** | Docker, Docker Compose (and Kubernetes in a later stage) |

### Explicit Technical Exclusions

To ensure deep understanding of underlying mechanics and standard .NET platform abstractions, the following are **explicitly excluded**:

- **.NET Aspire**
- **MassTransit** (messaging is implemented directly against `RabbitMQ.Client` with explicit Outbox/Inbox mechanics)
- **Direct Polly API usage** (resilience must be configured through `Microsoft.Extensions.Http.Resilience` and `Microsoft.Extensions.Resilience` abstractions)

## Open Architectural Decisions

The physical service boundaries (for example, modular monolith versus distributed microservices) and runtime deployment topology remain intentionally open architectural decisions to be recorded via Architecture Decision Records (ADRs) before implementation begins.

## Documentation Index

- [`AGENTS.md`](AGENTS.md) — Entry point and operating rules for AI coding agents.
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — Architectural principles, consistency boundaries, resilience, and exclusions.
- [`docs/ENGINEERING.md`](docs/ENGINEERING.md) — .NET 10 build configuration, coding standards, package management, and static analysis readiness.
- [`docs/TESTING.md`](docs/TESTING.md) — Testing strategy, test pyramid expectations, and verification discipline.
- [`docs/AI-WORKFLOW.md`](docs/AI-WORKFLOW.md) — Agent collaboration workflow, skills, reviewer subagent usage, and third-party skills policy.
- [`docs/adr/README.md`](docs/adr/README.md) — Architecture Decision Record guidelines and log.

## Getting Started

### Prerequisites

- .NET 10 SDK (`10.0.x`)

### Useful Commands

```bash
# Inspect SDK version
dotnet --info

# Build the solution
dotnet build Oms.slnx

# Verify code formatting (once projects are added)
dotnet format Oms.slnx --verify-no-changes
```
