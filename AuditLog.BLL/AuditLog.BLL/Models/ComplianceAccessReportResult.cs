namespace AuditLog.BLL.Models;

public sealed class ComplianceAccessReportResult
{
    public string AccountId { get; init; } = string.Empty;

    public DateTimeOffset? FromUtc { get; init; }

    public DateTimeOffset? ToUtc { get; init; }

    public IReadOnlyList<ComplianceAccessReportItem> Items { get; init; } =
        Array.Empty<ComplianceAccessReportItem>();

    public long TotalCount { get; init; }

    public int PageNumber { get; init; }

    public int PageSize { get; init; }
}
