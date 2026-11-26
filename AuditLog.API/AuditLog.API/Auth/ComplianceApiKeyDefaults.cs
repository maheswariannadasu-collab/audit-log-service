namespace AuditLog.API.Auth;

public static class ComplianceApiKeyDefaults
{
    public const string SchemeName = "ComplianceApiKey";
    public const string PolicyName = "ComplianceReportReader";
    public const string HeaderName = "X-Compliance-Api-Key";
    public const string RoleName = "ComplianceReportReader";
    public const string ConfigurationKey = "ComplianceReporting:ApiKey";
}
