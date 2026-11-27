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
protected by the configuration-backed API-key authentication scheme before
their controllers are invoked.

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
- Configuration-supplied API key required for Scenario A, B, and C API routes.
- Secrets are supplied through configuration and are not committed.
- Cancellation support for asynchronous operations.

The shared API key is a local assessment mechanism. Production should use an
identity provider and regulator-specific authorization. The current key does
not identify individual regulators or enforce account-level entitlements.