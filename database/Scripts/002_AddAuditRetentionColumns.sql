
-- Add archive metadata to existing audit records.
-- Existing records remain unarchived by default.

IF COL_LENGTH('dbo.AuditEvents', 'IsArchived') IS NULL
BEGIN
    ALTER TABLE dbo.AuditEvents
    ADD IsArchived BIT NOT NULL
        CONSTRAINT DF_AuditEvents_IsArchived DEFAULT (0);
END;
GO

IF COL_LENGTH('dbo.AuditEvents', 'ArchivedAtUtc') IS NULL
BEGIN
    ALTER TABLE dbo.AuditEvents
    ADD ArchivedAtUtc DATETIME2(7) NULL;
END;
GO

-- Helps locate old, unarchived records efficiently.
IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_AuditEvents_Retention'
      AND object_id = OBJECT_ID('dbo.AuditEvents')
)
BEGIN
    CREATE INDEX IX_AuditEvents_Retention
    ON dbo.AuditEvents (IsArchived, EventTimestampUtc);
END;
GO

-- Verify the changes.
SELECT
    Id,
    EventTimestampUtc,
    IsArchived,
    ArchivedAtUtc
FROM dbo.AuditEvents
ORDER BY Id;
GO
