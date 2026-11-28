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

        public Task<AuditEventQueryResult> QueryAsync(
            AuditEventQuery query,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(query);

            if (query.PageNumber < 1)
            {
                throw new ArgumentException(
                    "PageNumber must be greater than zero.",
                    nameof(query));
            }

            if (query.PageSize < 1 ||
                query.PageSize > AuditEventQuery.MaximumPageSize)
            {
                throw new ArgumentException(
                    $"PageSize must be between 1 and {AuditEventQuery.MaximumPageSize}.",
                    nameof(query));
            }

            if (query.FromUtc.HasValue &&
                query.ToUtc.HasValue &&
                query.FromUtc.Value >= query.ToUtc.Value)
            {
                throw new ArgumentException(
                    "FromUtc must be earlier than ToUtc.",
                    nameof(query));
            }

            ValidateFilterLength(query.AccountId, 150, nameof(query.AccountId));
            ValidateFilterLength(query.Action, 100, nameof(query.Action));
            ValidateFilterLength(query.Search, 500, nameof(query.Search));

            ValidateFilterLength(query.ActorId, 150, nameof(query.ActorId));
            ValidateFilterLength(query.ResourceType, 100, nameof(query.ResourceType));
            ValidateFilterLength(query.ResourceId, 150, nameof(query.ResourceId));
            ValidateFilterLength(query.EventType, 100, nameof(query.EventType));

            return _repository.QueryAsync(query, cancellationToken);
        }

        private static void ValidateFilterLength(
            string? value,
            int maximumLength,
            string parameterName)
        {
            if (value is not null &&
                (string.IsNullOrWhiteSpace(value) || value.Length > maximumLength))
            {
                throw new ArgumentException(
                    $"{parameterName} must contain 1 to {maximumLength} characters.",
                    parameterName);
            }
        }
    }
}
