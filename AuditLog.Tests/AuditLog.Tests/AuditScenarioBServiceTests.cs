using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using AuditLog.BLL.Services;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace AuditLog.Tests;

public sealed class AuditScenarioBServiceTests
{
    [Fact]
    public async Task ApplyRedactionAsync_RemovesSelectedValueAndReturnsReplacement()
    {
        using var payload = JsonDocument.Parse(
            """{"auth":{"password":"secret-value","method":"password"}}""");
        var timestamp = DateTime.SpecifyKind(
            new DateTime(2026, 1, 1, 12, 0, 0),
            DateTimeKind.Utc);
        var record = new AuditEventRecord
        {
            Id = 42,
            EventType = "USER_LOGIN",
            ActorId = "USER001",
            ResourceType = "ACCOUNT",
            ResourceId = "ACCOUNT100",
            PayloadJson = payload.RootElement.GetRawText(),
            EventTimestampUtc = timestamp
        };
        var repository = new RecordingRedactionRepository(record);
        var service = new AuditRedactionService(
            Options.Create(new AuditRedactionOptions
            {
                SensitiveFields = ["password"],
                ReplacementValue = "[REDACTED]"
            }),
            repository,
            new AuditHashingService());

        var result = await service.ApplyRedactionAsync(
            new AuditRedactionRequest
            {
                AuditEventId = record.Id,
                FieldsToRedact = ["password"],
                Reason = "Remove credential data",
                RedactedBy = "compliance-operator"
            },
            CancellationToken.None);

        Assert.Equal(record.Id, result.AuditEventId);
        Assert.NotNull(repository.SavedPayload);
        Assert.DoesNotContain("secret-value", repository.SavedPayload);
        Assert.Contains("[REDACTED]", repository.SavedPayload);

        using var redactedPayload = JsonDocument.Parse(repository.SavedPayload);
        var expectedHash = new AuditHashingService().ComputeContentHash(
            new AuditEventData
            {
                EventType = record.EventType,
                ActorId = record.ActorId,
                ResourceType = record.ResourceType,
                ResourceId = record.ResourceId,
                EventTimestampUtc = new DateTimeOffset(timestamp),
                Payload = redactedPayload.RootElement
            });

        Assert.Equal(expectedHash, repository.SavedContentHash);
    }

    [Fact]
    public async Task ExportBundleAsync_RequiresActorOrResourceFilter()
    {
        var service = new AuditExportService(
            new RecordingExportRepository(),
            new AuditHashingService());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExportBundleAsync(
                null,
                null,
                null,
                null,
                CancellationToken.None));
    }

    [Fact]
    public async Task ExportBundleAsync_IncludesFilteredEventsAndChainProof()
    {
        using var payload = JsonDocument.Parse("""{"action":"view"}""");
        var timestamp = DateTime.SpecifyKind(
            new DateTime(2026, 1, 1, 12, 0, 0),
            DateTimeKind.Utc);
        var record = new AuditExport
        {
            Id = 3,
            EventType = "USER_LOGIN",
            ActorId = "USER001",
            ResourceType = "ACCOUNT",
            ResourceId = "ACCOUNT100",
            Payload = payload.RootElement.Clone(),
            EventTimestampUtc = timestamp,
            IsRedacted = true,
            ContentHash = "11",
            PreviousHash = "00",
            RecordHash = "22"
        };
        var repository = new RecordingExportRepository(
            [record],
            [
                new AuditExportChainRecord
                {
                    Id = 1,
                    ContentHash = "01",
                    PreviousHash = "00",
                    RecordHash = "02"
                },
                new AuditExportChainRecord
                {
                    Id = 3,
                    ContentHash = record.ContentHash,
                    PreviousHash = record.PreviousHash,
                    RecordHash = record.RecordHash
                }
            ]);
        var service = new AuditExportService(
            repository,
            new AuditHashingService());

        var bytes = await service.ExportBundleAsync(
            "USER001",
            null,
            null,
            null,
            CancellationToken.None);
        var bundle = JsonSerializer.Deserialize<AuditExportBundle>(bytes);

        Assert.NotNull(bundle);
        Assert.Equal("USER001", repository.ReceivedActorId);
        Assert.Equal("USER001", bundle.ActorId);
        Assert.Single(bundle.Events);
        Assert.Equal(2, bundle.ChainProof.Count);
        Assert.Equal("22", bundle.ChainTipRecordHash);
        Assert.Equal(
            Convert.ToHexString(new AuditHashingService().ComputeContentHash(
                new AuditEventData
                {
                    EventType = record.EventType,
                    ActorId = record.ActorId,
                    ResourceType = record.ResourceType,
                    ResourceId = record.ResourceId,
                    EventTimestampUtc = new DateTimeOffset(timestamp),
                    Payload = record.Payload
                })),
            bundle.Events.Single().RedactedContentHash);
        Assert.Equal(
            Convert.ToHexString(new AuditHashingService().CreateGenesisHash()),
            bundle.GenesisHash);
    }

    [Fact]
    public async Task ArchiveExpiredEventsAsync_UsesConfiguredRetentionWindow()
    {
        var repository = new RecordingRetentionRepository();
        var service = new AuditRetentionService(
            repository,
            Options.Create(new AuditRetentionOptions { RetentionDays = 30 }));
        var beforeCallUtc = DateTime.UtcNow;

        var archivedCount = await service.ArchiveExpiredEventsAsync(
            CancellationToken.None);
        var expectedCutoff = DateTime.UtcNow.AddDays(-30);

        Assert.Equal(2, archivedCount);
        Assert.NotNull(repository.CutoffUtc);
        Assert.InRange(
            repository.CutoffUtc.Value,
            beforeCallUtc.AddDays(-30).AddSeconds(-1),
            expectedCutoff.AddSeconds(1));
    }

    private sealed class RecordingRedactionRepository(
        AuditEventRecord record) : IAuditRedactionRepository
    {
        public string? SavedPayload { get; private set; }

        public byte[]? SavedContentHash { get; private set; }

        public Task<AuditEventRecord?> GetEventForRedactionAsync(
            long auditEventId,
            CancellationToken cancellationToken) =>
            Task.FromResult<AuditEventRecord?>(
                auditEventId == record.Id ? record : null);

        public Task SaveRedactionAsync(
            AuditRedactionRequest request,
            string redactedPayloadJson,
            byte[] redactedContentHash,
            CancellationToken cancellationToken)
        {
            SavedPayload = redactedPayloadJson;
            SavedContentHash = redactedContentHash;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingExportRepository(
        List<AuditExport>? events = null,
        List<AuditExportChainRecord>? chainProof = null)
        : IAuditExportRepository
    {
        public string? ReceivedActorId { get; private set; }

        public Task<List<AuditExport>> GetAuditEventsAsync(
            DateTime? fromUtc,
            DateTime? toUtc,
            string? actorId,
            string? resourceId,
            CancellationToken cancellationToken)
        {
            ReceivedActorId = actorId;
            return Task.FromResult(events ?? []);
        }

        public Task<List<AuditExportChainRecord>> GetChainProofAsync(
            long throughId,
            CancellationToken cancellationToken) =>
            Task.FromResult(chainProof ?? []);
    }

    private sealed class RecordingRetentionRepository
        : IAuditRetentionRepository
    {
        public DateTime? CutoffUtc { get; private set; }

        public Task<int> ArchiveOlderThanAsync(
            DateTime cutoffUtc,
            CancellationToken cancellationToken)
        {
            CutoffUtc = cutoffUtc;
            return Task.FromResult(2);
        }
    }
}
