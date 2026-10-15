# Order Management System (OMS)

OMS is an educational Order Management System designed to develop and practise senior-level software engineering skills in modern .NET SaaS applications.

## Current Repository Status

This repository currently provides the **engineering foundation, build standards, AI-assisted development workflow, local Docker Compose PostgreSQL environment, and the runnable `Orders` HTTP API (`Oms.Orders.Api`, `Oms.Orders.UnitTests`, `Oms.Orders.IntegrationTests`)** inside `Oms.slnx`.

- **Implemented (`Orders` Service)**:
  - ASP.NET Core 10 Minimal API (`GET /health/live`, `POST /api/orders`, `GET /api/orders/{id}`) with RFC 9457 ProblemDetails error responses (`code` extension).
  - Domain model (`Order`, `OrderItem`, `OrderItemDraft`, `OrderStatus`, `Money`, and internally owned `Result`/`Result<TValue>`/`Error`).
  - MediatR CQRS application layer (`CreateOrderCommand`, `GetOrderByIdQuery`, `OrderDetails`, `IOrderStore`).
  - EF Core 10 PostgreSQL persistence (`OrdersDbContext`, `EfOrderStore`, and `InitialOrdersPersistence` migration).
  - Local PostgreSQL Docker Compose setup (`compose.yaml` and `.env.example`).
  - Unit, visibility, and Testcontainers PostgreSQL + HTTP integration tests.
- **Not Yet Implemented (Planned for Subsequent Tasks)**:
  - Authentication and authorisation (this milestone is a local educational setup and is not ready for public internet exposure).
  - Transactional Outbox/Inbox tables and RabbitMQ distributed messaging (`OrderPlaced` / `ReserveStock`).
  - `Inventory`, `Payments`, and `Notifications` services, and the React frontend.

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

## Architectural Direction

- **Accepted Architecture ([ADR-0001](docs/adr/0001-independent-service-boundaries.md) & [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md))**:
  - Four independently deployable services (`Oms.Orders.Api`, `Oms.Inventory.Api`, `Oms.Payments.Api`, and `Oms.Notifications.Worker`) with isolated PostgreSQL databases per service, implemented incrementally one service at a time.
  - Single deployable assembly per service with enforced logical layers (`Domain`, `Application`, `Infrastructure`, `Api` / `Worker`) and automated architecture tests (Option B).
  - Orders-mediated asynchronous process coordination over RabbitMQ separating integration commands (`ReserveStock`, `RequestPayment`, `CommitStock`, `ReleaseStock`) from integration events (`StockReserved`, `PaymentSucceeded`, `OrderConfirmed`, `OrderCancelled`) with explicit EF Core + Transactional Outbox/Inbox boundaries (Option 1).
  - Zero shared projects initially; integration contract packaging will be reconsidered when `Inventory` is introduced.

## Documentation Index

- [`AGENTS.md`](AGENTS.md) — Entry point and operating rules for AI coding agents.
- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — Architectural principles, consistency boundaries, resilience, and exclusions.
- [`docs/ENGINEERING.md`](docs/ENGINEERING.md) — .NET 10 build configuration, coding standards, Result pattern, package management, and static analysis readiness.
- [`docs/QUALITY-GATES.md`](docs/QUALITY-GATES.md) — Pre-handoff engineering quality gates, review verdicts, and evidence requirements.
- [`docs/TESTING.md`](docs/TESTING.md) — Testing strategy, test pyramid expectations, and verification discipline.
- [`docs/AI-WORKFLOW.md`](docs/AI-WORKFLOW.md) — Agent collaboration workflow, skills, reviewer subagent usage, and third-party skills policy.
- [`docs/adr/README.md`](docs/adr/README.md) — Architecture Decision Record guidelines and log.

---

## Running OMS Locally (Windows, PowerShell, JetBrains Rider & Docker Desktop)

### Prerequisites

- .NET 10 SDK (`10.0.x`)
- Docker Desktop for Windows (running)
- PowerShell 7+ or Windows PowerShell
- JetBrains Rider (optional; the API can be run from either Rider or the .NET CLI)

### 1. Configure Local PostgreSQL Credentials (`.env` and .NET User Secrets)

Neither `compose.yaml` nor `appsettings*.json` contains database passwords or connection strings. For local development, choose a local password for your PostgreSQL container and configure it in two untracked locations:

1. **Docker Compose `.env` file** (gitignored at the repository root):
   ```powershell
   $localPgPassword = "YourLocalDevPassword_ChangeMe!"

   (Get-Content -Raw .\.env.example) -replace '<replace_with_local_postgres_password>', $localPgPassword | Set-Content -NoNewline .\.env
   ```
   *(Note: `.env` is read by `docker compose` to initialise the `postgres:17.2-alpine` container on loopback port `127.0.0.1:54329`; `.env` is **not** read automatically by the .NET application.)*

2. **.NET User Secrets** (stored outside the repository in `%APPDATA%\Microsoft\UserSecrets\`, automatically loaded by `WebApplication.CreateBuilder` when `ASPNETCORE_ENVIRONMENT=Development` in both the .NET CLI and JetBrains Rider):
   ```powershell
   dotnet user-secrets set "ConnectionStrings:OrdersDatabase" "Host=localhost;Port=54329;Database=orders_db;Username=postgres;Password=$localPgPassword" --project src/Orders/Oms.Orders.Api/Oms.Orders.Api.csproj
   ```

### 2. Start PostgreSQL and Check Container Health

```powershell
docker compose up -d
docker compose ps
```

Wait a few seconds until `STATUS` shows `Up ... (healthy)`.

### 3. Restore Packages, Restore Local Tools (`dotnet-ef`), and Apply Migrations

Because `dotnet-ef` uses the `Oms.Orders.Api` startup project in `Development`, it automatically reads `ConnectionStrings:OrdersDatabase` from .NET User Secrets:

```powershell
dotnet restore Oms.slnx
dotnet tool restore

dotnet ef database update --project src/Orders/Oms.Orders.Api/Oms.Orders.Api.csproj --startup-project src/Orders/Oms.Orders.Api/Oms.Orders.Api.csproj
```

### 4. Run `Oms.Orders.Api` on `http://localhost:5080`

#### Option A — From PowerShell (.NET CLI)

```powershell
dotnet run --project src/Orders/Oms.Orders.Api/Oms.Orders.Api.csproj
```

`Properties/launchSettings.json` sets `ASPNETCORE_ENVIRONMENT=Development` and binds `http://localhost:5080`, automatically loading your User Secrets connection string.

#### Option B — From JetBrains Rider

1. Open `Oms.slnx` in JetBrains Rider.
2. Select the **`Oms.Orders.Api: http`** run configuration (`http://localhost:5080`, `ASPNETCORE_ENVIRONMENT=Development`).
3. Because .NET User Secrets were set in Step 1 (or can be edited via right-clicking `Oms.Orders.Api` in Rider $\rightarrow$ **Tools** $\rightarrow$ **Manage .NET User Secrets**), no credentials need to be typed into Rider run configuration files.
4. Click **Run** (or **Debug**). You can also execute requests directly from [`src/Orders/Oms.Orders.Api/Oms.Orders.Api.http`](src/Orders/Oms.Orders.Api/Oms.Orders.Api.http) using Rider's built-in HTTP Client.

### 5. Exercise the API from PowerShell

Open a second PowerShell terminal and run:

```powershell
# 1. Verify the liveness endpoint (200 OK -> Healthy)
Invoke-RestMethod -Uri "http://localhost:5080/health/live" -Method Get

# 2. Create an Order with two items, different quantities, and 3-decimal prices (201 Created)
$createOrderBody = @{
    items = @(
        @{
            productId       = "0199b0b1-7040-7890-8abc-0123456789ab"
            quantity        = 2
            unitPriceAmount = 10.125
            currency        = "GBP"
        },
        @{
            productId       = "0199b0b1-7040-7890-8abc-0123456789ac"
            quantity        = 3
            unitPriceAmount = 0.625
            currency        = "GBP"
        }
    )
} | ConvertTo-Json -Depth 4

$createResponse = Invoke-WebRequest -Uri "http://localhost:5080/api/orders" -Method Post -ContentType "application/json" -Body $createOrderBody -UseBasicParsing
$createResponse.StatusCode
$createResponse.Headers["Location"]

$createdOrder = $createResponse.Content | ConvertFrom-Json
$orderId = $createdOrder.orderId
Write-Output "Created Order ID: $orderId"

# 3. Retrieve the created Order by ID (200 OK)
$order = Invoke-RestMethod -Uri "http://localhost:5080/api/orders/$orderId" -Method Get
$order | ConvertTo-Json -Depth 5
```

### 6. Verify Persistence Across API Restarts

1. Stop the running `Oms.Orders.Api` process (`Ctrl+C` in the API terminal or **Stop** in Rider).
2. Start `Oms.Orders.Api` again (`dotnet run --project src/Orders/Oms.Orders.Api/Oms.Orders.Api.csproj`).
3. Re-run `Invoke-RestMethod -Uri "http://localhost:5080/api/orders/$orderId" -Method Get` using the **same `$orderId`**.
4. Confirm that the exact order and its line items (`totalAmount = 22.125`, `currency = "GBP"`) are returned from the `oms_postgres_data` Docker volume.

### 7. Stopping the Environment Cleanly

```powershell
# Stop the PostgreSQL container while preserving the oms_postgres_data volume (non-destructive)
docker compose down
```

> **Destructive Command Warning**: Running `docker compose down -v` deletes the named `oms_postgres_data` Docker volume and permanently erases all locally persisted orders. Only use `-v` when you intentionally want to wipe the local database and re-apply migrations from scratch.

---

## Build, Format & Test Commands

```bash
# Build the solution (Debug and Release)
dotnet build Oms.slnx -c Debug
dotnet build Oms.slnx -c Release

# Verify code formatting
dotnet format Oms.slnx --verify-no-changes

# Run unit and Testcontainers PostgreSQL integration tests (requires Docker)
dotnet test Oms.slnx -c Release --no-build
```
