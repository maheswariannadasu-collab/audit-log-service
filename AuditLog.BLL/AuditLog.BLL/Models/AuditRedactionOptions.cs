
namespace AuditLog.BLL.Models
{
    public class AuditRedactionOptions
    {
        public List<string> SensitiveFields { get; set; } = new();

        public string ReplacementValue { get; set; } = "[REDACTED]";
    }
}
