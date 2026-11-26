
namespace AuditLog.BLL.Interfaces
{
    public interface IAuditRetentionRepository
    {
        Task<int> ArchiveOlderThanAsync(
            DateTime cutoffUtc,
            CancellationToken cancellationToken);
    }
}
