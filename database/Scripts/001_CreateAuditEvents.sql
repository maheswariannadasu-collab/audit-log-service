
USE AuditLogDb;
GO

CREATE TABLE dbo.AuditEvents
(
    Id BIGINT IDENTITY(1,1) NOT NULL,

    EventType NVARCHAR(100) NOT NULL,

    ActorId NVARCHAR(150) NOT NULL,

    ResourceType NVARCHAR(100) NOT NULL,

    ResourceId NVARCHAR(150) NOT NULL,

    PayloadJson NVARCHAR(MAX) NOT NULL,

    EventTimestampUtc DATETIME2(7) NOT NULL,

    ContentHash VARBINARY(32) NOT NULL,

    PreviousHash VARBINARY(32) NOT NULL,

    RecordHash VARBINARY(32) NOT NULL,

    CreatedAtUtc DATETIME2(7) NOT NULL
        CONSTRAINT DF_AuditEvents_CreatedAtUtc
        DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_AuditEvents
        PRIMARY KEY CLUSTERED (Id),

    CONSTRAINT CK_AuditEvents_PayloadJson
        CHECK (ISJSON(PayloadJson) = 1),

    CONSTRAINT UQ_AuditEvents_RecordHash
        UNIQUE (RecordHash),

    CONSTRAINT CK_AuditEvents_ContentHash_Length
        CHECK (DATALENGTH(ContentHash) = 32),

    CONSTRAINT CK_AuditEvents_PreviousHash_Length
        CHECK (DATALENGTH(PreviousHash) = 32),

    CONSTRAINT CK_AuditEvents_RecordHash_Length
        CHECK (DATALENGTH(RecordHash) = 32)
);
GO

CREATE INDEX IX_AuditEvents_ActorId
ON dbo.AuditEvents (ActorId, Id);
GO

CREATE INDEX IX_AuditEvents_Resource
ON dbo.AuditEvents (ResourceType, ResourceId, Id);
GO

CREATE INDEX IX_AuditEvents_EventType
ON dbo.AuditEvents (EventType, Id);
GO

CREATE INDEX IX_AuditEvents_EventTimestampUtc
ON dbo.AuditEvents (EventTimestampUtc, Id);
GO
