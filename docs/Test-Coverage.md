# Test Execution and Coverage

## Verified run

- Verified: 2026-11-27
- Target framework: .NET 10
- Test command: `dotnet test AuditLog.Tests\AuditLog.Tests\AuditLog.Tests.csproj`
- Result: 132 passed, 0 failed, 0 skipped
- API build: succeeded with 0 warnings and 0 errors
- Coverage command: `dotnet test AuditLog.Tests\AuditLog.Tests\AuditLog.Tests.csproj --collect:"XPlat Code Coverage"`

The generated Cobertura report is written under
`AuditLog.Tests\AuditLog.Tests\TestResults\<run-id>\coverage.cobertura.xml`.

## Coverage results

Coverage across all instrumented project assemblies:

| Assembly | Line coverage | Branch coverage |
|---|---:|---:|
| All instrumented assemblies | 56.20% (1,101 / 1,959) | 32.36% (156 / 482) |
| AuditLog.API | 51.25% | 12.91% |
| AuditLog.BLL | 99.69% | 96.87% |
| AuditLog.DAL | 3.23% | 0.87% |

The API assembly total includes source-generated OpenAPI code and application
startup/registration code. Domain controller actions, API-key authentication,
and validation paths have focused tests; the database health endpoint's
unavailable-database response is tested.

The BLL services have near-complete line and branch coverage. The DAL contains
SQL Server repository implementations that are not exercised by these
in-memory unit tests. SQL-backed repository and healthy-database integration
tests remain necessary before considering DAL coverage representative.

## Covered behaviors

- Scenario A: event creation/query action results, query validation, hashing,
  integrity success and tamper/error paths.
- Scenario B: retention configuration, redaction validation and failures,
  export formats/filter validation, and bundle construction.
- Scenario C: compliance report mapping, validation, and API-key enforcement.
- Domain API controllers: success and mapped error results; required-field and
  pagination validation; unauthorized requests on protected routes.

