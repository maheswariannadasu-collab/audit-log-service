
namespace AuditLog.BLL.Models
{
    public class AuditRedactionRequest
    {
        public int AuditEventId { get; set; }

        public List<string> FieldsToRedact { get; set; } = new();

        public string Reason { get; set; } = string.Empty;
        public required string RedactedBy { get; set; }
    }
}
