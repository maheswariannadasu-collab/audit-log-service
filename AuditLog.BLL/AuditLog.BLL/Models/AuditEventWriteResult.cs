namespace AuditLog.BLL.Models;

public sealed class AuditEventWriteResult
{
    public long Id { get; init; }
    public bool IsReplay { get; init; }
}
