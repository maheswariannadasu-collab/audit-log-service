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
- Handle compliance requirements through documented assumptions and engineering decisions.

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
       +--------------+--------------+
       |              |              |
       v              v              v
 Audit Events     Retention      Redaction
 Controller      Controller      Controller
       |              |              |
       +--------------+--------------+
                      |
             +--------+--------+
             |                 |
             v                 v
        Export Controller   Integrity Controller
             |                 |
             +--------+--------+
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
- Handles audit processing, hashing, retention, redaction and export operations.

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
│   │   └── AuditExportController.cs
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

**Status:** Core implementation completed; final validation pending.

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

**Status:** Retention, redaction, and export APIs are implemented; SQL-backed
end-to-end validation and independent bundle verification remain pending.

### Scenario C – Compliance Reporting

**Requirement:**

"Regulators need to be able to audit access to client account data."

Proposed approach:

- Identify client account access events.
- Document requirement assumptions.
- Design compliance reporting API.
- Support account and date-based filtering.
- Restrict access to authorized users.

**Status:** Not implemented. Pending requirement clarification and design.

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

Apply scripts `001` through `004` in order. Script `004` adds the redacted
content hash required by redaction-aware verification and export.

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

### Step 5 – Build Solution

```bash
dotnet build
```

### Step 6 – Run Application

```bash
dotnet run --project AuditLog.API
```

Open Swagger to explore the available APIs.

## 7. API Controllers

The application uses separate controllers to keep responsibilities independent.

| Controller | Responsibility |
|---|---|
| AuditEventsController | Create and retrieve audit events |
| AuditIntegrityController | Verify audit record integrity |
| AuditRetentionController | Manage audit retention |
| AuditRedactionController | Manage payload redaction |
| AuditExportController | Retrieve audit data for export |

Example working endpoint:

```http
GET /api/audit-events/1?id=1
```

The remaining endpoint routes should be verified against the actual controller attributes.

## 8. Testing Strategy

### Unit Testing
- Hash generation.
- Record hash generation.
- Genesis hash validation.
- Input validation.
- Integrity verification.

### Integration Testing
- Database connectivity.
- Audit event insertion and retrieval.
- Filtering and pagination.
- Concurrent event insertion.
- Retention processing and archived-record verification.
- Redaction, duplicate prevention, and live-payload replacement.
- Targeted export and independent chain-proof verification.

### Security Testing
- Verify audit records cannot be modified through the API.
- Verify audit records cannot be deleted through the API.
- Validate hash-chain tampering detection.
- Verify unauthorized access handling.

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

The following areas require further implementation or validation:

- SQL-backed end-to-end validation for retention, redaction, and export.
- Independent verification of exported bundles; the prototype does not sign or
  externally anchor bundle chain tips.
- Redaction does not erase values from historical SQL backups or transaction
  logs.
- Automated integration tests.
- Compliance reporting.
- Production Azure deployment.

## 13. Project Status

| Scenario | Status |
|---|---|
| Scenario A – Core Audit Logging | Implemented; SQL end-to-end validation pending |
| Scenario B – Retention, Redaction and Export | Implemented; SQL end-to-end validation pending |
| Scenario C – Compliance Reporting | Not implemented |
| Architecture Documentation | Prepared |
| Automated Testing | Pending final validation |
| Azure Deployment | Planned |

## 14. Author and Ownership

This project demonstrates practical backend development, SQL Server database design, cryptographic integrity, API development, and responsible AI-assisted software engineering.

All architectural decisions, implementation changes, testing and final deliverables remain under engineer review and ownership.