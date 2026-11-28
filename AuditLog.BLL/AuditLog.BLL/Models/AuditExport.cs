
﻿using System.Text.Json;

namespace AuditLog.BLL.Models;

public sealed class AuditExport
{
    public long Id { get; init; }

    public string EventType { get; init; } = string.Empty;

    public string ActorId { get; init; } = string.Empty;

    public string CreatedBySubject { get; init; } = string.Empty;

    public string CreatedByDisplayName { get; init; } = string.Empty;

    public string ResourceType { get; init; } = string.Empty;

    public string ResourceId { get; init; } = string.Empty;

    public JsonElement Payload { get; init; }

    public DateTime EventTimestampUtc { get; init; }

    public bool IsArchived { get; init; }

    public bool IsRedacted { get; init; }

    public string ContentHash { get; init; } = string.Empty;

    public string? RedactedContentHash { get; set; }

    public string PreviousHash { get; init; } = string.Empty;

    public string RecordHash { get; init; } = string.Empty;
}
