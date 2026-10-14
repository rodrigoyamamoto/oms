# Architecture Decision Records (ADRs)

## Purpose

This directory contains Architecture Decision Records (ADRs) that capture significant architectural and technical decisions made during the evolution of the OMS project.

## When to Record an ADR

Create an ADR before implementing changes that affect:

- Physical service boundaries (for example, modular monolith versus distributed services)
- Deployment topology and container orchestration
- Database schema ownership, transaction boundaries, or Outbox/Inbox mechanics
- Messaging topology, serialisation contracts, or idempotency strategies
- Cross-cutting concerns (authentication, observability, caching, or resilience pipelines)
- Addition or removal of major dependencies

## ADR Format

Each ADR should be created as `docs/adr/NNNN-short-title-in-kebab-case.md` (for example, `0001-independent-service-boundaries.md`) using the following structure:

```markdown
# ADR NNNN: Title

- **Status**: Proposed | Accepted | Superseded by ADR-XXXX | Deprecated
- **Date**: YYYY-MM-DD

## Context

Describe the problem, architectural forces, and constraints at play.

## Decision

State the chosen approach clearly and concisely.

## Consequences

### Positive
- Benefits and simplifications gained.

### Negative / Trade-offs
- Costs, risks, or operational complexities accepted.

## Alternatives Considered

Briefly note viable alternatives and why they were not chosen.
```

## Decision Log

| ADR | Title | Status |
| :--- | :--- | :--- |
| [ADR-0001](0001-independent-service-boundaries.md) | Independent Service Boundaries and Initial Solution Architecture | Accepted |
