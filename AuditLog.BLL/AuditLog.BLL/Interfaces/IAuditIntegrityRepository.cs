using AuditLog.BLL.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AuditLog.BLL.Interfaces
{
    public interface IAuditIntegrityRepository
    {
        Task<List<AuditEventRecord>> GetAllForIntegrityCheckAsync(
            CancellationToken cancellationToken = default);
    }
}
