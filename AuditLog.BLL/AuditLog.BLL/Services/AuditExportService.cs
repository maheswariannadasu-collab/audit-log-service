
using System.Text;
using System.Text.Json;
using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;

namespace AuditLog.BLL.Services
{
    public class AuditExportService : IAuditExportService
    {
        private readonly IAuditExportRepository _repository;
        private readonly AuditHashingService _hashingService;

        public AuditExportService(
            IAuditExportRepository repository,
            AuditHashingService hashingService)
        {
            _repository = repository;
            _hashingService = hashingService;
        }

        public async Task<byte[]> ExportJsonAsync(
            DateTime? fromUtc,
            DateTime? toUtc,
            string? actorId,
            string? resourceId,
            CancellationToken cancellationToken)
        {
            ValidateRange(fromUtc, toUtc);
            ValidateFilters(actorId, resourceId);

            var records = await _repository.GetAuditEventsAsync(
                fromUtc, toUtc, actorId, resourceId, cancellationToken);
            AddMissingRedactedHashes(records);

            return JsonSerializer.SerializeToUtf8Bytes(
                records,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });
        }

        public async Task<byte[]> ExportCsvAsync(
            DateTime? fromUtc,
            DateTime? toUtc,
            string? actorId,
            string? resourceId,
            CancellationToken cancellationToken)
        {
            ValidateRange(fromUtc, toUtc);
            ValidateFilters(actorId, resourceId);

            var records = await _repository.GetAuditEventsAsync(
                fromUtc, toUtc, actorId, resourceId, cancellationToken);
            AddMissingRedactedHashes(records);

            var csv = new StringBuilder();

            csv.AppendLine(
                "Id,EventType,ActorId,ResourceType,ResourceId,Payload," +
                "EventTimestampUtc,IsArchived,IsRedacted,ContentHash," +
                "RedactedContentHash,PreviousHash,RecordHash");

            foreach (var record in records)
            {
                csv.AppendLine(string.Join(",",
                    record.Id.ToString(),
                    Escape(record.EventType),
                    Escape(record.ActorId),
                    Escape(record.ResourceType),
                    Escape(record.ResourceId),
                    Escape(record.Payload.GetRawText()),
                    Escape(record.EventTimestampUtc.ToString("O")),
                    record.IsArchived.ToString(),
                    record.IsRedacted.ToString(),
                    Escape(record.ContentHash),
                    Escape(record.RedactedContentHash),
                    Escape(record.PreviousHash),
                    Escape(record.RecordHash)));
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        public async Task<byte[]> ExportBundleAsync(
            string? actorId,
            string? resourceId,
            DateTime? fromUtc,
            DateTime? toUtc,
            CancellationToken cancellationToken)
        {
            ValidateRange(fromUtc, toUtc);
            ValidateFilters(actorId, resourceId);

            if (string.IsNullOrWhiteSpace(actorId) &&
                string.IsNullOrWhiteSpace(resourceId))
            {
                throw new ArgumentException(
                    "Specify an actorId or resourceId for a verifiable export.");
            }

            var events = await _repository.GetAuditEventsAsync(
                fromUtc, toUtc, actorId, resourceId, cancellationToken);
            AddMissingRedactedHashes(events);

            var throughId = events.Count == 0
                ? 0
                : events.Max(record => record.Id);

            List<AuditExportChainRecord> chainProof;
            if (throughId == 0)
            {
                chainProof = [];
            }
            else
            {
                chainProof = await _repository.GetChainProofAsync(
                    throughId,
                    cancellationToken);
            }

            var genesisHash = Convert.ToHexString(
                _hashingService.CreateGenesisHash());

            var bundle = new AuditExportBundle
            {
                GeneratedAtUtc = DateTime.UtcNow,
                ActorId = actorId,
                ResourceId = resourceId,
                FromUtc = fromUtc,
                ToUtc = toUtc,
                GenesisHash = genesisHash,
                ChainTipRecordHash = chainProof.Count == 0
                    ? genesisHash
                    : chainProof[^1].RecordHash,
                Events = events,
                ChainProof = chainProof
            };

            return JsonSerializer.SerializeToUtf8Bytes(
                bundle,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });
        }

        private static string Escape(string? value)
        {
            if (value == null)
                return "";

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static void ValidateRange(
            DateTime? fromUtc,
            DateTime? toUtc)
        {
            if (fromUtc.HasValue &&
                toUtc.HasValue &&
                fromUtc.Value >= toUtc.Value)
            {
                throw new ArgumentException(
                    "FromUtc must be earlier than ToUtc.");
            }
        }

        private static void ValidateFilters(
            string? actorId,
            string? resourceId)
        {
            if (actorId is not null &&
                (string.IsNullOrWhiteSpace(actorId) || actorId.Length > 150))
            {
                throw new ArgumentException(
                    "ActorId must contain 1 to 150 characters.",
                    nameof(actorId));
            }

            if (resourceId is not null &&
                (string.IsNullOrWhiteSpace(resourceId) ||
                 resourceId.Length > 150))
            {
                throw new ArgumentException(
                    "ResourceId must contain 1 to 150 characters.",
                    nameof(resourceId));
            }
        }

        private void AddMissingRedactedHashes(
            IEnumerable<AuditExport> records)
        {
            foreach (var record in records)
            {
                if (!record.IsRedacted ||
                    record.RedactedContentHash is not null)
                {
                    continue;
                }

                var auditEvent = new AuditEventData
                {
                    EventType = record.EventType,
                    ActorId = record.ActorId,
                    CreatedBySubject = record.CreatedBySubject,
                    CreatedByDisplayName = record.CreatedByDisplayName,
                    ResourceType = record.ResourceType,
                    ResourceId = record.ResourceId,
                    EventTimestampUtc = new DateTimeOffset(
                        DateTime.SpecifyKind(
                            record.EventTimestampUtc,
                            DateTimeKind.Utc)),
                    Payload = record.Payload
                };

                record.RedactedContentHash = Convert.ToHexString(
                    _hashingService.ComputeContentHash(auditEvent));
            }
        }
    }
}
