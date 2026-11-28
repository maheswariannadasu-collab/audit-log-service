using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using AuditLog.BLL.Services;

namespace AuditLog.Tests;

public sealed class AuditEventWriterServiceTests
{
    [Fact]
    public async Task WriteAsync_ForwardsEventAndCancellationToken()
    {
        using var cancellationSource = new CancellationTokenSource();
        var repository = new RecordingAuditEventRepository();
        var service = new AuditEventWriterService(repository);
        var auditEvent = new AuditEventData();

        var result = await service.WriteAsync(
            auditEvent,
            cancellationSource.Token);

        Assert.Equal(42, result.Id);
        Assert.Same(auditEvent, repository.ReceivedEvent);
        Assert.Equal(cancellationSource.Token, repository.ReceivedToken);
    }

    [Fact]
    public async Task WriteAsync_RejectsNullEventBeforeCallingRepository()
    {
        var repository = new RecordingAuditEventRepository();
        var service = new AuditEventWriterService(repository);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.WriteAsync(null!));

        Assert.Null(repository.ReceivedEvent);
    }

    [Fact]
    public async Task WriteAsync_PropagatesRepositoryFailure()
    {
        var repository = new RecordingAuditEventRepository
        {
            ExceptionToThrow = new InvalidOperationException("insert failed")
        };
        var service = new AuditEventWriterService(repository);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.WriteAsync(new AuditEventData()));

        Assert.Equal("insert failed", exception.Message);
    }

    private sealed class RecordingAuditEventRepository
        : IAuditEventRepository
    {
        public AuditEventData? ReceivedEvent { get; private set; }

        public CancellationToken ReceivedToken { get; private set; }

        public Exception? ExceptionToThrow { get; init; }

        public Task<AuditEventWriteResult> InsertAsync(
            AuditEventData auditEvent,
            CancellationToken cancellationToken = default)
        {
            ReceivedEvent = auditEvent;
            ReceivedToken = cancellationToken;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(new AuditEventWriteResult
            {
                Id = 42,
                IsReplay = false
            });
        }

        public Task<AuditEventRecord?> GetByIdAsync(
            long id,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AuditEventRecord?> GetRedactedByIdAsync(
            long id,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<AuditEventQueryResult> QueryAsync(
            AuditEventQuery query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
