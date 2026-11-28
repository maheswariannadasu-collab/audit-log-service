# Test Execution and Coverage

## Latest verified test run

- Verified: 2026-11-27
- Target framework: .NET 10
- SQL Server: LocalDB `(localdb)\MSSQLLocalDB`
- SQL integration methods: 10 passed, 0 failed, 0 skipped
- Full suite with integration enabled: **143 passed, 0 failed, 0 skipped**
- Full suite without integration opt-in: **133 passed, 0 failed, 10 skipped**

The integration tests use a real SQL Server database. The fixture creates a
uniquely named disposable database (`AuditLogIntegration_<id>`), applies
database scripts `001`–`006`, runs the tests, and drops the database afterward.
The admin connection's initial catalog is `master` only so the fixture can
create and drop databases. Tests do not run against `master` or the
application's `AuditLogDB` database. The disposable test database was verified
removed after the run.

The admin connection string must connect to `master` on a dedicated test SQL
Server instance and have permission to create and drop databases. For Windows
LocalDB:

```powershell
$env:AUDITLOG_RUN_SQLSERVER_INTEGRATION_TESTS = "true"
$env:AUDITLOG_SQLSERVER_ADMIN_CONNECTION_STRING = "Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False"
dotnet test AuditLog.Tests\AuditLog.Tests\AuditLog.Tests.csproj
```

Do not point the admin connection at a production or shared development
instance. Without both environment variables, the SQL Server integration tests
are skipped.

To run only the SQL Server integration tests, use the same environment
variables and add:

```powershell
dotnet test AuditLog.Tests\AuditLog.Tests\AuditLog.Tests.csproj --filter Category=SqlServerIntegration
```

## SQL Server integration coverage

The [integration test suite](../AuditLog.Tests/AuditLog.Tests/SqlServerApiIntegrationTests.cs)
uses the real API and SQL repositories. It covers:

- Missing and invalid JWTs across the protected controller route/method
  combinations, including the database health endpoint.
- Database health success without authentication.
- Event insertion, retrieval, filtered querying, and SQL-backed pagination.
- Both integrity verification routes and direct database payload tampering.
- Retention updates, preservation of current records, and repeat-run
  idempotency.
- Redaction preview/application, persisted payload replacement and hash
  metadata, duplicate rejection, and integrity verification of redacted data.
- Filtered JSON/CSV/bundle exports and independent recomputation of the bundle
  hash-only chain proof.
- Account-scoped compliance reporting with SQL-backed date filtering and
  pagination.

The engineer also confirmed that migrations `001`–`004` are applied to the
target database and that Scenarios A, B, and C, JWT access, retention,
redaction, export, and database tampering detection have been validated
against SQL Server. This is recorded as engineer-confirmed validation; no
separate target-database run log was supplied.

## Recorded coverage collection

The percentages below are from the earlier **132-test** coverage run, before
the SQL Server integration tests were added. Coverage has not yet been
re-collected for the latest 143-test run; these figures therefore do not
include the new SQL integration test execution.

| Assembly | Line coverage | Branch coverage |
|---|---:|---:|
| All instrumented assemblies | 56.20% (1,101 / 1,959) | 32.36% (156 / 482) |
| AuditLog.API | 51.25% | 12.91% |
| AuditLog.BLL | 99.69% | 96.87% |
| AuditLog.DAL | 3.23% | 0.87% |

Coverage command:

```powershell
dotnet test AuditLog.Tests\AuditLog.Tests\AuditLog.Tests.csproj --collect:"XPlat Code Coverage"
```

The API assembly total includes source-generated OpenAPI and application
startup/registration code. SQL-backed repository behavior is now exercised by
the integration suite, but a new coverage collection is needed to measure its
effect on the DAL percentages.

## Remaining limitations

- The integration suite does not stress concurrent database writers.
- The database health endpoint's unavailable-database response is covered by
  the existing request tests; the healthy SQL Server response is covered by
  the integration suite.
- The export proof is recomputed in tests, but bundles are not digitally
  signed or externally anchored.
- Shared claim-based account assignment does not provide a production
  database-backed account lookup or external identity-provider validation.
