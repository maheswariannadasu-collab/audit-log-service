namespace AuditLog.API.DTOs
{
    public sealed class CreateAuditEventResponse
    {
        public long Id { get; init; }

        public string Message { get; init; } = string.Empty;
    }
}
