using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using Microsoft.Extensions.Options;

namespace AuditLog.BLL.Services
{ 
    public class AuditRetentionService : IAuditRetentionService
    {
        private readonly IAuditRetentionRepository _repository;
        private readonly AuditRetentionOptions _options;

        public AuditRetentionService(
            IAuditRetentionRepository repository,
            IOptions<AuditRetentionOptions> options)
        {
            _repository = repository;
            _options = options.Value;

            if (_options.RetentionDays <= 0)
            {
                throw new InvalidOperationException(
                    "RetentionDays must be greater than zero.");
            }
        }

        public async Task<int> ArchiveExpiredEventsAsync(
            CancellationToken cancellationToken)
        {
            var cutoffUtc = DateTime.UtcNow.AddDays(
                -_options.RetentionDays);

            return await _repository.ArchiveOlderThanAsync(
                cutoffUtc,
                cancellationToken);
        }
    }
}