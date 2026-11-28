namespace AuditLog.API.Auth;

public static class JwtPolicyNames
{
    public const string EventsRead = "AuditEventsRead";
    public const string EventsWrite = "AuditEventsWrite";
    public const string IntegrityVerify = "AuditIntegrityVerify";
    public const string RetentionManage = "AuditRetentionManage";
    public const string RedactionManage = "AuditRedactionManage";
    public const string ExportRead = "AuditExportRead";
    public const string ReportsRead = "AuditReportsRead";
}
