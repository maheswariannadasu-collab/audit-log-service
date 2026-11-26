
namespace AuditLog.BLL.Interfaces
{
    public interface IAuditExportService
    {
        Task<byte[]> ExportJsonAsync(
            DateTime? fromUtc,
            DateTime? toUtc,
            string? actorId,
            string? resourceId,
            CancellationToken cancellationToken);

        Task<byte[]> ExportCsvAsync(
            DateTime? fromUtc,
            DateTime? toUtc,
            string? actorId,
            string? resourceId,
            CancellationToken cancellationToken);

        Task<byte[]> ExportBundleAsync(
            string? actorId,
            string? resourceId,
            DateTime? fromUtc,
            DateTime? toUtc,
            CancellationToken cancellationToken);
    }
}
