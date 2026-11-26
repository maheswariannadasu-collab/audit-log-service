using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using AuditLog.BLL.Services;

namespace AuditLog.Tests;

public sealed class ComplianceReportServiceTests
{
    [Fact]
    public async Task GetAccountAccessReportAsync_FiltersByAccountAndMapsResults()
    {
        var timestamp = DateTime.SpecifyKind(
            new DateTime(2026, 11, 1, 10, 30, 0),
            DateTimeKind.Utc);
        var repository = new RecordingAuditEventRepository(
            new AuditEventRecord
            {
                Id = 12,
                EventType = "ACCOUNT_VIEWED",
                ActorId = "USER001",
                ResourceType = "ACCOUNT",
                ResourceId = "ACCOUNT100",
                PayloadJson = """{"ipAddress":"192.0.2.10","outcome":"success"}""",
                EventTimestampUtc = timestamp
            });
        var service = new ComplianceReportService(
            new AuditEventQueryService(repository));
        var fromUtc = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var toUtc = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero);

        var result = await service.GetAccountAccessReportAsync(
            new ComplianceAccessReportQuery
            {
                AccountId = "ACCOUNT100",
                FromUtc = fromUtc,
                ToUtc = toUtc,
                PageNumber = 2,
                PageSize = 25
            });

        Assert.NotNull(repository.ReceivedQuery);
        Assert.Equal("ACCOUNT", repository.ReceivedQuery.ResourceType);
        Assert.Equal("ACCOUNT100", repository.ReceivedQuery.ResourceId);
        Assert.Equal(fromUtc, repository.ReceivedQuery.FromUtc);
        Assert.Equal(toUtc, repository.ReceivedQuery.ToUtc);
        Assert.Equal(2, repository.ReceivedQuery.PageNumber);
        Assert.Equal(25, repository.ReceivedQuery.PageSize);
        Assert.Equal("ACCOUNT100", result.AccountId);
        Assert.Single(result.Items);
        Assert.Equal(12, result.Items[0].EventId);
        Assert.Equal("USER001", result.Items[0].ActorId);
        Assert.Equal("ACCOUNT100", result.Items[0].AccountId);
        Assert.Equal(
            "192.0.2.10",
            result.Items[0].Payload.GetProperty("ipAddress").GetString());
        Assert.Equal(
            new DateTimeOffset(timestamp),
            result.Items[0].EventTimestampUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetAccountAccessReportAsync_RejectsMissingAccountId(
        string accountId)
    {
        var service = new ComplianceReportService(
            new AuditEventQueryService(
                new RecordingAuditEventRepository()));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetAccountAccessReportAsync(
                new ComplianceAccessReportQuery
                {
                    AccountId = accountId
                }));
    }

    [Fact]
    public async Task GetAccountAccessReportAsync_RejectsInvalidDateRange()
    {
        var service = new ComplianceReportService(
            new AuditEventQueryService(
                new RecordingAuditEventRepository()));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetAccountAccessReportAsync(
                new ComplianceAccessReportQuery
                {
                    AccountId = "ACCOUNT100",
                    FromUtc = new DateTimeOffset(
                        2026, 11, 2, 0, 0, 0, TimeSpan.Zero),
                    ToUtc = new DateTimeOffset(
                        2026, 11, 1, 0, 0, 0, TimeSpan.Zero)
                }));
    }

    private sealed class RecordingAuditEventRepository(
        AuditEventRecord? record = null) : IAuditEventRepository
    {
        public AuditEventQuery? ReceivedQuery { get; private set; }

        public Task<AuditEventQueryResult> QueryAsync(
            AuditEventQuery query,
            CancellationToken cancellationToken = default)
        {
            ReceivedQuery = query;

            return Task.FromResult(new AuditEventQueryResult
            {
                Items = record is null ? [] : [record],
                TotalCount = record is null ? 0 : 1,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize
            });
        }

        public Task<long> InsertAsync(
            AuditEventData auditEvent,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AuditEventRecord?> GetByIdAsync(
            long id,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AuditEventRecord?> GetRedactedByIdAsync(
            long id,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
