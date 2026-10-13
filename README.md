# Gameya Backend

Gameya is a .NET 8 REST API for managing Egyptian rotating savings associations. It uses Clean Architecture, tactical DDD for financial workflows, CQRS with MediatR, SQL Server/EF Core, ASP.NET Core Identity, JWT access tokens with rotating refresh tokens, Hangfire, SMTP email, PDF reports, Serilog, FluentValidation, and Swagger.

## Solution layout

- `Gameya.Domain` — dependency-free aggregates, entities, value objects, rules, events, and exceptions.
- `Gameya.Application` — commands, queries, validators, DTOs, pipeline behavior, and ports.
- `Gameya.Infrastructure` — EF Core, SQL Server migrations, Identity, JWT, SMTP, PDF generation, audit, and Hangfire.
- `Gameya.API` — controllers, authorization policies, Problem Details, security middleware, and composition root.
- `Gameya.Tests` — unit, integration, persistence-invariant, and architecture tests.

The architecture tests prevent Domain from depending on EF Core, Identity, Infrastructure, or API, and prevent Application from depending on Infrastructure or API.

## Run locally

Prerequisites: .NET 8-compatible SDK and SQL Server/LocalDB.

```powershell
dotnet restore Gameya.sln
dotnet ef database update --project Gameya.Infrastructure --startup-project Gameya.API
dotnet run --project Gameya.API
```

Swagger is available at `/swagger` in Development and health status at `/health`.

For a local container stack:

```powershell
$env:GAMEYA_SA_PASSWORD = '<strong SQL password>'
$env:GAMEYA_JWT_KEY = '<at least 32 random bytes>'
docker compose up --build
```

## Required production configuration

Use environment variables or a secret store; never commit real values.

- `ConnectionStrings__DefaultConnection`
- `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey`
- `Email__Host`, `Email__Port`, `Email__UseSsl`, `Email__UserName`, `Email__Password`, `Email__FromAddress`
- `Email__PublicApiBaseUrl`
- `Cors__AllowedOrigins__0` (and subsequent origins)
- Optional one-time bootstrap: `BootstrapAdmin__Email` and `BootstrapAdmin__Password`

`Database__ApplyMigrationsOnStartup` should normally be `false` in a multi-instance production deployment; apply migrations as a release step. It is enabled by the included local Docker Compose configuration.

## Authentication and authorization

- Owners register with email/password and cannot log in until email confirmation.
- Payment users activate accounts through a seven-day owner invitation.
- Participants authenticate with phone number plus their immutable global subscriber code and receive a token scoped to their own person ID.
- Refresh tokens are random, stored only as SHA-256 hashes, rotated transactionally, revocable, and protected by optimistic concurrency.
- Association access is enforced in queries and commands. Unauthorized association IDs deliberately return `404` to avoid existence disclosure.
- Use `Accept-Language: ar-EG` for Egyptian Arabic error messages.

## Important financial behavior

- Payments are timestamped by the backend and processed in a serializable transaction.
- Due obligations are ordered by the oldest outstanding association, then oldest period.
- Excess requires an explicit association and becomes association-specific credit.
- Credit use is recorded as immutable ledger entries; it never becomes global credit.
- Corrections are signed compensating payments and allocations. Payments, allocations, credits, settlements, replacements, and audit history cannot be deleted through the application.
- Association, payment, payout, refresh-token, and background-job records use concurrency protection where relevant.
- Replaced memberships remain in history; completion creates an exit settlement for replaced paying memberships.

## Deliberately configurable or unresolved business policy

The source does not silently invent these rules:

- `AssociationCalendar` is the calendar boundary. Its current configured policy creates one period per month from `StartDate`, uses `AssociationCalendar:DueDay`, and defines `EndDate` as the final period date. Change this service when the owner approves a different boundary rule.
- Exact duplicate-name policy is isolated in `CreatePersonHandler`; the current explicit policy is exact normalized-name uniqueness.
- “سلف” is represented as a real person with `MembershipKind.Advance`, contributes zero to paying-member balance validation, and has no invented final-settlement behavior. That final workflow remains a documented product decision.
- “بدون اسم” is a reserve payout slot, never a fake person, and is exactly one full share. A later beneficiary is an association on the reserve payout, not a conversion into a person.
- The monthly PDF is behind `IReportGenerator`; its current neutral table is a version-zero format pending the approved reference report.
- Replacement payout reassignment remains an explicit owner schedule action because no automatic split rule was supplied.

## Verification

```powershell
dotnet build Gameya.sln --no-restore
dotnet test Gameya.sln --no-restore
```

The tests cover lifecycle rules, fractional shares, multiple payout recipients, reserve and advance exclusion, replacement splits, immutable payouts/payments, late days, settlements, email-verification registration, participant isolation, PaymentUser IDOR protection, oldest-first allocation, association credit, concurrency metadata, and layer dependencies.
