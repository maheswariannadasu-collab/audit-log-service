
-- Scenario B: Structured Redaction

IF OBJECT_ID('dbo.AuditEventRedactions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditEventRedactions
    (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,

        AuditEventId BIGINT NOT NULL,

        RedactedPayloadJson NVARCHAR(MAX) NOT NULL,

        RedactedFieldsJson NVARCHAR(MAX) NOT NULL,

        Reason NVARCHAR(500) NOT NULL,

        RedactedBy NVARCHAR(150) NOT NULL,

        RedactedAtUtc DATETIME2(7) NOT NULL
            CONSTRAINT DF_AuditEventRedactions_RedactedAtUtc
            DEFAULT SYSUTCDATETIME(),

        CONSTRAINT FK_AuditEventRedactions_AuditEvents
            FOREIGN KEY (AuditEventId)
            REFERENCES dbo.AuditEvents(Id),

        CONSTRAINT UQ_AuditEventRedactions_AuditEventId
            UNIQUE (AuditEventId)
    );

    PRINT 'AuditEventRedactions table created.';
END
ELSE
BEGIN
    PRINT 'AuditEventRedactions already exists.';
END;
GO

-- Verify table and constraints
SELECT *
FROM dbo.AuditEventRedactions;
GO
