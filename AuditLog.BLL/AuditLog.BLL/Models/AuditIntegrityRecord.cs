namespace AuditLog.BLL.Models;

public sealed class AuditIntegrityRecord
{
    public required AuditEventRecord Event { get; init; }

    public long TotalRecords { get; init; }

    public byte[]? RedactedContentHash { get; init; }
}
