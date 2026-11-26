using System.Text.Json;

namespace AuditLog.BLL.Models;

public sealed class ComplianceAccessReportItem
{
    public long EventId { get; init; }

    public string EventType { get; init; } = string.Empty;

    public string ActorId { get; init; } = string.Empty;

    public string AccountId { get; init; } = string.Empty;

    public DateTimeOffset EventTimestampUtc { get; init; }

    public JsonElement Payload { get; init; }
}
