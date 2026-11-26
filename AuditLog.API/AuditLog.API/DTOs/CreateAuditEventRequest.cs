using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace AuditLog.API.DTOs
{
    public sealed class CreateAuditEventRequest
    {
        [Required]
        [StringLength(100)]
        public string EventType { get; init; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string ActorId { get; init; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string ResourceType { get; init; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string ResourceId { get; init; } = string.Empty;

        public DateTimeOffset? EventTimestampUtc { get; init; }

        public JsonElement Payload { get; init; }
    }
}
