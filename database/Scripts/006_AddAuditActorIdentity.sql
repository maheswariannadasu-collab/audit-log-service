-- Store server-verified JWT actor attribution on audit events.

IF COL_LENGTH('dbo.AuditEvents', 'CreatedBySubject') IS NULL
BEGIN
    ALTER TABLE dbo.AuditEvents
    ADD CreatedBySubject NVARCHAR(150) NULL;
END;
GO

IF COL_LENGTH('dbo.AuditEvents', 'CreatedByDisplayName') IS NULL
BEGIN
    ALTER TABLE dbo.AuditEvents
    ADD CreatedByDisplayName NVARCHAR(150) NULL;
END;
GO
