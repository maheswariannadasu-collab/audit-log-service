using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace AuditLog.API.DTOs
{
    public sealed class CreateAuditEventRequest
    {
        [Required(ErrorMessage = "AccountId is required.")]
        [StringLength(150, MinimumLength = 1,
            ErrorMessage = "AccountId must contain 1 to 150 characters.")]
        public string AccountId { get; init; } = string.Empty;

        [Required(ErrorMessage = "Action is required.")]
        [StringLength(100, MinimumLength = 1,
            ErrorMessage = "Action must contain 1 to 100 characters.")]
        public string Action { get; init; } = string.Empty;

        [Range(typeof(decimal), "0", "79228162514264337593543950335",
            ErrorMessage = "Amount must be greater than or equal to 0.")]
        public decimal? Amount { get; init; }

        [Required]
        [StringLength(100)]
        public string EventType { get; init; } = string.Empty;

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
