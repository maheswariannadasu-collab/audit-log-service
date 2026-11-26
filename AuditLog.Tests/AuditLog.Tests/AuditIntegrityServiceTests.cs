using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using AuditLog.BLL.Services;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace AuditLog.Tests;

public sealed class AuditIntegrityServiceTests
{
    [Fact]
    public async Task VerifyAsync_AcceptsIntactChain()
    {
        var records = CreateTwoRecordChain();
        var service = new AuditIntegrityService(
            new InMemoryAuditIntegrityRepository(records),
            new AuditHashingService());

        var result = await service.VerifyAsync();

        Assert.True(result.IsValid);
        Assert.Equal(2, result.TotalRecords);
        Assert.Equal(2, result.VerifiedRecords);
        Assert.Null(result.FailedRecordId);
    }

    [Fact]
    public async Task VerifyAsync_ReportsFirstRecordWithChangedPayload()
    {
        var records = CreateTwoRecordChain();
        var first = records[0];
        records[0] = new AuditEventRecord
        {
            Id = first.Id,
            EventType = first.EventType,
            ActorId = first.ActorId,
            ResourceType = first.ResourceType,
            ResourceId = first.ResourceId,
            PayloadJson = """{"value":"tampered"}""",
            EventTimestampUtc = first.EventTimestampUtc,
            ContentHash = first.ContentHash,
            PreviousHash = first.PreviousHash,
            RecordHash = first.RecordHash
        };

        var service = new AuditIntegrityService(
            new InMemoryAuditIntegrityRepository(records),
            new AuditHashingService());

        var result = await service.VerifyAsync();

        Assert.False(result.IsValid);
        Assert.Equal(1, result.FailedRecordId);
        Assert.Equal("Content hash mismatch.", result.Message);
        Assert.Equal(2, result.TotalRecords);
        Assert.Equal(0, result.VerifiedRecords);
    }

    [Fact]
    public async Task VerifyAsync_AcceptsRedactedPayloadWithOriginalChainHash()
    {
        var records = CreateTwoRecordChain();
        var first = records[0];
        var redactedPayload = """{"value":"[REDACTED]"}""";
        var redactedContentHash = ComputeContentHash(first, redactedPayload);
        records[0] = WithPayload(first, redactedPayload);

        var service = new AuditIntegrityService(
            new InMemoryAuditIntegrityRepository(
                records,
                new Dictionary<long, byte[]>
                {
                    [first.Id] = redactedContentHash
                }),
            new AuditHashingService());

        var result = await service.VerifyAsync();

        Assert.True(result.IsValid);
        Assert.Equal(2, result.TotalRecords);
        Assert.Equal(2, result.VerifiedRecords);
    }

    [Fact]
    public async Task VerifyAsync_DetectsChangedRedactedPayload()
    {
        var records = CreateTwoRecordChain();
        var first = records[0];
        records[0] = WithPayload(first, """{"value":"altered"}""");

        var service = new AuditIntegrityService(
            new InMemoryAuditIntegrityRepository(
                records,
                new Dictionary<long, byte[]>
                {
                    [first.Id] = ComputeContentHash(
                        first,
                        """{"value":"[REDACTED]"}""")
                }),
            new AuditHashingService());

        var result = await service.VerifyAsync();

        Assert.False(result.IsValid);
        Assert.Equal(first.Id, result.FailedRecordId);
        Assert.Equal("Redacted content hash mismatch.", result.Message);
    }

    private static List<AuditEventRecord> CreateTwoRecordChain()
    {
        var hashingService = new AuditHashingService();
        var first = CreateRecord(
            id: 1,
            payloadJson: """{"value":"one"}""",
            previousHash: hashingService.CreateGenesisHash(),
            hashingService);
        var second = CreateRecord(
            id: 2,
            payloadJson: """{"value":"two"}""",
            previousHash: first.RecordHash,
            hashingService);

        return [first, second];
    }

    private static AuditEventRecord CreateRecord(
        long id,
        string payloadJson,
        byte[] previousHash,
        AuditHashingService hashingService)
    {
        using var payload = JsonDocument.Parse(payloadJson);
        var timestamp = new DateTimeOffset(
            2026, 1, (int)id, 12, 0, 0, TimeSpan.Zero);
        var auditEvent = new AuditEventData
        {
            EventType = "RECORD_UPDATED",
            ActorId = "actor-1",
            ResourceType = "account",
            ResourceId = "account-1",
            Payload = payload.RootElement,
            EventTimestampUtc = timestamp
        };
        var contentHash =
            hashingService.ComputeContentHash(auditEvent);
        var recordHash =
            hashingService.ComputeRecordHash(previousHash, contentHash);

        return new AuditEventRecord
        {
            Id = id,
            EventType = auditEvent.EventType,
            ActorId = auditEvent.ActorId,
            ResourceType = auditEvent.ResourceType,
            ResourceId = auditEvent.ResourceId,
            PayloadJson = payloadJson,
            EventTimestampUtc = timestamp.UtcDateTime,
            ContentHash = contentHash,
            PreviousHash = previousHash,
            RecordHash = recordHash
        };
    }

    private static byte[] ComputeContentHash(
        AuditEventRecord record,
        string payloadJson)
    {
        using var payload = JsonDocument.Parse(payloadJson);
        return new AuditHashingService().ComputeContentHash(
            new AuditEventData
            {
                EventType = record.EventType,
                ActorId = record.ActorId,
                ResourceType = record.ResourceType,
                ResourceId = record.ResourceId,
                Payload = payload.RootElement,
                EventTimestampUtc = new DateTimeOffset(
                    DateTime.SpecifyKind(
                        record.EventTimestampUtc,
                        DateTimeKind.Utc))
            });
    }

    private static AuditEventRecord WithPayload(
        AuditEventRecord record,
        string payloadJson) =>
        new()
        {
            Id = record.Id,
            EventType = record.EventType,
            ActorId = record.ActorId,
            ResourceType = record.ResourceType,
            ResourceId = record.ResourceId,
            PayloadJson = payloadJson,
            EventTimestampUtc = record.EventTimestampUtc,
            ContentHash = record.ContentHash,
            PreviousHash = record.PreviousHash,
            RecordHash = record.RecordHash
        };

    private sealed class InMemoryAuditIntegrityRepository(
        List<AuditEventRecord> records,
        IReadOnlyDictionary<long, byte[]>? redactedHashes = null)
        : IAuditIntegrityRepository
    {
        public IAsyncEnumerable<AuditIntegrityRecord>
            StreamForIntegrityCheckAsync(
                CancellationToken cancellationToken = default) =>
            EnumerateRecords(records, cancellationToken);

        private async IAsyncEnumerable<AuditIntegrityRecord>
            EnumerateRecords(
                IReadOnlyCollection<AuditEventRecord> source,
                [EnumeratorCancellation]
                CancellationToken cancellationToken)
        {
            var totalRecords = source.Count;

            foreach (var record in source)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return new AuditIntegrityRecord
                {
                    Event = record,
                    TotalRecords = totalRecords,
                    RedactedContentHash =
                        redactedHashes?.GetValueOrDefault(record.Id)
                };
                await Task.Yield();
            }
        }
    }
}
