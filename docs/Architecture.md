# Audit Log – Architecture

## Architecture Overview

The application follows a layered architecture.

- **API Layer:** Handles HTTP requests through controllers.
- **BLL Layer:** Contains business logic, interfaces, models and services.
- **DAL Layer:** Handles database operations using repositories.
- **Database:** SQL Server stores audit events and related information.

## Architecture Flow

Client → API Controller → BLL Service → DAL Repository → SQL Server

## Key Design Decisions

- Dependency Injection for loose coupling.
- Repository Pattern for database access.
- Async/await for database operations.
- CancellationToken for request cancellation.
- SHA-256 hashing for audit integrity.
- SQL transactions and application locks for hash-chain consistency.

## Database Tables

- AuditEvents
- AuditEventRedactions

## Security and Reliability

- Parameterized SQL queries.
- Hash chaining for tamper detection.
- Separate storage for redacted data.
- Cancellation support for asynchronous operations.