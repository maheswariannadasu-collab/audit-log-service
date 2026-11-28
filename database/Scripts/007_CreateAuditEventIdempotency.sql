-- Store POST /api/audit-events idempotency records only.

IF OBJECT_ID('dbo.AuditEventIdempotency', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditEventIdempotency
    (
        Id BIGINT IDENTITY(1,1) NOT NULL,
        IdempotencyKey NVARCHAR(128) NOT NULL,
        CreatedBySubject NVARCHAR(150) NOT NULL,
        RequestHash VARBINARY(32) NOT NULL,
        AuditEventId BIGINT NOT NULL,
        CreatedAtUtc DATETIME2(7) NOT NULL
            CONSTRAINT DF_AuditEventIdempotency_CreatedAtUtc
            DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_AuditEventIdempotency PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_AuditEventIdempotency_SubjectKey
            UNIQUE (CreatedBySubject, IdempotencyKey),
        CONSTRAINT FK_AuditEventIdempotency_AuditEvent
            FOREIGN KEY (AuditEventId)
            REFERENCES dbo.AuditEvents(Id),
        CONSTRAINT CK_AuditEventIdempotency_RequestHashLength
            CHECK (DATALENGTH(RequestHash) = 32)
    );
END;
GO
