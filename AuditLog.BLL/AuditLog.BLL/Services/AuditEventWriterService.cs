using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;

namespace AuditLog.BLL.Services;

public sealed class AuditEventWriterService
{
    private readonly IAuditEventRepository _repository;

    public AuditEventWriterService(
        IAuditEventRepository repository)
    {
        _repository = repository;
    }

    public async Task<AuditEventWriteResult> WriteAsync(
        AuditEventData auditEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        return await _repository.InsertAsync(
            auditEvent,
            cancellationToken);
    }
}