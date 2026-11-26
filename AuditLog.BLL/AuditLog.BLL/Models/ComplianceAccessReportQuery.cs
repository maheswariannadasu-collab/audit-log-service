namespace AuditLog.BLL.Models;

public sealed class ComplianceAccessReportQuery
{
    public string AccountId { get; init; } = string.Empty;

    public DateTimeOffset? FromUtc { get; init; }

    public DateTimeOffset? ToUtc { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = AuditEventQuery.DefaultPageSize;
}
