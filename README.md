# Gameya Backend

Gameya is a .NET 8 REST API for managing Egyptian rotating savings associations. It uses Clean Architecture, tactical DDD for financial workflows, CQRS with MediatR, SQL Server/EF Core, ASP.NET Core Identity, JWT access tokens with rotating refresh tokens, SMTP email, PDF reports, Serilog, FluentValidation, and Swagger.

The repository also includes `gameya-web`, a standalone Angular 22 application with an Arabic Egyptian RTL interface, role-specific lazy routes, reactive forms, Signals/RxJS state, typed API clients, responsive mobile layouts, and Vitest coverage.

## Solution layout

- `Gameya.Domain` — dependency-free aggregates, entities, value objects, rules, events, and exceptions.
- `Gameya.Application` — commands, queries, validators, DTOs, pipeline behavior, and ports.
- `Gameya.Infrastructure` — EF Core, SQL Server migrations, Identity, JWT, SMTP, PDF generation, and audit.
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

Run the Angular application in a second terminal:

```powershell
cd gameya-web
npm install
npm start
```

The development environment calls `https://localhost:7043/api`. Update `src/environments/environment.development.ts` if the API uses a different launch URL.

For a local container stack:

```powershell
$env:GAMEYA_SA_PASSWORD = '<strong SQL password>'
$env:GAMEYA_JWT_KEY = '<at least 32 random bytes>'
docker compose up --build
```

The compose stack exposes the Angular application at `http://localhost:4200` and proxies its same-origin `/api` requests to the API container.

## Required production configuration

Use environment variables or a secret store; never commit real values.

- `ConnectionStrings__DefaultConnection`
- `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey`
- `Email__Host`, `Email__Port`, `Email__UseSsl`, `Email__UserName`, `Email__Password`, `Email__FromAddress`
- `Email__PublicApiBaseUrl` (email-confirmation callback)
- `Email__PublicAppBaseUrl` (payment-user invitation callback)
- `Cors__AllowedOrigins__0` (and subsequent origins)
- `ScheduledReports__Secret` (at least 32 random characters; server-side only)
- Optional one-time bootstrap: `BootstrapAdmin__Email` and `BootstrapAdmin__Password`

`Database__ApplyMigrationsOnStartup` should normally be `false` in a multi-instance production deployment; apply migrations as a release step. It is enabled by the included local Docker Compose configuration.

### Automatic monthly collection email

MonsterASP Free cannot guarantee that an in-process timer stays alive, so Gameya exposes an external-scheduler endpoint instead of using Hangfire. Configure a scheduler such as cron-job.org to call `POST /api/internal/scheduled-reports/monthly-collection` on the first day of each month at the chosen Egypt (`Africa/Cairo`) time with header `X-Gameya-Scheduler-Key` equal to `ScheduledReports__Secret`.

The endpoint generates the current Egyptian business month's collection PDF for every active Owner. Executions use the durable key `automatic:{ownerId}:{yyyyMM}`: completed deliveries are never repeated, overlapping calls are ignored, and failed/stale executions can be retried. The secret is never sent to Angular. Manual PDF download and email delivery do not depend on the scheduler.

## Authentication and authorization

- Owners register with email/password and cannot log in until email confirmation.
- Payment users activate accounts through a single-use owner invitation that remains valid until accepted or explicitly revoked.
- Participants authenticate with phone number plus their immutable global subscriber code and receive a token scoped to their own person ID.
- Refresh tokens are random, stored only as SHA-256 hashes, rotated transactionally, revocable, and protected by optimistic concurrency.
- Association access is enforced in queries and commands. Unauthorized association IDs deliberately return `404` to avoid existence disclosure.
- Use `Accept-Language: ar-EG` for Egyptian Arabic error messages.

## Important financial behavior

- Payments are timestamped by the backend and processed in a serializable transaction.
- Due obligations are ordered by the oldest outstanding association, then oldest period.
- The backend allocates payments automatically across accessible associations by oldest outstanding obligation; excess becomes deterministic association-specific credit for a future obligation.
- Credit use is recorded as immutable ledger entries; it never becomes global credit.
- Corrections are signed compensating payments and allocations. Payments, allocations, credits, settlements, replacements, and audit history cannot be deleted through the application.
- Association, payment, payout, refresh-token, and background-job records use concurrency protection where relevant.
- Replaced memberships remain in history; completion creates an exit settlement for replaced paying memberships.

## Deliberately configurable or unresolved business policy

The source does not silently invent these rules:

- `AssociationCalendar` is the calendar boundary. Its current configured policy creates one period per month from `StartDate`, uses `AssociationCalendar:DueDay`, and defines `EndDate` as the final period date. Change this service when the owner approves a different boundary rule.
- Exact duplicate-name policy is isolated in `CreatePersonHandler`; the current explicit policy is exact normalized-name uniqueness.
- “سلف” is a virtual payout slot (`AdvanceSlot`), never a person and never a paying membership.
- “بدون اسم” is a reserve payout slot, never a fake person, and is exactly one full share. A later beneficiary is an association on the reserve payout, not a conversion into a person.
- Monthly and association paper-style Arabic PDFs share `IReportGenerator`; association reports group multiple recipients per period and render normal, “بدون اسم”, and “سلف” recipients.
- Replacement payout reassignment remains an explicit owner schedule action because no automatic split rule was supplied.

## Verification

```powershell
dotnet build Gameya.sln --no-restore
dotnet test Gameya.sln --no-restore
cd gameya-web
npm run build
npm test
```

The tests cover lifecycle rules, fractional shares, multiple payout recipients, reserve and advance exclusion, replacement splits, immutable payouts/payments, late days, settlements, email-verification registration, participant isolation, PaymentUser IDOR protection, oldest-first allocation, association credit, concurrency metadata, and layer dependencies.
