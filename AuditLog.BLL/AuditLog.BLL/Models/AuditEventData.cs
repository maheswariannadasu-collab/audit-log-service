
using System.Text.Json;

namespace AuditLog.BLL.Models;

public sealed class AuditEventData
{
    public string EventType { get; init; } = string.Empty;

    public string ActorId { get; init; } = string.Empty;

    public string CreatedBySubject { get; init; } = string.Empty;

    public string CreatedByDisplayName { get; init; } = string.Empty;

    public string ResourceType { get; init; } = string.Empty;

    public string ResourceId { get; init; } = string.Empty;

    public JsonElement Payload { get; init; }

    public DateTimeOffset EventTimestampUtc { get; init; }
}
