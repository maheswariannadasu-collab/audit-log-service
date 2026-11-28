# AI Assistance and Project Attestation

**Revision updated:** 2026-11-27

## AI assistance disclosure

AI assistance was used to analyze the assessment requirements, support
implementation across Scenarios A, B, and C, develop focused tests, and update
project documentation. The implementation and validation evidence below is
limited to work documented in this repository. The project owner remains
responsible for reviewing the code, understanding the submitted implementation,
and deciding whether it is ready for submission or deployment.

## Scenario implementation and test evidence

| Scenario | Implemented scope | Test evidence | Current validation status |
|---|---|---|---|
| A – Core audit logging | Event creation and retrieval/query; hash-chain creation and verification; filtering and pagination. | [AuditEventWriterServiceTests.cs](AuditLog.Tests/AuditLog.Tests/AuditEventWriterServiceTests.cs), [AuditEventQueryServiceTests.cs](AuditLog.Tests/AuditLog.Tests/AuditEventQueryServiceTests.cs), [AuditHashingServiceTests.cs](AuditLog.Tests/AuditLog.Tests/AuditHashingServiceTests.cs), [AuditIntegrityServiceTests.cs](AuditLog.Tests/AuditLog.Tests/AuditIntegrityServiceTests.cs), controller/authorization tests, and [SQL Server integration tests](AuditLog.Tests/AuditLog.Tests/SqlServerApiIntegrationTests.cs). See [Scenario A](docs/Scenario-A.md). | SQL Server integration tests and engineer-reported target-database checks cover event persistence, pagination, integrity, and tampering detection. Concurrent-writer stress testing remains pending. |
| B – Retention, redaction, and export | Configurable archival, payload redaction with a redacted-content hash, JSON/CSV export, and filtered export bundles with chain proof. | [AuditScenarioBServiceTests.cs](AuditLog.Tests/AuditLog.Tests/AuditScenarioBServiceTests.cs), controller/authorization tests, and [SQL Server integration tests](AuditLog.Tests/AuditLog.Tests/SqlServerApiIntegrationTests.cs). See [Scenario B](docs/Scenario-B.md). | SQL Server integration tests and engineer-reported target-database checks cover retention, redaction, exports, and bundle-proof recomputation. Redaction does not erase historical backup/log copies; bundles are not signed or externally anchored. |
| C – Compliance reporting | Account-scoped, date-filterable, paginated access reports using the redaction-aware query path. | [ComplianceReportServiceTests.cs](AuditLog.Tests/AuditLog.Tests/ComplianceReportServiceTests.cs), JWT/controller tests, and [SQL Server integration tests](AuditLog.Tests/AuditLog.Tests/SqlServerApiIntegrationTests.cs). See [Scenario C](docs/Scenario-C.md). | SQL Server integration tests and engineer-reported target-database checks cover account/date filtering, pagination, and JWT account assignment. Production identity-provider and database-backed account assignment remain pending. |

The scenarios use local JWT Bearer authentication and permission claims. Account
assignment comes from the JWT `accountId` claim for this assignment. A production
identity provider and database-backed account mapping remain pending.

## Verified build, test, and coverage results

Verified on 2026-11-27 for the .NET 10 solution:

- With SQL Server integration enabled: **140 passed, 0 failed, 0 skipped**,
  including 11 SQL Server integration test methods.
- Without integration opt-in: **129 passed, 0 failed, 11 skipped**.
- The full test run built the API, BLL, DAL, and test projects successfully.

The integration fixture used a real LocalDB instance, created a uniquely named
disposable database, applied scripts `001`–`006`, and dropped the database
afterward. The engineer also confirmed that scripts `001`–`004` are applied to
the target database and that Scenarios A, B, and C, JWT access, retention,
redaction, export, database tampering detection, and the health API have been
validated. See [Test-Coverage.md](docs/Test-Coverage.md) for the opt-in command
and test details.

The following coverage figures are from the earlier 132-test coverage run,
before the SQL Server integration tests were added; coverage has not been
re-collected for the latest 140-test run:

| Assembly | Line coverage | Branch coverage |
|---|---:|---:|
| All instrumented assemblies | 56.20% (1,101 / 1,959) | 32.36% (156 / 482) |
| AuditLog.API | 51.25% | 12.91% |
| AuditLog.BLL | 99.69% | 96.87% |
| AuditLog.DAL | 3.23% | 0.87% |

The API total includes source-generated OpenAPI and application startup code.
The existing Cobertura figures do not reflect the new SQL Server integration
execution. The integration suite covers the healthy health endpoint with a
valid JWT; the unavailable-database response is also covered by a
request-level test.

The generated [Cobertura report](AuditLog.Tests/AuditLog.Tests/TestResults/151824fc-5b67-43fa-9032-8b85c08ec791/coverage.cobertura.xml)
contains the detailed coverage data. The test and coverage summary is also in
[Test-Coverage.md](docs/Test-Coverage.md).

## Remaining validation

- Concurrent SQL Server writer stress testing.
- Production identity-provider integration and database-backed account
  authorization; the local implementation uses claim-based account assignment.
- Production deployment and operational validation.

## Project-owner confirmation

- **Project owner:** Annadasu Umamaheswari
- **Date:** 25-11-2026
- **Signature:** A.Umamaheswari
