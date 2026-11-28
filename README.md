# AI-Assisted Audit Log Service

A secure, append-only Audit Log Service built using **C#, ASP.NET Core Web API, SQL Server, and ADO.NET**, following an AI-assisted software engineering approach.

The project demonstrates audit logging, data integrity, retention, redaction, export, and compliance-related design while maintaining engineering ownership and validation.

---

## 1. Project Overview

The Audit Log Service provides a centralized mechanism to record, retrieve, and verify application audit events.

The system is designed to:

- Capture user and system activities.
- Store structured audit events in SQL Server.
- Maintain an append-only audit history.
- Protect audit records using SHA-256 hash chaining.
- Detect unauthorized database modifications.
- Support retention, redaction, and bulk export.
- Provide account-scoped compliance reports to authorized readers.
- Support SQL-backed filtering, sorting, and pagination for audit events.
- Provide consistent ProblemDetails responses and fixed-window rate limiting.
- Document assumptions and engineering decisions for compliance requirements.

## 2. Technology Stack

| Technology | Purpose |
|---|---|
| C# | Backend programming language |
| .NET 10 | Application framework |
| ASP.NET Core Web API | REST API development |
| SQL Server Express | Local database |
| Azure SQL Database | Planned cloud database |
| ADO.NET | Database connectivity |
| Microsoft.Data.SqlClient | SQL Server data access |
| SHA-256 | Cryptographic hashing |
| System.Text.Json | JSON serialization |
| xUnit | Automated testing |
| Visual Studio 2022 | Development environment |
| Azure App Service | Planned application hosting |
| Azure Key Vault | Planned secrets management |
| Application Insights | Planned monitoring |

## 3. Architecture

The application follows a layered architecture with separate controllers for different audit functionalities.

### Architecture Diagram

```text
                    Client
                      |
                      v
                AuditLog.API
                      |
                      v
       Scenario A, B, and C API Controllers
              [JWT permission policies]
                      |
                      v
                 AuditLog.BLL
              Business Services
                      |
                      v
                 AuditLog.DAL
                  Repositories
                      |
                      v
                  SQL Server
```

### Layer Responsibilities

**API Layer**
- Handles HTTP requests and responses.
- Uses separate controllers for different functionalities.
- Handles request validation and API responses.

**BLL Layer**
- Contains business logic.
- Defines service interfaces.
- Handles audit processing, hashing, retention, redaction, export, and
  compliance-report operations.

**DAL Layer**
- Contains repository implementations.
- Uses ADO.NET and parameterized SQL queries.
- Handles SQL Server operations.

**Database**
- Stores audit events and redaction records.
- Maintains audit hashes and archival information.

### Project Structure

```text
AuditLogService/
│
├── AuditLog.API/
│   ├── Controllers/
│   │   ├── AuditEventsController.cs
│   │   ├── AuditIntegrityController.cs
│   │   ├── AuditRetentionController.cs
│   │   ├── AuditRedactionController.cs
│   │   ├── AuditExportController.cs
│   │   └── ComplianceReportsController.cs
│   ├── Auth/
│   │
│   ├── Program.cs
│   ├── appsettings.json
│   └── AuditLog.API.csproj
│
├── AuditLog.BLL/
│   ├── Interfaces/
│   ├── Models/
│   └── Services/
│
├── AuditLog.DAL/
│   └── Repositories/
│
├── AuditLog.Tests/
│
├── database/
│   └── Scripts/
│
├── docs/
│
├── ATTESTATION.md
├── README.md
└── AuditLogService.sln
```

*The structure above shows the intended separation of responsibilities. Keep the controller filenames aligned with your actual source files.*

## 4. Implementation Scenarios

The assignment is divided into three scenarios.

### Scenario A – Core Audit Logging

**Objective:** Implement core audit logging and integrity verification.

Implemented functionality:

- Create and retrieve audit events.
- Capture event type, actor, resource, payload and timestamp.
- Generate SHA-256 content hashes.
- Maintain hash chaining using previous record hashes.
- Use a genesis hash for the first event.
- Use SQL transactions and `sp_getapplock` for concurrent writes.
- Retrieve audit records for integrity verification.
- Support audit event querying.

**Main Components:**
- AuditEventsController
- AuditIntegrityController
- AuditEventRepository
- AuditHashingService
- AuditIntegrityRepository
- AuditEventQueryService

**Status:** Core implementation and SQL Server validation are complete,
including integration tests and engineer-reported target-database checks.
Concurrent-writer stress testing remains pending.

### Scenario B – Retention, Redaction and Export

**Objective:** Extend the audit service with audit data management functionality.

**Retention**
- `POST /api/audit-retention/archive` marks records older than the configured
  `AuditRetention:RetentionDays` as archived.
- Archived rows remain in the chain and are included in verification.

**Redaction**
- `POST /api/audit-redaction/apply` replaces configured payload values in the
  live event row and records redaction metadata.
- The original content hash remains the chain anchor; a separate hash verifies
  the current redacted payload.
- Duplicate redaction is rejected, except legacy redaction rows without the new
  redacted-content hash can be upgraded.
- SQL backups and transaction logs may retain historical values; this is not
  secure erasure from backups.

**Export**
- JSON and CSV exports accept optional actor/resource and UTC date filters.
- `GET /api/audit-export/bundle` requires `actorId` or `resourceId` and includes
  selected events, their hashes, genesis/tip values, and a hash-only chain proof.

**Main Components:**
- AuditRetentionController
- AuditRedactionController
- AuditExportController
- AuditRetentionRepository
- AuditRedactionRepository
- AuditExportRepository

**Status:** Retention, redaction, and export APIs are implemented and covered
by SQL Server integration tests, including bundle-proof recomputation.
External signing or anchoring of bundles and production deployment validation
remain pending.

### Scenario C – Compliance Reporting

**Objective:** Allow authorized compliance readers to review client account
activity by account and date range.

Implemented functionality:

- `GET /api/compliance-reports/accounts/{accountId}/access` filters to
  `ResourceType = ACCOUNT` and the requested account ID.
- Supports optional inclusive `fromUtc`, exclusive `toUtc`, and bounded
  pagination.
- Returns the actor, event type, account, timestamp, and stored payload, using
  the existing redaction-aware query path.
- Protects Scenario A, B, and C API endpoints with local JWT Bearer
  authentication and permission claims.
- Reuses the existing audit-event table and query repository; no new DB
  migration is required.

**Status:** Implemented for the local assessment and exercised by SQL Server
integration tests; the engineer also confirmed validation against the target
SQL Server database. Account assignments come from the JWT `accountId` claim
and will require a database lookup in production.

## 5. Database Design

Database: `AuditLogDb`

### Main Tables

**dbo.AuditEvents**

| Column | Description |
|---|---|
| Id | Unique audit record identifier |
| EventType | Type of activity |
| ActorId | User or system performing the action |
| ResourceType | Type of affected resource |
| ResourceId | Identifier of affected resource |
| PayloadJson | Structured event information |
| EventTimestampUtc | Event occurrence timestamp |
| ContentHash | SHA-256 content hash |
| PreviousHash | Previous record hash |
| RecordHash | Current record hash |
| CreatedAtUtc | Database insertion timestamp |
| IsArchived | Archive status |
| ArchivedAtUtc | Archive timestamp |

**dbo.AuditEventRedactions**

Stores redacted payload information, request metadata, and the hash of the
current redacted payload. `ContentHash` on the event remains the original chain
anchor.

### Hash Chain Design

Each audit record contains:

- Content hash.
- Previous record hash.
- Current record hash.

```text
ContentHash = SHA256(CanonicalEventJson)

RecordHash = SHA256(PreviousHash + ContentHash)
```

The first record uses a predefined genesis hash.

Hash chaining helps detect modifications to stored audit records. External integrity checkpoints or protected signing keys can further strengthen tamper resistance.

## 6. Local Development Setup

### Prerequisites

- Visual Studio 2022
- .NET 10 SDK
- SQL Server Express
- SQL Server Management Studio
- Git

### Step 1 – Clone Repository

```bash
git clone YOUR_PRIVATE_REPOSITORY_URL
cd AuditLogService
```

### Step 2 – Restore Dependencies

```bash
dotnet restore
```

### Step 3 – Create Database

```sql
CREATE DATABASE AuditLogDb;
```

Execute the SQL scripts available in:

```text
database/Scripts/
```

Apply scripts `001` through `007` in order. Script `004` adds the redacted
content hash; script `006` adds server-verified JWT actor attribution; script
`007` adds POST audit-event idempotency storage.

### Step 4 – Configure Connection String

Update the connection string in `AuditLog.API/appsettings.json`.

```json
{
  "ConnectionStrings": {
    "AuditLogDb": "Server=.\\SQLEXPRESS;Database=AuditLogDb;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=True;"
  }
}
```

Replace the server name with your SQL Server instance.

Configure a local JWT signing key using user secrets. The signing key is not
stored in source control:

```powershell
dotnet user-secrets set "Jwt:SigningKey" "<random-key-of-at-least-32-characters>" --project AuditLog.API\AuditLog.API\AuditLog.API.csproj
```

The local `Jwt:Mode` setting is `Local`; issuer, audience, and authority are
configurable in `appsettings.json`. Get a demo token:

```http
POST /auth/token
Content-Type: application/json

{
  "username": "AuditWriter",
  "accountId": "ACCOUNT100"
}
```

Send the returned token as `Authorization: Bearer <token>`. Missing or invalid
tokens receive HTTP 401; valid tokens without a required permission receive
HTTP 403.

### Step 5 – Build Solution

```bash
dotnet build
```

### Step 6 – Run Application

```bash
dotnet run --project AuditLog.API
```

Open Scalar at the development route `/scalar` or retrieve `/openapi/v1.json`
to explore the available APIs.

## 7. API Controllers

The application uses separate controllers to keep responsibilities independent.

| Controller | Responsibility |
|---|---|
| AuditEventsController | Create and retrieve audit events |
| AuditIntegrityController | Verify audit record integrity |
| AuditRetentionController | Manage audit retention |
| AuditRedactionController | Manage payload redaction |
| AuditExportController | Retrieve audit data for export |
| ComplianceReportsController | Retrieve protected account-access reports |

Example working endpoint:

```http
GET /api/audit-events/1?id=1
```

Audit-event list query examples:

```http
GET /api/audit-events?accountId=ACC123&page=1&pageSize=50&sortBy=createdAt&sortDir=desc
GET /api/audit-events?action=DEPOSIT&sortDir=asc
GET /api/audit-events?from=2026-01-01&to=2026-01-31&search=Ravi
```

Supported list parameters are `accountId`, `action`, `search`, `from`, `to`,
`actorId`, `resourceType`, `resourceId`, `eventType`, `fromUtc`, `toUtc`,
`sortBy`, `sortDir`, `page`, and `pageSize`. `sortBy` accepts `createdAt`,
`action`, or `accountId`; invalid values default to `createdAt desc`.
`page` defaults to 1, `pageSize` defaults to 50 and is capped at 100.
The response includes `items`, `totalCount`, `pageNumber`, `pageSize`, and
`totalPages`.

The current controller routes are covered by the authorization tests; see
[Test-Coverage.md](docs/Test-Coverage.md) for the verified test results.

### Rate Limiting

The API uses the built-in ASP.NET Core fixed-window rate limiter:

- `POST /auth/token`: 5 requests per minute per IP.
- `/api/*` with a valid JWT: 100 requests per minute per `sub` claim.
- `/api/*` without a valid JWT: 20 requests per minute per IP.
- `/health` and `/api/health/database` are not rate limited.
- `/openapi` and Scalar documentation endpoints are not rate limited.

Exceeding a limit returns HTTP 429. Token limiting includes `Retry-After: 60`;
API limiting uses a ProblemDetails JSON response.

### Validation and Error Responses

The API uses ASP.NET Core `[ApiController]` DataAnnotations validation and a
consistent ProblemDetails-style response for validation, authentication,
authorization, not-found, conflict, rate-limit, service-unavailable, and
unhandled server errors.

Unhandled exceptions are converted to generic HTTP 500 responses. Stack traces,
SQL details, and internal exception messages are not returned to clients.

## 8. Testing Strategy

### Unit Testing
- Hash generation.
- Record hash generation.
- Genesis hash validation.
- Input validation.
- Integrity verification.
- Compliance account/date filtering, report projection, and JWT validation.
- Domain-controller success, validation, and error responses; database-health
  failure handling.

### API Request Testing
- HTTP-level authorization and request-model validation using the test server.
- Missing and invalid JWTs across protected Scenario A, B, and C endpoints.
- `/health` is public and does not perform a database check.
- `/health/database` performs the SQL `SELECT 1` health check and is not
  protected or rate limited.
- Database-health failure response when SQL Server is unavailable.

### SQL Server Integration Testing

The opt-in integration suite exercises the API against SQL Server, including
JWT authorization, database queries, retention, redaction, exports,
compliance reports, health checks, and tampering detection. The admin
connection uses `master` only for database setup. The suite creates a temporary
database named `AuditLogIntegration_<id>`, applies scripts `001`–`007`, runs the
tests against it, then drops it. The application's `AuditLogDB` is not used or
modified. Use a dedicated test SQL Server instance, not a production or shared
development instance.

For LocalDB on Windows:

```powershell
$env:AUDITLOG_RUN_SQLSERVER_INTEGRATION_TESTS = "true"
$env:AUDITLOG_SQLSERVER_ADMIN_CONNECTION_STRING = "Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False"
dotnet test AuditLog.Tests\AuditLog.Tests\AuditLog.Tests.csproj
```

The admin connection must connect to `master` and have permission to create
and drop databases. Without both environment variables, the 11 SQL Server
integration tests are skipped.

### Security Testing
- SQL database tampering detection is covered by hash-chain integration tests.
- Missing and invalid JWTs are rejected across protected route/method
  combinations.
- The database health endpoint remains accessible without authentication.

The latest verified test and coverage results are documented in
[Test-Coverage.md](docs/Test-Coverage.md).

## 9. AI-Assisted Engineering Approach

AI tools are used as engineering assistants throughout development.

Usage includes:

- Requirement analysis.
- Architecture suggestions.
- Code generation assistance.
- Code review.
- Test case suggestions.
- Documentation preparation.
- Security and edge-case analysis.

All AI-generated suggestions are reviewed and validated by the engineer before acceptance.

AI usage details are maintained in the documentation folder.

## 10. Azure Deployment Plan

The planned cloud architecture uses:

| Azure Service | Purpose |
|---|---|
| Azure App Service | API hosting |
| Azure SQL Database | Managed database |
| Azure Key Vault | Secure secrets management |
| Application Insights | Monitoring |

Azure deployment is planned and has not yet been completed.

## 11. Engineering Principles

- Separation of concerns.
- Single Responsibility Principle.
- Repository Pattern.
- Dependency Injection.
- Asynchronous database operations.
- Parameterized SQL queries.
- Append-only audit records.
- Hash-chain integrity.
- Transaction-safe database operations.
- Input validation.
- Structured error handling.
- Human review of AI-generated code.

## 12. Known Limitations

The following areas remain limitations or require further validation:

- The account assignment is currently claim-based through JWT `accountId`.
  Production should replace this mapping with an identity-provider-backed
  database lookup and least-privilege policies.
- The prototype does not digitally sign or externally anchor export bundle
  chain tips.
- Redaction does not erase values from historical SQL backups or transaction
  logs.
- Concurrent database-writer stress testing.
- Production Azure deployment.

## 13. Project Status

| Scenario | Status |
|---|---|
| Scenario A – Core Audit Logging | Implemented; SQL Server integration tests passed |
| Scenario B – Retention, Redaction and Export | Implemented; SQL Server integration tests passed |
| Scenario C – Compliance Reporting | Implemented; SQL Server integration tests passed |
| Architecture Documentation | Prepared |
| Automated Testing | Unit tests and opt-in SQL Server integration tests; latest default run: 132 passed |
| Azure Deployment | Planned |

## 14. Author and Ownership

This project demonstrates practical backend development, SQL Server database design, cryptographic integrity, API development, and responsible AI-assisted software engineering.

All architectural decisions, implementation changes, testing and final deliverables remain under engineer review and ownership.