using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using AuditLog.BLL.Services;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace AuditLog.Tests;

public sealed class AuditScenarioBServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void RedactJson_RejectsEmptyInput(string? json)
    {
        var service = CreateRedactionService(
            new RecordingRedactionRepository(null));

        Assert.Throws<ArgumentException>(() => service.RedactJson(json!));
    }

    [Fact]
    public void RedactJson_RejectsMalformedJson()
    {
        var service = CreateRedactionService(
            new RecordingRedactionRepository(null));

        Assert.ThrowsAny<JsonException>(() => service.RedactJson("{"));
    }

    [Fact]
    public void RedactJson_RejectsJsonNullNode()
    {
        var service = CreateRedactionService(
            new RecordingRedactionRepository(null));

        Assert.Throws<JsonException>(() => service.RedactJson("null"));
    }

    [Fact]
    public void RedactJson_RedactsMatchingFieldsRecursivelyAndCaseInsensitively()
    {
        var service = CreateRedactionService(
            new RecordingRedactionRepository(null));

        var result = service.RedactJson(
            """{"password":"one","items":[{"Password":"two","keep":"value"}]}""");
        using var document = JsonDocument.Parse(result);

        Assert.Equal(
            "[REDACTED]",
            document.RootElement.GetProperty("password").GetString());
        var item = document.RootElement.GetProperty("items")[0];
        Assert.Equal(
            "[REDACTED]",
            item.GetProperty("Password").GetString());
        Assert.Equal("value", item.GetProperty("keep").GetString());
    }

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
    public async Task ApplyRedactionAsync_RejectsInvalidEventId()
    {
        var service = CreateRedactionService(
            new RecordingRedactionRepository(null));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ApplyRedactionAsync(
                CreateRedactionRequest(auditEventId: 0),
                CancellationToken.None));
    }

    [Theory]
    [InlineData("", "operator")]
    [InlineData("reason", " ")]
    public async Task ApplyRedactionAsync_RejectsMissingReasonOrActor(
        string reason,
        string redactedBy)
    {
        var service = CreateRedactionService(
            new RecordingRedactionRepository(null));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ApplyRedactionAsync(
                CreateRedactionRequest(
                    reason: reason,
                    redactedBy: redactedBy),
                CancellationToken.None));
    }

    [Fact]
    public async Task ApplyRedactionAsync_RejectsEmptyFieldList()
    {
        var service = CreateRedactionService(
            new RecordingRedactionRepository(null));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ApplyRedactionAsync(
                CreateRedactionRequest(fieldsToRedact: []),
                CancellationToken.None));
    }

    [Fact]
    public async Task ApplyRedactionAsync_RejectsFieldsNotApprovedByConfiguration()
    {
        var service = CreateRedactionService(
            new RecordingRedactionRepository(null));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ApplyRedactionAsync(
                CreateRedactionRequest(fieldsToRedact: ["accountNumber"]),
                CancellationToken.None));
    }

    [Fact]
    public async Task ApplyRedactionAsync_ReportsMissingEvent()
    {
        var service = CreateRedactionService(
            new RecordingRedactionRepository(null));

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.ApplyRedactionAsync(
                CreateRedactionRequest(),
                CancellationToken.None));
    }

    [Fact]
    public async Task ApplyRedactionAsync_RejectsMalformedStoredPayload()
    {
        var record = CreateRedactionRecord(payloadJson: "{");
        var service = CreateRedactionService(
            new RecordingRedactionRepository(record));

        await Assert.ThrowsAnyAsync<JsonException>(() =>
            service.ApplyRedactionAsync(
                CreateRedactionRequest(),
                CancellationToken.None));
    }

    [Fact]
    public async Task ApplyRedactionAsync_PropagatesRepositoryFailure()
    {
        var record = CreateRedactionRecord();
        var repository = new RecordingRedactionRepository(record)
        {
            SaveException = new InvalidOperationException("save failed")
        };
        var service = CreateRedactionService(repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ApplyRedactionAsync(
                CreateRedactionRequest(),
                CancellationToken.None));

        Assert.Equal("save failed", exception.Message);
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
    public async Task ExportJsonAsync_ReturnsFilteredJsonAndAddsMissingRedactedHash()
    {
        var record = CreateExportRecord(
            isRedacted: true,
            redactedContentHash: null);
        var repository = new RecordingExportRepository([record]);
        var service = new AuditExportService(
            repository,
            new AuditHashingService());
        var fromUtc = new DateTime(2026, 1, 1);
        var toUtc = new DateTime(2026, 2, 1);

        var bytes = await service.ExportJsonAsync(
            fromUtc,
            toUtc,
            "USER001",
            "ACCOUNT100",
            CancellationToken.None);
        var exported = JsonSerializer.Deserialize<List<AuditExport>>(bytes);

        Assert.NotNull(exported);
        Assert.Single(exported);
        Assert.NotNull(exported[0].RedactedContentHash);
        Assert.Equal(fromUtc, repository.ReceivedFromUtc);
        Assert.Equal(toUtc, repository.ReceivedToUtc);
        Assert.Equal("USER001", repository.ReceivedActorId);
        Assert.Equal("ACCOUNT100", repository.ReceivedResourceId);
    }

    [Fact]
    public async Task ExportCsvAsync_EscapesFieldsAndIncludesHeaders()
    {
        var record = CreateExportRecord(
            eventType: "TYPE,WITH\"QUOTE",
            isRedacted: false);
        var service = new AuditExportService(
            new RecordingExportRepository([record]),
            new AuditHashingService());

        var bytes = await service.ExportCsvAsync(
            null,
            null,
            null,
            null,
            CancellationToken.None);
        var csv = Encoding.UTF8.GetString(bytes);

        Assert.Contains(
            "Id,EventType,ActorId,ResourceType,ResourceId,Payload,",
            csv);
        Assert.Contains("\"TYPE,WITH\"\"QUOTE\"", csv);
        Assert.EndsWith(Environment.NewLine, csv);
    }

    [Fact]
    public async Task ExportBundleAsync_UsesGenesisTipWhenNoEventsMatch()
    {
        var repository = new RecordingExportRepository();
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
        Assert.Empty(bundle.Events);
        Assert.Empty(bundle.ChainProof);
        Assert.Equal(bundle.GenesisHash, bundle.ChainTipRecordHash);
        Assert.Null(repository.ReceivedThroughId);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("csv")]
    [InlineData("bundle")]
    public async Task ExportMethods_RejectInvalidDateRange(string format)
    {
        var service = new AuditExportService(
            new RecordingExportRepository(),
            new AuditHashingService());
        var fromUtc = new DateTime(2026, 2, 1);
        var toUtc = new DateTime(2026, 1, 1);

        await Assert.ThrowsAsync<ArgumentException>(() => format switch
        {
            "json" => service.ExportJsonAsync(
                fromUtc, toUtc, null, null, CancellationToken.None),
            "csv" => service.ExportCsvAsync(
                fromUtc, toUtc, null, null, CancellationToken.None),
            "bundle" => service.ExportBundleAsync(
                "USER001", null, fromUtc, toUtc, CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        });
    }

    [Theory]
    [InlineData("actor", " ")]
    [InlineData("actor", "too-long")]
    [InlineData("resource", " ")]
    [InlineData("resource", "too-long")]
    public async Task ExportMethods_RejectInvalidFilters(
        string filter,
        string value)
    {
        if (value == "too-long")
        {
            value = new string('x', 151);
        }

        var service = new AuditExportService(
            new RecordingExportRepository(),
            new AuditHashingService());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExportJsonAsync(
                null,
                null,
                filter == "actor" ? value : null,
                filter == "resource" ? value : null,
                CancellationToken.None));
    }

    [Fact]
    public async Task ExportBundleAsync_PropagatesRepositoryFailure()
    {
        var repository = new RecordingExportRepository
        {
            ExceptionToThrow = new InvalidOperationException("export failed")
        };
        var service = new AuditExportService(
            repository,
            new AuditHashingService());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExportBundleAsync(
                "USER001",
                null,
                null,
                null,
                CancellationToken.None));

        Assert.Equal("export failed", exception.Message);
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

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AuditRetentionService_RejectsNonPositiveRetentionDays(
        int retentionDays)
    {
        Assert.Throws<InvalidOperationException>(() =>
            new AuditRetentionService(
                new RecordingRetentionRepository(),
                Options.Create(new AuditRetentionOptions
                {
                    RetentionDays = retentionDays
                })));
    }

    [Fact]
    public async Task ArchiveExpiredEventsAsync_PropagatesRepositoryFailure()
    {
        var repository = new RecordingRetentionRepository
        {
            ExceptionToThrow = new InvalidOperationException("archive failed")
        };
        var service = new AuditRetentionService(
            repository,
            Options.Create(new AuditRetentionOptions { RetentionDays = 1 }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ArchiveExpiredEventsAsync(CancellationToken.None));

        Assert.Equal("archive failed", exception.Message);
    }

    private static AuditRedactionService CreateRedactionService(
        IAuditRedactionRepository repository) =>
        new(
            Options.Create(new AuditRedactionOptions
            {
                SensitiveFields = ["password", "email"],
                ReplacementValue = "[REDACTED]"
            }),
            repository,
            new AuditHashingService());

    private static AuditRedactionRequest CreateRedactionRequest(
        long auditEventId = 42,
        string reason = "Remove sensitive data",
        string redactedBy = "compliance-operator",
        List<string>? fieldsToRedact = null) =>
        new()
        {
            AuditEventId = auditEventId,
            FieldsToRedact = fieldsToRedact ?? ["password"],
            Reason = reason,
            RedactedBy = redactedBy
        };

    private static AuditEventRecord CreateRedactionRecord(
        string payloadJson = """{"auth":{"password":"secret-value"}}""") =>
        new()
        {
            Id = 42,
            EventType = "USER_LOGIN",
            ActorId = "USER001",
            ResourceType = "ACCOUNT",
            ResourceId = "ACCOUNT100",
            PayloadJson = payloadJson,
            EventTimestampUtc = DateTime.SpecifyKind(
                new DateTime(2026, 1, 1, 12, 0, 0),
                DateTimeKind.Utc)
        };

    private static AuditExport CreateExportRecord(
        string eventType = "ACCOUNT_VIEWED",
        bool isRedacted = false,
        string? redactedContentHash = null)
    {
        using var payload = JsonDocument.Parse("""{"note":"a,b"}""");

        return new AuditExport
        {
            Id = 3,
            EventType = eventType,
            ActorId = "USER001",
            ResourceType = "ACCOUNT",
            ResourceId = "ACCOUNT100",
            Payload = payload.RootElement.Clone(),
            EventTimestampUtc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            IsRedacted = isRedacted,
            ContentHash = "11",
            RedactedContentHash = redactedContentHash,
            PreviousHash = "00",
            RecordHash = "22"
        };
    }

    private sealed class RecordingRedactionRepository(
        AuditEventRecord? record) : IAuditRedactionRepository
    {
        public string? SavedPayload { get; private set; }

        public byte[]? SavedContentHash { get; private set; }

        public Exception? SaveException { get; init; }

        public Task<AuditEventRecord?> GetEventForRedactionAsync(
            long auditEventId,
            CancellationToken cancellationToken) =>
            Task.FromResult<AuditEventRecord?>(
                record is not null && auditEventId == record.Id
                    ? record
                    : null);

        public Task SaveRedactionAsync(
            AuditRedactionRequest request,
            string redactedPayloadJson,
            byte[] redactedContentHash,
            CancellationToken cancellationToken)
        {
            if (SaveException is not null)
            {
                throw SaveException;
            }

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
        public DateTime? ReceivedFromUtc { get; private set; }

        public DateTime? ReceivedToUtc { get; private set; }

        public string? ReceivedActorId { get; private set; }

        public string? ReceivedResourceId { get; private set; }

        public long? ReceivedThroughId { get; private set; }

        public Exception? ExceptionToThrow { get; init; }

        public Task<List<AuditExport>> GetAuditEventsAsync(
            DateTime? fromUtc,
            DateTime? toUtc,
            string? actorId,
            string? resourceId,
            CancellationToken cancellationToken)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            ReceivedFromUtc = fromUtc;
            ReceivedToUtc = toUtc;
            ReceivedActorId = actorId;
            ReceivedResourceId = resourceId;
            return Task.FromResult(events ?? []);
        }

        public Task<List<AuditExportChainRecord>> GetChainProofAsync(
            long throughId,
            CancellationToken cancellationToken)
        {
            ReceivedThroughId = throughId;
            return Task.FromResult(chainProof ?? []);
        }
    }

    private sealed class RecordingRetentionRepository
        : IAuditRetentionRepository
    {
        public DateTime? CutoffUtc { get; private set; }

        public Exception? ExceptionToThrow { get; init; }

        public Task<int> ArchiveOlderThanAsync(
            DateTime cutoffUtc,
            CancellationToken cancellationToken)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            CutoffUtc = cutoffUtc;
            return Task.FromResult(2);
        }
    }
}
