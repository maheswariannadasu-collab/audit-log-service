using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using System.Text.Json;

namespace AuditLog.BLL.Services;

public sealed class ComplianceReportService : IComplianceReportService
{
    private readonly AuditEventQueryService _eventQueryService;

    public ComplianceReportService(
        AuditEventQueryService eventQueryService)
    {
        _eventQueryService = eventQueryService;
    }

    public async Task<ComplianceAccessReportResult> GetAccountAccessReportAsync(
        ComplianceAccessReportQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.AccountId) ||
            query.AccountId.Length > 150)
        {
            throw new ArgumentException(
                "AccountId must contain 1 to 150 characters.",
                nameof(query));
        }

        var page = await _eventQueryService.QueryAsync(
            new AuditEventQuery
            {
                ResourceType = "ACCOUNT",
                ResourceId = query.AccountId,
                FromUtc = query.FromUtc,
                ToUtc = query.ToUtc,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize
            },
            cancellationToken);

        var items = page.Items
            .Select(record =>
            {
                using var payloadDocument =
                    JsonDocument.Parse(record.PayloadJson);

                return new ComplianceAccessReportItem
                {
                    EventId = record.Id,
                    EventType = record.EventType,
                    ActorId = record.ActorId,
                    AccountId = record.ResourceId,
                    EventTimestampUtc = new DateTimeOffset(
                        DateTime.SpecifyKind(
                            record.EventTimestampUtc,
                            DateTimeKind.Utc)),
                    Payload = payloadDocument.RootElement.Clone()
                };
            })
            .ToArray();

        return new ComplianceAccessReportResult
        {
            AccountId = query.AccountId,
            FromUtc = query.FromUtc?.ToUniversalTime(),
            ToUtc = query.ToUtc?.ToUniversalTime(),
            Items = items,
            TotalCount = page.TotalCount,
            PageNumber = page.PageNumber,
            PageSize = page.PageSize
        };
    }
}
