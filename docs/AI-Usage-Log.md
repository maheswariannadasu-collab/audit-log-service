# AI Usage Log

This log records AI-assisted tasks, engineer decisions, repository changes, and
available validation evidence in chronological order. Prompts are summarized
unless identified as verbatim. Git commit file lists identify the scope of each
change; they do not prove that AI authored every line in those files.

## Evidence and attribution

- The initial user statement was that the project, APIs, and database schema
  already existed. Earlier setup commits are therefore treated as project
  baseline unless an AI contribution is specifically recorded below.
- Git commits are authored by `Uma`; commit authorship alone does not establish
  whether code was AI-generated or developer-written.
- Test/build outcomes are labeled as assistant-run, repository-recorded, or
  developer-reported. Missing task-specific evidence is stated rather than
  inferred.
- Commit references below were reviewed through `cbbb4f2` (`HEAD` at the time
  of this update).

## Chronological task record

### 1. Project architecture advice

**Date / commit:** Initial project history; recorded in `d1f18a1` (2026-11-25).

**AI tool:** ChatGPT, as recorded in the original log.

**Prompt (verbatim from the original log):** “Suggest a maintainable
architecture for an audit log service.”

**AI contribution:** Suggested API, BLL, DAL, and database layers.

**Engineer decision:** Accepted the layered approach with modifications to
keep the assignment implementation simple.

**Files / commit evidence:** `docs/AI-Usage-Log.md` and `docs/Architecture.md`
were added in the initial project commit. The commit also created the initial
repository scaffold, but the available record does not attribute those source
files individually to AI.

**Validation:** No build or test result is recorded for this architecture
advice.

### 2. Assessment coverage review and completion planning

**Date / commit:** Before the Scenario A follow-up; no separate commit.

**Task request summary:** Compare the existing project with the assessment,
identify completed and pending areas, and propose a completion plan.

**AI contribution:** Reviewed the existing implementation and scenario
documentation, summarized gaps across Scenarios A, B, and C, and recommended
priorities for completing them.

**Engineer decision:** Proceeded with high-level Scenario A completion, then
Scenario B and Scenario C work. Database-script and local API validation
questions were handled separately as they arose.

**Files changed:** None for the assessment review itself.

**Validation:** No build or tests were requested or run for this review.

### 3. Scenario A completion and optimization

**Date / commit:** 2026-11-26; `5fbd1b2`.

**Task request summary:** Complete Scenario A at a high level without breaking
existing behavior, update its scenario document, and optimize the integrity
verification path.

**AI contribution:** Added filtered and paginated event queries, retained the
existing verification route while adding the required route, implemented
streaming chain verification, and updated the Scenario A documentation and
focused tests.

**Engineer decisions:** Preserved the existing verification route for
compatibility. The work did not require a new database schema script.

**Files changed in `5fbd1b2`:**

- `AuditLog.API/AuditLog.API/Controllers/AuditEventsController.cs`
- `AuditLog.API/AuditLog.API/Controllers/AuditIntegrityController.cs`
- `AuditLog.API/AuditLog.API/DTOs/AuditEventPageResponse.cs`
- `AuditLog.API/AuditLog.API/DTOs/AuditEventQueryRequest.cs`
- `AuditLog.BLL/AuditLog.BLL/Interfaces/IAuditEventRepository.cs`
- `AuditLog.BLL/AuditLog.BLL/Interfaces/IAuditIntegrityRepository.cs`
- `AuditLog.BLL/AuditLog.BLL/Models/AuditEventQuery.cs`
- `AuditLog.BLL/AuditLog.BLL/Models/AuditEventQueryResult.cs`
- `AuditLog.BLL/AuditLog.BLL/Models/AuditIntegrityRecord.cs`
- `AuditLog.BLL/AuditLog.BLL/Services/AuditEventQueryService.cs`
- `AuditLog.BLL/AuditLog.BLL/Services/AuditIntegrityService.cs`
- `AuditLog.DAL/AuditLog.DAL/Repositories/AuditEventRepository.cs`
- `AuditLog.DAL/AuditLog.DAL/Repositories/AuditIntegrityRepository.cs`
- `AuditLog.Tests/AuditLog.Tests/AuditEventQueryServiceTests.cs`
- `AuditLog.Tests/AuditLog.Tests/AuditIntegrityServiceTests.cs`
- `AuditLog.Tests/AuditLog.Tests/UnitTest1.cs` (deleted)
- `docs/Scenario-A.md`

**Validation:** The session record reports 7 tests passed for the Scenario A
work. A task-specific test result file and exact command were not retained.
The engineer also reported a successful query response containing a redacted
payload; this confirmed response behavior, not erasure of the stored original
value. On 2026-11-27, the engineer additionally confirmed Scenario A was
tested against SQL Server and database-tampering detection was validated. The
later comprehensive suite recorded below includes the current Scenario A
tests.

### 4. Scenario B retention, redaction, and export

**Date / commit:** 2026-11-26; `e89c345`.

**Task request summary:** Review Scenario B coverage, implement only the
required high-level behavior, update its documentation, and provide database
setup guidance.

**AI contribution:** Extended retention, changed redaction to store the
redacted payload while keeping the original chain hash, added a separate
redacted-content hash, and added filtered JSON/CSV and verifiable bundle
exports with hash-only chain proof. Added migration `004` and updated tests
and documentation.

**Engineer decisions and reported checks:** The original content hash remains
the chain anchor; migration `004_AddRedactedContentHash.sql` is required for
the updated database queries. The engineer reported encountering the missing
column before requesting the migration update. Later, the engineer reported
that Scenarios B and C worked in local validation. On 2026-11-27, the
engineer confirmed migrations `001`–`004` were applied and that retention,
redaction, and export were validated against SQL Server. The exact SQL Server
test steps and independent bundle-proof verification were not recorded.

**Files changed in `e89c345`:**

- `AuditLog.API/AuditLog.API/Controllers/AuditExportController.cs`
- `AuditLog.API/AuditLog.API/Controllers/AuditRedactionController.cs`
- `AuditLog.BLL/AuditLog.BLL/Interfaces/IAuditExportRepository.cs`
- `AuditLog.BLL/AuditLog.BLL/Interfaces/IAuditExportService.cs`
- `AuditLog.BLL/AuditLog.BLL/Interfaces/IAuditRedactionRepository.cs`
- `AuditLog.BLL/AuditLog.BLL/Models/AuditExport.cs`
- `AuditLog.BLL/AuditLog.BLL/Models/AuditExportBundle.cs`
- `AuditLog.BLL/AuditLog.BLL/Models/AuditExportChainRecord.cs`
- `AuditLog.BLL/AuditLog.BLL/Models/AuditIntegrityRecord.cs`
- `AuditLog.BLL/AuditLog.BLL/Models/AuditRedactionRequest.cs`
- `AuditLog.BLL/AuditLog.BLL/Models/AuditRedactionResult.cs`
- `AuditLog.BLL/AuditLog.BLL/Services/AuditExportService.cs`
- `AuditLog.BLL/AuditLog.BLL/Services/AuditIntegrityService.cs`
- `AuditLog.BLL/AuditLog.BLL/Services/AuditRedactionService.cs`
- `AuditLog.DAL/AuditLog.DAL/Repositories/AuditExportRepository.cs`
- `AuditLog.DAL/AuditLog.DAL/Repositories/AuditIntegrityRepository.cs`
- `AuditLog.DAL/AuditLog.DAL/Repositories/AuditRedactionRepository.cs`
- `AuditLog.DAL/AuditLog.DAL/Repositories/AuditRetentionRepository.cs`
- `AuditLog.Tests/AuditLog.Tests/AuditIntegrityServiceTests.cs`
- `AuditLog.Tests/AuditLog.Tests/AuditScenarioBServiceTests.cs`
- `README.md`
- `database/Scripts/004_AddRedactedContentHash.sql`
- `docs/Scenario-B.md`

**Validation:** Scenario B unit tests were added and expanded, but no
task-specific passing test count is recorded. Later, the complete 132-test
suite passed. The engineer later confirmed Scenario B and its retention,
redaction, and export workflows were tested against SQL Server. This
developer-reported validation is separate from the in-memory suite; no
automated SQL integration result artifact is recorded. Independent
recomputation of an export bundle's chain proof was not explicitly confirmed.

### 5. Scenario C compliance reporting and local API-key configuration

**Date / commit:** 2026-11-26; `cdd9978`.

**Task request summary:** Implement an account-scoped, date-filterable
compliance report, protect it with the existing local assessment
authentication, and document Visual Studio configuration.

**AI contribution:** Added the compliance report API/service and tests, wired
the configuration-backed API-key authentication, and documented User Secrets
and environment-variable configuration.

**Engineer decisions and reported checks:** A shared API key was selected
because no identity provider was configured. The key is a local assessment
gate, not user identity or account-level authorization. The engineer reported
that the API returned a response after rebuilding with the User Secrets
configuration; the exact response and request details are not preserved here.
The existing Development launch profiles were used; no `launchSettings.json`
change appears in this commit.

**Files changed in `cdd9978`:**

- `ATTESTATION.md`
- `AuditLog.API/AuditLog.API/AuditLog.API.csproj`
- `AuditLog.API/AuditLog.API/Auth/ComplianceApiKeyAuthenticationHandler.cs`
- `AuditLog.API/AuditLog.API/Auth/ComplianceApiKeyDefaults.cs`
- `AuditLog.API/AuditLog.API/Auth/ComplianceApiKeyValidator.cs`
- `AuditLog.API/AuditLog.API/Controllers/ComplianceReportsController.cs`
- `AuditLog.API/AuditLog.API/DTOs/ComplianceAccessReportQueryRequest.cs`
- `AuditLog.API/AuditLog.API/Program.cs`
- `AuditLog.BLL/AuditLog.BLL/Interfaces/IComplianceReportService.cs`
- `AuditLog.BLL/AuditLog.BLL/Models/ComplianceAccessReportItem.cs`
- `AuditLog.BLL/AuditLog.BLL/Models/ComplianceAccessReportQuery.cs`
- `AuditLog.BLL/AuditLog.BLL/Models/ComplianceAccessReportResult.cs`
- `AuditLog.BLL/AuditLog.BLL/Services/ComplianceReportService.cs`
- `AuditLog.Tests/AuditLog.Tests/ComplianceApiKeyValidatorTests.cs`
- `AuditLog.Tests/AuditLog.Tests/ComplianceReportServiceTests.cs`
- `README.md`
- `docs/AI-Usage-Log.md`
- `docs/Architecture.md`
- `docs/Scenario-C.md`

**Validation:** Tests for the report service and API-key validator were added.
No scenario-specific passing test count is recorded in this commit. The later
comprehensive suite includes these tests. The engineer later confirmed
Scenario C was tested against SQL Server; no separate SQL integration test
artifact or task-specific count is recorded.

### 6. JWT migration review (planning only; not implemented)

**Date / commit:** After the Scenario C work and before the Scenario A/B
authorization update; no commit.

**Task request summary:** Review replacing the API key with JWT Bearer
authentication across the APIs, including signature, issuer, audience,
expiration, and role validation; wait for approval before implementation.

**AI contribution:** Inspected the .NET 10 target and existing authentication
design, then outlined a migration plan. The user subsequently redirected the
work to extending the existing API-key scheme.

**Engineer decision:** JWT migration was not approved or implemented in this
task sequence.

**Files changed:** None. No package or JWT configuration was added.

**Validation:** No build or tests were run for the unimplemented JWT plan.

### 7. Protect Scenario A and B endpoints with the existing API key

**Date / commit:** 2026-11-27; `c28a309`.

**Task request summary:** Apply the existing `X-Compliance-Api-Key`
authentication to Scenario A and B endpoints, reject unauthorized requests,
and do not introduce JWT or another authentication mechanism.

**AI contribution:** Applied the existing authorization policy to the
Scenario A and B controllers, added request-level authorization tests, and
updated the scenario, architecture, and project documentation. The test host
entry point and MVC testing dependency were also added for request-level
tests.

**Engineer decision:** Reused the shared key and policy already used by
Scenario C. This does not separate read/write permissions or establish
individual identity.

**Files changed in `c28a309`:**

- `ATTESTATION.md`
- `AuditLog.API/AuditLog.API/Controllers/AuditEventsController.cs`
- `AuditLog.API/AuditLog.API/Controllers/AuditExportController.cs`
- `AuditLog.API/AuditLog.API/Controllers/AuditIntegrityController.cs`
- `AuditLog.API/AuditLog.API/Controllers/AuditRedactionController.cs`
- `AuditLog.API/AuditLog.API/Controllers/AuditRetentionController.cs`
- `AuditLog.API/AuditLog.API/Program.cs`
- `AuditLog.Tests/AuditLog.Tests/ApiKeyAuthorizationTests.cs`
- `AuditLog.Tests/AuditLog.Tests/AuditEventQueryServiceTests.cs`
- `AuditLog.Tests/AuditLog.Tests/AuditEventWriterServiceTests.cs`
- `AuditLog.Tests/AuditLog.Tests/AuditHashingServiceTests.cs`
- `AuditLog.Tests/AuditLog.Tests/AuditIntegrityServiceTests.cs`
- `AuditLog.Tests/AuditLog.Tests/AuditLog.Tests.csproj`
- `AuditLog.Tests/AuditLog.Tests/TestResults/a844a3f5-f7da-4173-9e30-9590be7c8492/coverage.cobertura.xml`
- `README.md`
- `docs/AI-Usage-Log.md`
- `docs/Architecture.md`
- `docs/Scenario-A.md`
- `docs/Scenario-B.md`
- `docs/Scenario-C.md`

**Validation:** The recorded full suite passed 34 tests, including
authorization checks for all 11 Scenario A and B route mappings. A separate
session summary also records 17 relevant tests passing during targeted
validation. On 2026-11-27, the engineer confirmed API-key authentication works
across all endpoints. No separate automated SQL-backed authorization test
artifact is recorded.

### 8. Comprehensive API and business-logic tests, with coverage reporting

**Date / commit:** 2026-11-27; `c225bd3`.

**Task request summary:** Add meaningful tests for API actions and business
logic, including success, validation, authorization, edge, and error cases;
run the complete suite and document measured coverage.

**AI contribution:** Added direct controller-action tests and expanded
service and authorization tests. Test-host setup uses an unreachable SQL
endpoint for the database-unavailable health response. Added the coverage
summary and linked it from the README. No production business-logic changes
were made for this testing task.

**Files changed in `c225bd3`:**

- `AuditLog.Tests/AuditLog.Tests/ApiControllerTests.cs`
- `AuditLog.Tests/AuditLog.Tests/ApiKeyAuthorizationTests.cs`
- `AuditLog.Tests/AuditLog.Tests/AuditScenarioBServiceTests.cs`
- `AuditLog.Tests/AuditLog.Tests/ComplianceReportServiceTests.cs`
- `AuditLog.Tests/AuditLog.Tests/TestResults/151824fc-5b67-43fa-9032-8b85c08ec791/coverage.cobertura.xml`
- `AuditLog.Tests/AuditLog.Tests/TestResults/a844a3f5-f7da-4173-9e30-9590be7c8492/coverage.cobertura.xml` (deleted/replaced)
- `README.md`
- `docs/Test-Coverage.md`

**Assistant-run validation recorded in** [Test-Coverage.md](Test-Coverage.md):

- `dotnet test AuditLog.Tests\AuditLog.Tests\AuditLog.Tests.csproj`:
  **132 passed, 0 failed, 0 skipped**.
- `dotnet test AuditLog.Tests\AuditLog.Tests\AuditLog.Tests.csproj --collect:"XPlat Code Coverage"`:
  all 132 tests passed.
- `dotnet build AuditLog.API\AuditLog.API\AuditLog.API.csproj --no-restore`:
  succeeded with 0 warnings and 0 errors.
- Overall coverage: **56.20% line** (1,101 / 1,959) and **32.36% branch**
  (156 / 482).
- API: 51.25% line, 12.91% branch; BLL: 99.69% line, 96.87% branch; DAL:
  3.23% line, 0.87% branch.

**Limitations:** Most DAL repository code is SQL Server-backed and is not
exercised by the in-memory tests; the user separately confirmed SQL Server
validation for Scenarios A, B, and C. That manual confirmation does not change
the reported code-coverage figures. The database health success path is not
covered by the recorded automated suite.

### 9. Attestation revision

**Date / commit:** 2026-11-27; `cbbb4f2`.

**Task request summary:** Remove the Draft label, improve the attestation,
trace each scenario to implementation and tests, and accurately state
completed and pending validation.

**AI contribution:** Reorganized the attestation around implementation,
test/coverage evidence, and outstanding SQL and production-identity
validation. The existing owner date and signature were retained as a prior
record and explicitly not extended to later results.

**Engineer decision:** Requested a revision-history approach so the existing
owner entry was preserved without suggesting it signed off on newer results.

**Files changed in `cbbb4f2`:** `ATTESTATION.md`.

**Validation:** Documentation-only. `git diff --check` passed and the
attestation's 18 relative links were checked. No tests were rerun.

### 10. AI usage log consolidation

**Date / commit:** 2026-11-27; this update is not committed at the time of
writing.

**Task request summary:** Review repository files, recent Git history, tests,
documentation, and implementation tasks, then produce a task-wise AI usage
record with exact changed files and verifiable outcomes.

**AI contribution:** Compared the existing log with repository commits,
scenario documents, test and coverage evidence, and recorded task history.
Separated documented AI contributions from baseline implementation whose
authorship cannot be established from Git.

**Files changed:** `docs/AI-Usage-Log.md`.

**Validation:** Documentation-only. The relative link resolves and
`git diff --check` passed. No application tests were run.

### 11. Record engineer-confirmed SQL Server validation

**Date / commit:** 2026-11-27; no commit at the time of this update.

**Engineer confirmation:** The engineer confirmed that database schema
changes are updated and migrations `001`–`004` have been applied; Scenarios A,
B, and C have been tested against SQL Server; API-key authentication works
across all API endpoints; and retention, redaction, export, and database
tampering detection have been validated.

**AI contribution:** Updated this log to distinguish this
developer-confirmed validation from the previously recorded automated test
and coverage results.

**Files changed:** `docs/AI-Usage-Log.md`.

**Evidence limits:** No SQL Server test commands, run logs, or new automated
test counts were supplied with the confirmation. It is recorded as
engineer-reported validation, not as a newly executed or independently
reproduced test run.

## Repository baseline and attribution boundary

The initial project scaffold and implementation commits include
`d1f18a1`, `46e1775`, `a55e3d7`, `039de50`, and `5fa30f5`. They contain the
solution setup, initial audit-event API and database script, and earlier
Scenario A/B and documentation work. The user stated that the project, APIs,
and database schema were already in progress before requesting the coverage
review. Apart from the architecture recommendation documented above, the
available AI-usage and session records do not reliably attribute individual
files in those baseline commits to AI. They are therefore not described here
as AI-generated.

### 12. SQL Server integration tests for all API scenarios

**Date / commit:** 2026-11-27; no commit at the time of this update.

**Task request summary:** Add real SQL Server integration coverage for
Scenarios A, B, and C, including API-key authorization, database operations,
retention, redaction, export, and tampering detection; execute the suite and
record actual results.

**AI contribution:** Added an opt-in test fixture that creates a unique
disposable SQL Server database, applies migrations `001`–`004`, uses the real
API and repository implementations, and drops the test database at teardown.
Added integration cases for all controller routes, successful health checks,
event persistence/query pagination, both integrity routes, retention,
redaction persistence and hash metadata, exports and proof recomputation,
compliance report filtering/pagination, and raw SQL payload tampering.

The route-authentication matrix covers all 13 controller route/method
combinations with missing and invalid keys. Source inspection found the
database health controller lacked the shared authorization policy; the policy
was applied there and its unavailable-database test now sends a valid key.

**Engineer decisions:** The suite requires explicit opt-in and an admin
connection string to `master` on a dedicated test instance. The fixture uses
an isolated generated database name and does not use or drop the application's
configured `AuditLogDb`. The test database was confirmed removed after the
run.

**Files changed:**

- `AuditLog.API/AuditLog.API/Controllers/HealthController.cs`
- `AuditLog.Tests/AuditLog.Tests/ApiKeyAuthorizationTests.cs`
- `AuditLog.Tests/AuditLog.Tests/AuditLog.Tests.csproj`
- `AuditLog.Tests/AuditLog.Tests/SqlServerApiIntegrationTests.cs` (added)
- `README.md`
- `ATTESTATION.md`
- `docs/Scenario-A.md`
- `docs/Scenario-B.md`
- `docs/Scenario-C.md`
- `docs/Test-Coverage.md`
- `docs/AI-Usage-Log.md`

**Assistant-run validation:**

- SQL Server integration filter: **10 passed, 0 failed, 0 skipped**.
- Full suite with integration enabled: **143 passed, 0 failed, 0 skipped**.
- Default full suite without opt-in: **133 passed, 0 failed, 10 skipped**.
- All projects built successfully as part of the full test run.
- Tests ran against LocalDB using a freshly created database; scripts
  `001`–`004` were applied and the database was dropped afterward.

**Coverage note:** The existing line/branch percentages are from the earlier
132-test coverage collection and were not regenerated for this 143-test run.
Concurrent-writer stress, production identity/account-level authorization,
and externally signed or anchored export bundles remain limitations.

### 13. Record engineer-confirmed API-key authorization validation

**Date / commit:** 2026-11-27; no commit at the time of this update.

**Engineer confirmation:** Unauthorized-access handling was validated and is
working. Every controller endpoint, including database health, requires a
valid API key.

**AI contribution:** Updated the README security-testing status to distinguish
the engineer-confirmed validation from the previously recorded automated
authorization and SQL Server integration-test results.

**Files changed:** `README.md` and `docs/AI-Usage-Log.md`.

**Evidence limits:** No additional test command, run log, or test count was
provided with this confirmation. It is recorded as engineer-reported
validation; the earlier automated test results remain separately documented.

## Current pending validation

- Concurrent SQL Server writer stress testing.
- Production identity, per-user/account authorization, and least-privilege
  access policies; the current API key is shared across routes.
- Production deployment and operational validation.


