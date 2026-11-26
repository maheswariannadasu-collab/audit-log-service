
using System.Text;
using System.Text.Json;
using AuditLog.BLL.Interfaces;

namespace AuditLog.BLL.Services
{
    public class AuditExportService : IAuditExportService
    {
        private readonly IAuditExportRepository _repository;

        public AuditExportService(
            IAuditExportRepository repository)
        {
            _repository = repository;
        }

        public async Task<byte[]> ExportJsonAsync(
            DateTime? fromUtc,
            DateTime? toUtc,
            CancellationToken cancellationToken)
        {
            ValidateRange(fromUtc, toUtc);

            var records = await _repository.GetAuditEventsAsync(
                fromUtc, toUtc, cancellationToken);

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
            CancellationToken cancellationToken)
        {
            ValidateRange(fromUtc, toUtc);

            var records = await _repository.GetAuditEventsAsync(
                fromUtc, toUtc, cancellationToken);

            var csv = new StringBuilder();

            csv.AppendLine(
                "Id,EventType,ResourceType,ResourceId," +
                "EventTimestampUtc,IsArchived,ContentHash,PreviousHash");

            foreach (var record in records)
            {
                csv.AppendLine(string.Join(",",
                    record.Id.ToString(),
                    Escape(record.EventType),
                    Escape(record.ResourceType),
                    Escape(record.ResourceId),
                    Escape(record.EventTimestampUtc.ToString("O")),
                    record.IsArchived.ToString(),
                    Escape(record.ContentHash),
                    Escape(record.PreviousHash)));
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
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
    }
}
