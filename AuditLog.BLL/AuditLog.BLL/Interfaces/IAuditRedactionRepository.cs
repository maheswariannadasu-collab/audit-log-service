using AuditLog.BLL.Models;

namespace AuditLog.BLL.Interfaces
{
    public interface IAuditRedactionRepository
    {
        Task<AuditEventRecord?> GetEventForRedactionAsync(
            long auditEventId,
            CancellationToken cancellationToken);

        Task SaveRedactionAsync(
            AuditRedactionRequest request,
            string redactedPayloadJson,
            byte[] redactedContentHash,
            CancellationToken cancellationToken);
    }
}
