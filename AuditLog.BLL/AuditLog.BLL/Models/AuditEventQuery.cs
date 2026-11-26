namespace AuditLog.BLL.Models;

public sealed class AuditEventQuery
{
    public const int DefaultPageSize = 50;
    public const int MaximumPageSize = 100;

    public string? ActorId { get; init; }

    public string? ResourceType { get; init; }

    public string? ResourceId { get; init; }

    public string? EventType { get; init; }

    public DateTimeOffset? FromUtc { get; init; }

    public DateTimeOffset? ToUtc { get; init; }

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;
}
