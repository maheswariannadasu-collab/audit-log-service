using AuditLog.BLL.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AuditLog.BLL.Interfaces
{
    public interface IAuditIntegrityService
    {
        Task<AuditIntegrityResult> VerifyAsync(
            CancellationToken cancellationToken = default);
    }
}
