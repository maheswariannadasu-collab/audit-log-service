using AuditLog.BLL.Models;

namespace AuditLog.BLL.Interfaces
{
    public interface IAuditRedactionRepository
    {
        Task<string?> GetPayloadAsync(
            long auditEventId,
            CancellationToken cancellationToken);

        Task SaveRedactionAsync(
            AuditRedactionRequest request,
            string redactedPayloadJson,
            CancellationToken cancellationToken);
    }
}
