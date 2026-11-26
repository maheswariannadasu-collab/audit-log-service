namespace AuditLog.BLL.Models;

public sealed class AuditExportBundle
{
    public string HashAlgorithm { get; init; } = "SHA-256";

    public DateTime GeneratedAtUtc { get; init; }

    public string? ActorId { get; init; }

    public string? ResourceId { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }

    public string GenesisHash { get; init; } = string.Empty;

    public string ChainTipRecordHash { get; init; } = string.Empty;

    public IReadOnlyList<AuditExport> Events { get; init; } =
        Array.Empty<AuditExport>();

    public IReadOnlyList<AuditExportChainRecord> ChainProof { get; init; } =
        Array.Empty<AuditExportChainRecord>();
}
