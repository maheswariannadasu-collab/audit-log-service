namespace AuditLog.BLL.Interfaces
{
    public interface IAuditRetentionService
    {
        Task<int> ArchiveExpiredEventsAsync(
            CancellationToken cancellationToken);
    }
}
