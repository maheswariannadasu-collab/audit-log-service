# Scenario A – Core Audit Logging

## Requirement and assumptions

Record audit events in an append-only history, support filtered and paginated
queries, and detect changes to stored events through hash-chain verification.

- Event timestamps are supplied by the caller and must be present. The API
  stores them in UTC.
- Query `FromUtc` is inclusive and `ToUtc` is exclusive. Supplied offsets are
  converted to UTC for database comparison.
- Query results are ordered by descending database ID. This is deterministic
  and follows append order, independently of caller-supplied event timestamps.
- The first record's previous hash is 32 zero bytes.

## API

| Method and route | Behavior |
|---|---|
| `POST /api/audit-events` | Append an event with event type, actor, resource, timestamp, and JSON-object payload. |
| `GET /api/audit-events/{id}` | Retrieve one event by ID. |
| `GET /api/audit-events` | Query by any combination of `actorId`, `resourceType`, `resourceId`, `eventType`, `fromUtc`, and `toUtc`. |
| `GET /audit/verify` | Verify the full chain; returns `200` when intact and `409` with the first failure when invalid. |
| `GET /api/audit-integrity/verify` | Existing verification route retained for compatibility. |

## Access control

All Scenario A endpoints require the existing `X-Compliance-Api-Key` header.
The key is read from `ComplianceReporting:ApiKey`; configure it using the
User Secrets or environment-variable instructions in
[Scenario C](Scenario-C.md#access-control). Requests with a missing or invalid
key receive HTTP 401.

Query pagination uses `pageNumber` (default `1`) and `pageSize` (default `50`,
maximum `100`). The response includes `items`, `totalCount`, `pageNumber`, and
`pageSize`. Invalid page values, overlong filters, and a `fromUtc` value that
is not earlier than `toUtc` are rejected with `400`.

## Hash-chain design

The content hash is SHA-256 over the event type, actor ID, resource type,
resource ID, UTC timestamp in round-trip format, and structured payload. Payload
object properties are serialized in ordinal name order; array order is
preserved.

```text
ContentHash = SHA256(canonical event content)
RecordHash  = SHA256(PreviousHash bytes || ContentHash bytes)
```

Each record stores its content hash, previous hash, and record hash. The next
record links to the prior record hash. Inserts use a serializable transaction
and a SQL Server application lock so concurrent writers cannot create
competing chain links. Verification scans records in ascending ID order and
checks the previous-hash link, content hash, and record hash. It stops at the
first inconsistency and identifies the record and failure type. Records are
streamed from the database, avoiding an in-memory copy of the full chain during
verification.

## Append-only behavior and boundaries

The API provides no event update or delete operation. The prototype does not
configure database-role permissions that independently deny direct SQL updates
or deletes; the hash-chain verification is intended to detect unauthorized
changes, not prevent privileged database access.

The query endpoint returns a redacted payload when a redaction record exists,
while the chain hashes remain those of the originally written event. Redaction
storage and privacy limitations are covered separately in Scenario B.

## Validation

The unit tests cover query validation and hash-chain verification, including
detection of a changed payload. SQL Server integration tests also cover event
creation, retrieval, filtered/paginated querying, both verification routes, and
detection of direct database payload tampering.

Run the integration suite as documented in
[Test-Coverage.md](Test-Coverage.md). The engineer confirmed the target
database has migrations `001`–`004` applied and Scenario A was tested against
SQL Server.

Concurrent database-writer stress testing and direct database permission
restrictions are not covered by the current automated tests. The API does not
provide an event update or delete operation; database hash verification detects
changes but does not prevent privileged direct SQL changes.

## Status

The write API, hash-chain creation and verification, filtered/paginated query
API, and required verification route are implemented. Scenario A's SQL Server
integration tests pass; concurrent-writer stress testing remains pending.