namespace AuditLog.BLL.Models;

public sealed class AuditExportChainRecord
{
    public long Id { get; init; }

    public string ContentHash { get; init; } = string.Empty;

    public string PreviousHash { get; init; } = string.Empty;

    public string RecordHash { get; init; } = string.Empty;
}
