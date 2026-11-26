using AuditLog.BLL.Models;

namespace AuditLog.BLL.Interfaces
{
    public interface IAuditExportRepository
    {
        Task<List<AuditExport>> GetAuditEventsAsync(
            DateTime? fromUtc,
            DateTime? toUtc,
            CancellationToken cancellationToken);
    }
}
