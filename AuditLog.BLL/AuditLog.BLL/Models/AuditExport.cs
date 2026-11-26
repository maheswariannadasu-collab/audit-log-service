
namespace AuditLog.BLL.Models
{
    public class AuditExport
    {
        public long Id { get; set; }

        public string EventType { get; set; } = string.Empty;

        public string ResourceType { get; set; } = string.Empty;

        public string ResourceId { get; set; } = string.Empty;

        public DateTime EventTimestampUtc { get; set; }

        public bool IsArchived { get; set; }

        public string ContentHash { get; set; } = string.Empty;

        public string PreviousHash { get; set; } = string.Empty;
    }
}
