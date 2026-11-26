using AuditLog.BLL.Models;

namespace AuditLog.BLL.Interfaces;

public interface IComplianceReportService
{
    Task<ComplianceAccessReportResult> GetAccountAccessReportAsync(
        ComplianceAccessReportQuery query,
        CancellationToken cancellationToken = default);
}
