using AuditLog.BLL.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AuditLog.BLL.Interfaces
{
    public interface IAuditEventRepository
    {
        Task<long> InsertAsync(
            AuditEventData auditEvent,
            CancellationToken cancellationToken = default);
    }
}
