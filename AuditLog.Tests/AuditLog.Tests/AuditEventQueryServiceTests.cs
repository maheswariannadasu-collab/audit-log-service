using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using AuditLog.BLL.Services;

namespace AuditLog.Tests;

public sealed class AuditEventQueryServiceTests
{
    [Fact]
    public async Task QueryAsync_ForwardsFiltersAndPagination()
    {
        var repository = new RecordingAuditEventRepository();
        var service = new AuditEventQueryService(repository);
        var query = new AuditEventQuery
        {
            ActorId = "actor-1",
            ResourceType = "account",
            ResourceId = "account-2",
            EventType = "ACCOUNT_READ",
            FromUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ToUtc = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            PageNumber = 2,
            PageSize = 25
        };

        var result = await service.QueryAsync(query);

        Assert.Same(query, repository.ReceivedQuery);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(25, result.PageSize);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task QueryAsync_RejectsInvalidPagination(
        int pageNumber,
        int pageSize)
    {
        var service = new AuditEventQueryService(
            new RecordingAuditEventRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.QueryAsync(new AuditEventQuery
            {
                PageNumber = pageNumber,
                PageSize = pageSize
            }));
    }

    [Fact]
    public async Task QueryAsync_RejectsInvalidTimeRange()
    {
        var service = new AuditEventQueryService(
            new RecordingAuditEventRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.QueryAsync(new AuditEventQuery
            {
                FromUtc = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
                ToUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
            }));
    }

    private sealed class RecordingAuditEventRepository
        : IAuditEventRepository
    {
        public AuditEventQuery? ReceivedQuery { get; private set; }

        public Task<AuditEventQueryResult> QueryAsync(
            AuditEventQuery query,
            CancellationToken cancellationToken = default)
        {
            ReceivedQuery = query;

            return Task.FromResult(new AuditEventQueryResult
            {
                TotalCount = 0,
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
