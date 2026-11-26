using System.Text.Json;

namespace AuditLog.API.DTOs
{
    public sealed class AuditEventResponse
    {
        public long Id { get; init; }

        public string EventType { get; init; } = string.Empty;

        public string ActorId { get; init; } = string.Empty;

        public string ResourceType { get; init; } = string.Empty;

        public string ResourceId { get; init; } = string.Empty;

        public DateTime EventTimestampUtc { get; init; }

        public JsonElement Payload { get; init; }

        public string ContentHash { get; init; } = string.Empty;

        public string PreviousHash { get; init; } = string.Empty;

        public string RecordHash { get; init; } = string.Empty;
    }
}
