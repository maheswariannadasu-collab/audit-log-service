# Scenario C – Compliance Reporting

## Requirement

Regulators need to audit access to client account data, retrieve reports by
account and date range, and limit report access to authorized users.

## Assumptions

- Audit events for client accounts use `ResourceType = "ACCOUNT"` and place the
  account identifier in `ResourceId`.
- Events written for those resources represent the account activity the
  regulator needs to review. The report includes all matching event types so
  new account activity types are not silently omitted.
- Report date filters are optional; when supplied, `fromUtc` is inclusive and
  `toUtc` is exclusive.
- This local assessment has no configured identity provider. A configuration-
  supplied API key protects the Scenario A, B, and C API endpoints; production
  should use the organization's identity provider and user-level authorization.

## Implementation

`GET /api/compliance-reports/accounts/{accountId}/access` returns a paginated
report for one account. It reuses the existing audit-event query path, constrains
results to `ResourceType = "ACCOUNT"` and the requested account ID, applies
optional date bounds, and returns event ID, event type, actor, account, event
timestamp, and payload. The existing query repository returns the stored
redacted payload when a redaction record exists.

The report uses the existing pagination limits: page size defaults to 50 and
cannot exceed 100. Results follow the existing event query order (newest ID
first). An account with no matching audit events returns an empty page.

### Access control

All Scenario A, B, and C API controllers require authentication. Callers must
provide the `X-Compliance-Api-Key` header. The key is read from the
`ComplianceReporting:ApiKey` configuration value. For local Visual Studio
development, store it with User Secrets:

```powershell
dotnet user-secrets set "ComplianceReporting:ApiKey" "<your-random-32-plus-character-key>" --project AuditLog.API\AuditLog.API\AuditLog.API.csproj
```

The API launch profiles use the Development environment, so ASP.NET Core loads
the secret automatically. The `ComplianceReporting__ApiKey` environment
variable is also supported for command-line runs. Keys shorter than 32
characters are rejected; the comparison uses fixed-time hash comparison.

Requests without a valid configured key receive HTTP 401. The configured key
is a shared local-assessment credential; it does not identify individual
regulators or provide per-user audit attribution. Do not commit the key to
source control.

### Example request

```http
GET /api/compliance-reports/accounts/ACCOUNT100/access?fromUtc=2026-01-01T00:00:00Z&toUtc=2026-02-01T00:00:00Z&pageNumber=1&pageSize=50
X-Compliance-Api-Key: <configured-key>
```

### Example response

```json
{
  "accountId": "ACCOUNT100",
  "fromUtc": "2026-01-01T00:00:00+00:00",
  "toUtc": "2026-02-01T00:00:00+00:00",
  "items": [
    {
      "eventId": 15,
      "eventType": "ACCOUNT_VIEWED",
      "actorId": "USER001",
      "accountId": "ACCOUNT100",
      "eventTimestampUtc": "2026-01-15T10:30:00+00:00",
      "payload": {
        "ipAddress": "192.0.2.10",
        "device": "Chrome",
        "outcome": "success"
      }
    }
  ],
  "totalCount": 1,
  "pageNumber": 1,
  "pageSize": 50
}
```

## Database impact

No new database table or migration is required. The report relies on
`AuditEvents` and the existing indexes and redaction-aware query implementation.
For the API to return useful reports, account activity must be written with
`ResourceType = "ACCOUNT"` and the correct account ID as `ResourceId`.

## Unit test coverage

Unit tests verify account scoping, the `ACCOUNT` resource-type constraint,
date-bound and pagination forwarding, report-field projection, invalid account
and date-range handling, and API-key acceptance/rejection behavior.

SQL Server integration tests exercise the report endpoint against persisted
account events, including date filtering and pagination. The engineer confirmed
Scenario C was also tested against the target SQL Server database and that
migrations `001`–`004` have been applied. See
[Test-Coverage.md](Test-Coverage.md) for the integration-test command and
results.

## Remaining production considerations

The API key is intentionally a small local-assessment access gate, not a
replacement for an identity provider. The shared key also grants access to
Scenario A and B operations, including writes, retention, and redaction.
Production deployment should use an identity provider, assign least-privilege
roles and account scopes, store secrets in a secret manager, and audit report
access by the authenticated user.
