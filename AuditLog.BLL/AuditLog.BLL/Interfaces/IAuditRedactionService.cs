
using AuditLog.BLL.Models;

namespace AuditLog.BLL.Interfaces
{
    public interface IAuditRedactionService
    {
        string RedactJson(string json);

        Task<AuditRedactionResult> ApplyRedactionAsync(
        AuditRedactionRequest request,
        CancellationToken cancellationToken);
    }
}
