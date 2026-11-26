
namespace AuditLog.BLL.Models
{
    public class AuditRedactionResult
    {
        public long AuditEventId { get; set; }

        public List<string> RedactedFields { get; set; } = new();

        public string Status { get; set; } = string.Empty;

        public DateTime RedactedAtUtc { get; set; }
    }
}
