# Audit Log – Architecture

## Architecture Overview

The application follows a layered architecture.

- **API Layer:** Handles HTTP requests through controllers.
- **BLL Layer:** Contains business logic, interfaces, models and services.
- **DAL Layer:** Handles database operations using repositories.
- **Database:** SQL Server stores audit events and related information.

## Architecture Flow

Client → API Controller → BLL Service → DAL Repository → SQL Server

Scenario A, B, and C controller endpoints follow the same flow and are
protected by local JWT Bearer authentication and named permission policies.

## Key Design Decisions

- Dependency Injection for loose coupling.
- Repository Pattern for database access.
- Async/await for database operations.
- CancellationToken for request cancellation.
- SHA-256 hashing for audit integrity.
- SQL transactions and application locks for hash-chain consistency.
- Compliance reports reuse the existing paginated audit-event query; no new
  database table is introduced.

## Database Tables

- AuditEvents
- AuditEventRedactions

## Security and Reliability

- Parameterized SQL queries.
- Hash chaining for tamper detection.
- Separate storage for redacted data.
- Account and date scoping for compliance reports.
- JWT issuer, audience, signature, and lifetime validation is required.
- Permission claims control endpoint access independently of role-only checks.
- The compliance report route checks JWT `accountId` claim assignment.
- Secrets are supplied through configuration and are not committed.
- Cancellation support for asynchronous operations.
- Fixed-window ASP.NET Core rate limiting protects token issuance and `/api/*`.
- `/health` is public and excluded from rate limiting.
- DataAnnotations and a global exception handler provide consistent ProblemDetails.

The JWT implementation is a local OIDC-compatible demonstration. Production
should replace claim-based account mapping and local signing-key configuration
with an external identity provider and a database-backed account assignment.