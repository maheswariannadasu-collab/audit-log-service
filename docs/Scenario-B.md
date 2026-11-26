# Scenario B – Retention, Redaction, and Verifiable Export

## Requirement and implementation status

Scenario B extends the append-only audit log with configurable archival,
structured payload redaction, and targeted export.

| Capability | Implemented behavior |
|---|---|
| Retention | `POST /api/audit-retention/archive` marks records older than the configured `AuditRetention:RetentionDays` as archived. Rows remain in the chain, so verification includes archived records. Repeated runs only update unarchived rows. |
| Redaction | `POST /api/audit-redaction/apply` replaces configured payload fields in the live `AuditEvents.PayloadJson` row, stores redaction metadata, and retains the original content hash as the chain anchor. |
| Export | `GET /api/audit-export/bundle` exports records selected by `actorId` or `resourceId`, with optional UTC date bounds, event hashes, genesis hash, chain tip, and hash-only proof records through the latest selected event. |
| Existing exports | JSON and CSV routes remain available and now accept optional `actorId` and `resourceId` filters. They include payload and complete hash fields. |

`fromUtc` is inclusive and `toUtc` is exclusive. A bundle request must specify
at least one of `actorId` or `resourceId`. If both are supplied, both filters
apply.

## Redaction design and trade-offs

For a new redaction, the service:

1. Reads the original event and applies the configured field replacements.
2. Computes a hash of the redacted event representation.
3. In one SQL transaction, overwrites `AuditEvents.PayloadJson` and records
   redaction metadata plus the redacted-content hash.

The original `ContentHash` and `RecordHash` are not changed. The original
content hash remains the value linked into the chain; verification checks the
current redacted payload against `RedactedContentHash`, then checks the
record-hash link against the original `ContentHash`. This preserves chain
verification while detecting later edits to the redacted representation.

Existing redaction rows created before this change have no
`RedactedContentHash` and may still have the original payload in the live
event row. After applying database script
[`004_AddRedactedContentHash.sql`](../database/Scripts/004_AddRedactedContentHash.sql),
re-apply redaction to those events to overwrite the live payload and establish
the redacted-content hash.

Overwriting the current row does not guarantee erasure from SQL transaction
logs, backups, or previously exported files. The original content hash also
remains as a chain anchor. Do not log passwords or credentials in the first
place; production use needs a defined backup/crypto-erasure policy and
appropriate key management.

## Export bundle verification

The bundle has two collections:

- `events`: matching event data and its content, redacted-content (when
  applicable), previous, and record hashes.
- `chainProof`: hash-only rows for the chain from its beginning through the
  latest matching event. It contains no payloads for nonmatching events.

A recipient can validate the proof in ascending ID order by starting with
`genesisHash`, checking each `previousHash` against the previous record hash,
and recomputing each record hash as
`SHA256(previousHash bytes || contentHash bytes)`. For each exported event, the
recipient also recomputes its canonical content hash and compares it with
`contentHash`, or `redactedContentHash` if the event is redacted.

This prototype does not digitally sign or externally anchor the bundle. The
chain proof detects ordinary payload/link changes, but a party able to rewrite
the entire bundle can recompute its hashes. A production design should publish
or sign chain checkpoints with a separately protected key.

## Database setup

Apply scripts `001`, `002`, `003`, then `004` in order. Script `004` adds the
redacted-content hash used by verification and export. No script removes old
redaction values from backups or transaction logs.

## Validation

Unit tests cover retention-window calculation, redaction replacement and hash
generation, export bundle construction/filtering, bundle filter validation,
and verification of redacted chain records. They use in-memory repositories;
SQL transaction behavior, retention updates, and exports still require
end-to-end validation against SQL Server.

## Status and remaining validation

The retention, redaction, and export APIs and their service/data-layer
implementations are present. Before treating Scenario B as validated, run the
API against SQL Server after applying the scripts, confirm archived rows still
verify, confirm redaction overwrites the live payload and the chain remains
valid, and download and independently verify an export bundle.
