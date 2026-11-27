using AuditLog.API.Controllers;
using AuditLog.API.DTOs;
using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using AuditLog.BLL.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AuditLog.Tests;

public sealed class AuditEventsControllerTests
{
    [Fact]
    public async Task Create_ReturnsCreatedAndForwardsEvent()
    {
        var repository = new ControllerAuditEventRepository();
        var controller = CreateController(repository);
        using var payload = JsonDocument.Parse("""{"action":"view"}""");
        var timestamp = DateTimeOffset.UtcNow;

        var result = await controller.Create(
            new CreateAuditEventRequest
            {
                EventType = "ACCOUNT_VIEWED",
                ActorId = "USER001",
                ResourceType = "ACCOUNT",
                ResourceId = "ACCOUNT100",
                EventTimestampUtc = timestamp,
                Payload = payload.RootElement.Clone()
            },
            CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        var response = Assert.IsType<CreateAuditEventResponse>(created.Value);
        Assert.Equal(42, response.Id);
        Assert.Equal("ACCOUNT_VIEWED", repository.InsertedEvent?.EventType);
        Assert.Equal("ACCOUNT100", repository.InsertedEvent?.ResourceId);
        Assert.Equal(timestamp, repository.InsertedEvent?.EventTimestampUtc);
    }

    [Fact]
    public async Task Create_RejectsMissingPayload()
    {
        var controller = CreateController(new ControllerAuditEventRepository());

        var result = await controller.Create(
            CreateAuditEventRequest(
                payload: default,
                timestamp: DateTimeOffset.UtcNow),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_RejectsNonObjectPayload()
    {
        using var payload = JsonDocument.Parse("""["value"]""");
        var controller = CreateController(new ControllerAuditEventRepository());

        var result = await controller.Create(
            CreateAuditEventRequest(
                payload: payload.RootElement.Clone(),
                timestamp: DateTimeOffset.UtcNow),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Create_RejectsMissingTimestamp()
    {
        using var payload = JsonDocument.Parse("{}");
        var controller = CreateController(new ControllerAuditEventRepository());

        var result = await controller.Create(
            CreateAuditEventRequest(
                payload: payload.RootElement.Clone(),
                timestamp: null),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetById_RejectsNonPositiveId()
    {
        var repository = new ControllerAuditEventRepository();
        var result = await CreateController(repository).GetById(
            0,
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, repository.GetRedactedCallCount);
    }

    [Fact]
    public async Task GetById_ReturnsNotFoundWhenRecordDoesNotExist()
    {
        var controller = CreateController(
            new ControllerAuditEventRepository(
                record: null,
                useDefaultRecord: false));

        var result = await controller.GetById(42, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetById_ReturnsRedactedEventAndHashes()
    {
        var controller = CreateController(
            new ControllerAuditEventRepository());

        var result = await controller.GetById(42, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuditEventResponse>(ok.Value);
        Assert.Equal(42, response.Id);
        Assert.Equal("ACCOUNT_VIEWED", response.EventType);
        Assert.Equal("value", response.Payload.GetProperty("field").GetString());
        Assert.Equal(32, Convert.FromHexString(response.ContentHash).Length);
    }

    [Fact]
    public async Task Query_ReturnsPageWithMappedEvents()
    {
        var repository = new ControllerAuditEventRepository();
        var controller = CreateController(repository);

        var result = await controller.Query(
            new AuditEventQueryRequest
            {
                ActorId = "USER001",
                ResourceId = "ACCOUNT100",
                PageNumber = 2,
                PageSize = 10
            },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var page = Assert.IsType<AuditEventPageResponse>(ok.Value);
        Assert.Equal(2, page.PageNumber);
        Assert.Equal(10, page.PageSize);
        Assert.Single(page.Items);
        Assert.Equal("USER001", repository.ReceivedQuery?.ActorId);
        Assert.Equal("ACCOUNT100", repository.ReceivedQuery?.ResourceId);
    }

    [Fact]
    public async Task Query_MapsInvalidQueryToBadRequest()
    {
        var controller = CreateController(
            new ControllerAuditEventRepository());

        var result = await controller.Query(
            new AuditEventQueryRequest
            {
                FromUtc = new DateTimeOffset(
                    2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
                ToUtc = new DateTimeOffset(
                    2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            },
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Query_PropagatesUnexpectedRepositoryFailure()
    {
        var repository = new ControllerAuditEventRepository
        {
            QueryException = new InvalidOperationException("query failed")
        };
        var controller = CreateController(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.Query(
                new AuditEventQueryRequest(),
                CancellationToken.None));
    }

    private static AuditEventsController CreateController(
        ControllerAuditEventRepository repository) =>
        new(
            new AuditEventWriterService(repository),
            new AuditEventQueryService(repository));

    private static CreateAuditEventRequest CreateAuditEventRequest(
        JsonElement payload,
        DateTimeOffset? timestamp) =>
        new()
        {
            EventType = "ACCOUNT_VIEWED",
            ActorId = "USER001",
            ResourceType = "ACCOUNT",
            ResourceId = "ACCOUNT100",
            EventTimestampUtc = timestamp,
            Payload = payload
        };

    private sealed class ControllerAuditEventRepository(
        AuditEventRecord? record = null,
        bool useDefaultRecord = true) : IAuditEventRepository
    {
        private readonly AuditEventRecord? _record = useDefaultRecord
            ? record ?? CreateRecord()
            : record;

        public AuditEventData? InsertedEvent { get; private set; }

        public AuditEventQuery? ReceivedQuery { get; private set; }

        public int GetRedactedCallCount { get; private set; }

        public Exception? QueryException { get; init; }

        public Task<long> InsertAsync(
            AuditEventData auditEvent,
            CancellationToken cancellationToken = default)
        {
            InsertedEvent = auditEvent;
            return Task.FromResult(42L);
        }

        public Task<AuditEventRecord?> GetByIdAsync(
            long id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AuditEventRecord?>(_record?.Id == id ? _record : null);

        public Task<AuditEventRecord?> GetRedactedByIdAsync(
            long id,
            CancellationToken cancellationToken = default)
        {
            GetRedactedCallCount++;
            return Task.FromResult<AuditEventRecord?>(
                _record?.Id == id ? _record : null);
        }

        public Task<AuditEventQueryResult> QueryAsync(
            AuditEventQuery query,
            CancellationToken cancellationToken = default)
        {
            ReceivedQuery = query;

            if (QueryException is not null)
            {
                throw QueryException;
            }

            return Task.FromResult(new AuditEventQueryResult
            {
                Items = _record is null ? [] : [_record],
                TotalCount = _record is null ? 0 : 1,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize
            });
        }

        private static AuditEventRecord CreateRecord()
        {
            using var payload = JsonDocument.Parse("""{"field":"value"}""");

            return new AuditEventRecord
            {
                Id = 42,
                EventType = "ACCOUNT_VIEWED",
                ActorId = "USER001",
                ResourceType = "ACCOUNT",
                ResourceId = "ACCOUNT100",
                PayloadJson = payload.RootElement.GetRawText(),
                EventTimestampUtc = DateTime.SpecifyKind(
                    new DateTime(2026, 1, 1, 12, 0, 0),
                    DateTimeKind.Utc),
                ContentHash = new byte[32],
                PreviousHash = new byte[32],
                RecordHash = new byte[32]
            };
        }
    }
}

public sealed class AuditIntegrityControllerTests
{
    [Fact]
    public async Task Verify_ReturnsOkForValidChain()
    {
        var result = new AuditIntegrityResult
        {
            IsValid = true,
            TotalRecords = 2,
            VerifiedRecords = 2,
            Message = "verified"
        };
        var controller = new AuditIntegrityController(
            new StubIntegrityService(result));

        var response = Assert.IsType<OkObjectResult>(
            await controller.Verify(CancellationToken.None));

        Assert.Same(result, response.Value);
    }

    [Fact]
    public async Task Verify_ReturnsConflictForInvalidChain()
    {
        var result = new AuditIntegrityResult
        {
            IsValid = false,
            FailedRecordId = 7,
            Message = "Record hash mismatch."
        };
        var controller = new AuditIntegrityController(
            new StubIntegrityService(result));

        var response = Assert.IsType<ConflictObjectResult>(
            await controller.Verify(CancellationToken.None));

        Assert.Same(result, response.Value);
    }

    private sealed class StubIntegrityService(
        AuditIntegrityResult result) : IAuditIntegrityService
    {
        public Task<AuditIntegrityResult> VerifyAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}

public sealed class AuditRetentionControllerTests
{
    [Fact]
    public async Task ArchiveExpiredEvents_ReturnsArchivedCount()
    {
        var controller = new AuditRetentionController(
            new StubRetentionService(4));

        var result = Assert.IsType<OkObjectResult>(
            await controller.ArchiveExpiredEvents(CancellationToken.None));

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.Equal(4, result.Value?.GetType().GetProperty("ArchivedRecords")
            ?.GetValue(result.Value));
    }

    private sealed class StubRetentionService(int count)
        : IAuditRetentionService
    {
        public Task<int> ArchiveExpiredEventsAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(count);
    }
}

public sealed class AuditRedactionControllerTests
{
    [Fact]
    public void PreviewRedaction_ReturnsRedactedJson()
    {
        using var input = JsonDocument.Parse("""{"password":"secret"}""");
        var controller = new AuditRedactionController(
            new StubRedactionService { PreviewResult = """{"password":"[REDACTED]"}""" });

        var result = Assert.IsType<OkObjectResult>(
            controller.PreviewRedaction(input.RootElement));
        var redactedData = result.Value?.GetType()
            .GetProperty("RedactedData")?.GetValue(result.Value);

        Assert.IsType<JsonElement>(redactedData);
        Assert.Equal(
            "[REDACTED]",
            ((JsonElement)redactedData!).GetProperty("password").GetString());
    }

    [Fact]
    public void PreviewRedaction_MapsJsonExceptionToBadRequest()
    {
        using var input = JsonDocument.Parse("{}");
        var controller = new AuditRedactionController(
            new StubRedactionService
            {
                PreviewException = new JsonException("invalid json")
            });

        Assert.IsType<BadRequestObjectResult>(
            controller.PreviewRedaction(input.RootElement));
    }

    [Fact]
    public async Task ApplyRedaction_ReturnsCompletedResult()
    {
        var redactionResult = new AuditRedactionResult
        {
            AuditEventId = 42,
            RedactedFields = ["password"],
            Status = "Completed",
            RedactedAtUtc = DateTime.UtcNow
        };
        var controller = new AuditRedactionController(
            new StubRedactionService { ApplyResult = redactionResult });

        var result = Assert.IsType<OkObjectResult>(
            await controller.ApplyRedaction(
                CreateRedactionRequest(),
                CancellationToken.None));

        Assert.Equal(
            42L,
            result.Value?.GetType().GetProperty("AuditEventId")
                ?.GetValue(result.Value));
    }

    [Theory]
    [InlineData("not-found", 404)]
    [InlineData("invalid-argument", 400)]
    [InlineData("conflict", 409)]
    [InlineData("json", 400)]
    public async Task ApplyRedaction_MapsServiceExceptions(
        string exceptionKind,
        int expectedStatusCode)
    {
        Exception exception = exceptionKind switch
        {
            "not-found" => new KeyNotFoundException("missing"),
            "invalid-argument" => new ArgumentException("invalid"),
            "conflict" => new InvalidOperationException("conflict"),
            "json" => new JsonException("invalid json"),
            _ => throw new ArgumentOutOfRangeException(nameof(exceptionKind))
        };
        var controller = new AuditRedactionController(
            new StubRedactionService { ApplyException = exception });

        var result = await controller.ApplyRedaction(
            CreateRedactionRequest(),
            CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(expectedStatusCode, objectResult.StatusCode);
    }

    private static AuditRedactionRequest CreateRedactionRequest() =>
        new()
        {
            AuditEventId = 42,
            FieldsToRedact = ["password"],
            Reason = "privacy request",
            RedactedBy = "USER001"
        };

    private sealed class StubRedactionService : IAuditRedactionService
    {
        public string PreviewResult { get; init; } = "{}";

        public Exception? PreviewException { get; init; }

        public AuditRedactionResult ApplyResult { get; init; } =
            new AuditRedactionResult();

        public Exception? ApplyException { get; init; }

        public string RedactJson(string json)
        {
            if (PreviewException is not null)
            {
                throw PreviewException;
            }

            return PreviewResult;
        }

        public Task<AuditRedactionResult> ApplyRedactionAsync(
            AuditRedactionRequest request,
            CancellationToken cancellationToken)
        {
            if (ApplyException is not null)
            {
                throw ApplyException;
            }

            return Task.FromResult(ApplyResult);
        }
    }
}

public sealed class AuditExportControllerTests
{
    [Fact]
    public async Task ExportJson_ReturnsJsonFile()
    {
        var controller = new AuditExportController(
            new StubExportService { Data = [1, 2, 3] });

        var result = Assert.IsType<FileContentResult>(
            await controller.ExportJson(null, null, null, null, default));

        Assert.Equal("application/json", result.ContentType);
        Assert.Equal("audit-events.json", result.FileDownloadName);
        Assert.Equal(new byte[] { 1, 2, 3 }, result.FileContents);
    }

    [Fact]
    public async Task ExportCsv_ReturnsCsvFile()
    {
        var controller = new AuditExportController(
            new StubExportService { Data = [4, 5] });

        var result = Assert.IsType<FileContentResult>(
            await controller.ExportCsv(null, null, null, null, default));

        Assert.Equal("text/csv", result.ContentType);
        Assert.Equal("audit-events.csv", result.FileDownloadName);
        Assert.Equal(new byte[] { 4, 5 }, result.FileContents);
    }

    [Fact]
    public async Task ExportBundle_ReturnsJsonBundleFile()
    {
        var controller = new AuditExportController(
            new StubExportService { Data = [6] });

        var result = Assert.IsType<FileContentResult>(
            await controller.ExportBundle(
                "USER001",
                null,
                null,
                null,
                default));

        Assert.Equal("application/json", result.ContentType);
        Assert.Equal("audit-export-bundle.json", result.FileDownloadName);
        Assert.Equal(new byte[] { 6 }, result.FileContents);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("csv")]
    [InlineData("bundle")]
    public async Task ExportActions_MapArgumentExceptionToBadRequest(string format)
    {
        var controller = new AuditExportController(
            new StubExportService
            {
                ExceptionToThrow = new ArgumentException("invalid filter")
            });

        var result = format switch
        {
            "json" => await controller.ExportJson(
                null, null, null, null, default),
            "csv" => await controller.ExportCsv(
                null, null, null, null, default),
            "bundle" => await controller.ExportBundle(
                "USER001", null, null, null, default),
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private sealed class StubExportService : IAuditExportService
    {
        public byte[] Data { get; init; } = [];

        public Exception? ExceptionToThrow { get; init; }

        public Task<byte[]> ExportJsonAsync(
            DateTime? fromUtc,
            DateTime? toUtc,
            string? actorId,
            string? resourceId,
            CancellationToken cancellationToken) =>
            GetData();

        public Task<byte[]> ExportCsvAsync(
            DateTime? fromUtc,
            DateTime? toUtc,
            string? actorId,
            string? resourceId,
            CancellationToken cancellationToken) =>
            GetData();

        public Task<byte[]> ExportBundleAsync(
            string? actorId,
            string? resourceId,
            DateTime? fromUtc,
            DateTime? toUtc,
            CancellationToken cancellationToken) =>
            GetData();

        private Task<byte[]> GetData()
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(Data);
        }
    }
}

public sealed class ComplianceReportsControllerTests
{
    [Fact]
    public async Task GetAccountAccessReport_ReturnsReport()
    {
        var report = new ComplianceAccessReportResult
        {
            AccountId = "ACCOUNT100",
            PageNumber = 1,
            PageSize = 50
        };
        var controller = new ComplianceReportsController(
            new StubComplianceReportService { Result = report });

        var result = Assert.IsType<OkObjectResult>(
            await controller.GetAccountAccessReport(
                "ACCOUNT100",
                new ComplianceAccessReportQueryRequest(),
                CancellationToken.None));

        Assert.Same(report, result.Value);
    }

    [Fact]
    public async Task GetAccountAccessReport_MapsArgumentExceptionToBadRequest()
    {
        var controller = new ComplianceReportsController(
            new StubComplianceReportService
            {
                ExceptionToThrow = new ArgumentException("invalid report")
            });

        var result = await controller.GetAccountAccessReport(
            "ACCOUNT100",
            new ComplianceAccessReportQueryRequest(),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private sealed class StubComplianceReportService
        : IComplianceReportService
    {
        public ComplianceAccessReportResult Result { get; init; } =
            new ComplianceAccessReportResult();

        public Exception? ExceptionToThrow { get; init; }

        public Task<ComplianceAccessReportResult> GetAccountAccessReportAsync(
            ComplianceAccessReportQuery query,
            CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(Result);
        }
    }
}
