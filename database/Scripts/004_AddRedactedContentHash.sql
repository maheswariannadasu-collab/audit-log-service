-- Store an integrity hash for the current redacted payload while keeping the
-- original content hash as the immutable chain anchor.

IF COL_LENGTH(
    'dbo.AuditEventRedactions',
    'RedactedContentHash') IS NULL
BEGIN
    ALTER TABLE dbo.AuditEventRedactions
    ADD RedactedContentHash VARBINARY(32) NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = 'CK_AuditEventRedactions_RedactedContentHash_Length'
      AND parent_object_id = OBJECT_ID('dbo.AuditEventRedactions')
)
BEGIN
    ALTER TABLE dbo.AuditEventRedactions
    ADD CONSTRAINT CK_AuditEventRedactions_RedactedContentHash_Length
    CHECK
    (
        RedactedContentHash IS NULL
        OR DATALENGTH(RedactedContentHash) = 32
    );
END;
GO
