using AuditLog.BLL.Models;
using System.ComponentModel.DataAnnotations;

namespace AuditLog.API.DTOs;

public sealed class ComplianceAccessReportQueryRequest
{
    public DateTimeOffset? FromUtc { get; init; }

    public DateTimeOffset? ToUtc { get; init; }

    [Range(1, int.MaxValue)]
    public int PageNumber { get; init; } = 1;

    [Range(1, AuditEventQuery.MaximumPageSize)]
    public int PageSize { get; init; } = AuditEventQuery.DefaultPageSize;
}
