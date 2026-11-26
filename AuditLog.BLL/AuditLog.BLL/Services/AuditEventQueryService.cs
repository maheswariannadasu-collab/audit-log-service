using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;

namespace AuditLog.BLL.Services
{
    public sealed class AuditEventQueryService
    {
        private readonly IAuditEventRepository _repository;

        public AuditEventQueryService(
            IAuditEventRepository repository)
        {
            _repository = repository;
        }

        public Task<AuditEventRecord?> GetByIdAsync(
            long id,
            CancellationToken cancellationToken = default)
        {
            if (id <= 0)
            {
                throw new ArgumentException(
                    "ID must be greater than zero.");
            }

            return _repository.GetByIdAsync(
                id,
                cancellationToken);
        }

        public async Task<AuditEventRecord?> GetRedactedByIdAsync(
        long id,
        CancellationToken cancellationToken)
            {
                return await _repository.GetRedactedByIdAsync(
                    id,
                    cancellationToken);
            }
    }
}
