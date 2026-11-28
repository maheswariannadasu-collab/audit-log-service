namespace AuditLog.API.DTOs;

public sealed class AuditEventPageResponse
{
    public IReadOnlyList<AuditEventResponse> Items { get; init; } =
        Array.Empty<AuditEventResponse>();

    public long TotalCount { get; init; }

    public int PageNumber { get; init; }

    public int PageSize { get; init; }

    public int TotalPages { get; init; }
}
