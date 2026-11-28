using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using AuditLog.BLL.Services;

namespace AuditLog.Tests;

public sealed class AuditEventQueryServiceTests
{
    [Fact]
    public async Task GetByIdAsync_ForwardsPositiveIdAndCancellationToken()
    {
        using var cancellationSource = new CancellationTokenSource();
        var repository = new RecordingAuditEventRepository();
        var service = new AuditEventQueryService(repository);

        var result = await service.GetByIdAsync(
            42,
            cancellationSource.Token);

        Assert.Null(result);
        Assert.Equal(42, repository.ReceivedId);
        Assert.Equal(cancellationSource.Token, repository.ReceivedToken);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetByIdAsync_RejectsNonPositiveId(long id)
    {
        var repository = new RecordingAuditEventRepository();
        var service = new AuditEventQueryService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetByIdAsync(id));

        Assert.Null(repository.ReceivedId);
    }

    [Fact]
    public async Task GetRedactedByIdAsync_ForwardsId()
    {
        var repository = new RecordingAuditEventRepository();
        var service = new AuditEventQueryService(repository);

        await service.GetRedactedByIdAsync(17, CancellationToken.None);

        Assert.Equal(17, repository.ReceivedRedactedId);
    }

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

    [Fact]
    public async Task QueryAsync_RejectsEqualTimeBounds()
    {
        var bound = new DateTimeOffset(
            2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var service = new AuditEventQueryService(
            new RecordingAuditEventRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.QueryAsync(new AuditEventQuery
            {
                FromUtc = bound,
                ToUtc = bound
            }));
    }

    [Theory]
    [InlineData("actor", " ")]
    [InlineData("actor", "too-long")]
    [InlineData("resource-type", " ")]
    [InlineData("resource-type", "too-long")]
    [InlineData("resource-id", " ")]
    [InlineData("resource-id", "too-long")]
    [InlineData("event-type", " ")]
    [InlineData("event-type", "too-long")]
    public async Task QueryAsync_RejectsInvalidFilters(
        string filter,
        string value)
    {
        if (value == "too-long")
        {
            value = new string('x', filter switch
            {
                "actor" or "resource-id" => 151,
                "resource-type" or "event-type" => 101,
                _ => throw new ArgumentOutOfRangeException(nameof(filter))
            });
        }

        var query = filter switch
        {
            "actor" => new AuditEventQuery { ActorId = value },
            "resource-type" => new AuditEventQuery { ResourceType = value },
            "resource-id" => new AuditEventQuery { ResourceId = value },
            "event-type" => new AuditEventQuery { EventType = value },
            _ => throw new ArgumentOutOfRangeException(nameof(filter))
        };
        var service = new AuditEventQueryService(
            new RecordingAuditEventRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.QueryAsync(query));
    }

    [Fact]
    public async Task QueryAsync_RejectsNullQuery()
    {
        var service = new AuditEventQueryService(
            new RecordingAuditEventRepository());

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.QueryAsync(null!));
    }

    private sealed class RecordingAuditEventRepository
        : IAuditEventRepository
    {
        public AuditEventQuery? ReceivedQuery { get; private set; }

        public long? ReceivedId { get; private set; }

        public long? ReceivedRedactedId { get; private set; }

        public CancellationToken ReceivedToken { get; private set; }

        public Task<AuditEventQueryResult> QueryAsync(
            AuditEventQuery query,
            CancellationToken cancellationToken = default)
        {
            ReceivedQuery = query;
            ReceivedToken = cancellationToken;

            return Task.FromResult(new AuditEventQueryResult
            {
                TotalCount = 0,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize
            });
        }

        public Task<AuditEventWriteResult> InsertAsync(
            AuditEventData auditEvent,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AuditEventRecord?> GetByIdAsync(
            long id,
            CancellationToken cancellationToken = default)
        {
            ReceivedId = id;
            ReceivedToken = cancellationToken;
            return Task.FromResult<AuditEventRecord?>(null);
        }

        public Task<AuditEventRecord?> GetRedactedByIdAsync(
            long id,
            CancellationToken cancellationToken = default)
        {
            ReceivedRedactedId = id;
            ReceivedToken = cancellationToken;
            return Task.FromResult<AuditEventRecord?>(null);
        }
    }
}
