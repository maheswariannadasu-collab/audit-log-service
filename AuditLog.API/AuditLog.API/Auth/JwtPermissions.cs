namespace AuditLog.API.Auth;

public static class JwtPermissions
{
    public const string EventsRead = "audit.events.read";
    public const string EventsWrite = "audit.events.write";
    public const string IntegrityVerify = "audit.integrity.verify";
    public const string RetentionManage = "audit.retention.manage";
    public const string RedactionManage = "audit.redaction.manage";
    public const string ExportRead = "audit.export.read";
    public const string ReportsRead = "audit.reports.read";
    public const string AccountsManage = "audit.accounts.manage";

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> DemoUsers =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
        {
            ["AuditWriter"] =
            [
                EventsWrite
            ],
            ["Auditor"] =
            [
                EventsRead,
                IntegrityVerify,
                ReportsRead
            ],
            ["ComplianceReader"] =
            [
                ReportsRead
            ],
            ["SecurityOperator"] =
            [
                RetentionManage,
                RedactionManage,
                ExportRead
            ],
            ["Administrator"] =
            [
                EventsRead,
                EventsWrite,
                IntegrityVerify,
                RetentionManage,
                RedactionManage,
                ExportRead,
                ReportsRead,
                AccountsManage
            ]
        };
}
