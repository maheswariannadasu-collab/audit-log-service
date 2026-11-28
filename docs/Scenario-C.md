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
- This local assessment uses local JWTs with OIDC-compatible claims. It does
  not connect to an external identity provider. Production should replace local
  signing and claim-based account mapping with the organization identity
  provider and a database-backed assignment lookup.

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

All Scenario A, B, and C API controllers require JWT Bearer authentication.
The local token endpoint is `POST /auth/token`. Supported demo users are
`AuditWriter`, `Auditor`, `ComplianceReader`, `SecurityOperator`, and
`Administrator`.

```powershell
dotnet user-secrets set "Jwt:SigningKey" "<random-key-of-at-least-32-characters>" --project AuditLog.API\AuditLog.API\AuditLog.API.csproj
```

The local mode validates issuer, audience, signature, expiry, and lifetime
with zero clock skew. The issuer and audience come from `Jwt:Issuer` and
`Jwt:Audience`; `Jwt:Authority` remains configurable for future migration.
Requests without a valid token receive HTTP 401. A valid token without
`audit.reports.read` receives HTTP 403.

For account access, the route account ID must equal the JWT `accountId` claim
unless the token has `audit.accounts.manage`. The report service always receives
the authorized route account ID as its account filter.

### Example request

```http
GET /api/compliance-reports/accounts/ACCOUNT100/access?fromUtc=2026-01-01T00:00:00Z&toUtc=2026-02-01T00:00:00Z&pageNumber=1&pageSize=50
Authorization: Bearer <local-JWT>
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
and date-range handling, and JWT permission/account assignment behavior.

SQL Server integration tests exercise the report endpoint against persisted
account events, including date filtering and pagination. The engineer confirmed
Scenario C was also tested against the target SQL Server database and that
migrations `001`–`004` have been applied. See
[Test-Coverage.md](Test-Coverage.md) for the integration-test command and
results.

## Remaining production considerations

The JWT implementation is intentionally local and claim-based for this
assignment. Production deployment should use an identity provider, replace
the claim mapping with database-backed account assignments, store secrets in
a secret manager, and separate read/write permissions.
