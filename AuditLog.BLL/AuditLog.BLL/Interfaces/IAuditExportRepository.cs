using AuditLog.BLL.Models;

namespace AuditLog.BLL.Interfaces
{
    public interface IAuditExportRepository
    {
        Task<List<AuditExport>> GetAuditEventsAsync(
            DateTime? fromUtc,
            DateTime? toUtc,
            string? actorId,
            string? resourceId,
            CancellationToken cancellationToken);

        Task<List<AuditExportChainRecord>> GetChainProofAsync(
            long throughId,
            CancellationToken cancellationToken);
    }
}
