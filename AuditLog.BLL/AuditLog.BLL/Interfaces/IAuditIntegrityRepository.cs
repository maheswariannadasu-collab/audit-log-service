using AuditLog.BLL.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AuditLog.BLL.Interfaces
{
    public interface IAuditIntegrityRepository
    {
        IAsyncEnumerable<AuditIntegrityRecord> StreamForIntegrityCheckAsync(
            CancellationToken cancellationToken = default);
    }
}
