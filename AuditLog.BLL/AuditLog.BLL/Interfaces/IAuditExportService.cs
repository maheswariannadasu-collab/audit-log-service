
namespace AuditLog.BLL.Interfaces
{
    public interface IAuditExportService
    {
        Task<byte[]> ExportJsonAsync(
            DateTime? fromUtc,
            DateTime? toUtc,
            CancellationToken cancellationToken);

        Task<byte[]> ExportCsvAsync(
            DateTime? fromUtc,
            DateTime? toUtc,
            CancellationToken cancellationToken);
    }
}
