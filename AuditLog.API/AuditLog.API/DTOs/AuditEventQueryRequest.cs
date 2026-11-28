using AuditLog.BLL.Models;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace AuditLog.API.DTOs;

public sealed class AuditEventQueryRequest
{
    [StringLength(150)]
    [FromQuery(Name = "accountId")]
    public string? AccountId { get; init; }

    [StringLength(100)]
    [FromQuery(Name = "action")]
    public string? Action { get; init; }

    [StringLength(500)]
    [FromQuery(Name = "search")]
    public string? Search { get; init; }

    [FromQuery(Name = "sortBy")]
    public string? SortBy { get; init; }

    [FromQuery(Name = "sortDir")]
    public string? SortDir { get; init; }

    [FromQuery(Name = "from")]
    public DateTimeOffset? From { get; init; }

    [FromQuery(Name = "to")]
    public DateTimeOffset? To { get; init; }

    [StringLength(150)]
    [FromQuery(Name = "actorId")]
    public string? ActorId { get; init; }

    [StringLength(100)]
    [FromQuery(Name = "resourceType")]
    public string? ResourceType { get; init; }

    [StringLength(150)]
    [FromQuery(Name = "resourceId")]
    public string? ResourceId { get; init; }

    [StringLength(100)]
    [FromQuery(Name = "eventType")]
    public string? EventType { get; init; }

    [FromQuery(Name = "fromUtc")]
    public DateTimeOffset? FromUtc { get; init; }

    [FromQuery(Name = "toUtc")]
    public DateTimeOffset? ToUtc { get; init; }

    [Range(1, int.MaxValue)]
    [FromQuery(Name = "page")]
    public int Page { get; init; } = 1;

    [Range(1, AuditEventQuery.MaximumPageSize)]
    [FromQuery(Name = "pageSize")]
    public int PageSize { get; init; } = AuditEventQuery.DefaultPageSize;
}
