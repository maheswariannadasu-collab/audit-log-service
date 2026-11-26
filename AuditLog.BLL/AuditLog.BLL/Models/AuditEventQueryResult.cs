namespace AuditLog.BLL.Models;

public sealed class AuditEventQueryResult
{
    public IReadOnlyList<AuditEventRecord> Items { get; init; } =
        Array.Empty<AuditEventRecord>();

    public long TotalCount { get; init; }

    public int PageNumber { get; init; }

    public int PageSize { get; init; }
}
