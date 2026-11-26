using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AuditLog.BLL.Services
{
    public class AuditRedactionService : IAuditRedactionService
    {
        private readonly AuditRedactionOptions _options;
        private readonly IAuditRedactionRepository _repository;

        public AuditRedactionService(
            IOptions<AuditRedactionOptions> options,
            IAuditRedactionRepository repository)
        {
            _options = options.Value;
            _repository = repository;
        }

        public string RedactJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("JSON cannot be empty.");

            var node = JsonNode.Parse(json)
                ?? throw new JsonException("Invalid JSON.");

            RedactNode(node, _options.SensitiveFields);

            return node.ToJsonString();
        }

        public async Task<AuditRedactionResult> ApplyRedactionAsync(
            AuditRedactionRequest request,
            CancellationToken cancellationToken)
        {
            if (request.AuditEventId <= 0)
                throw new ArgumentException("Invalid audit event ID.");

            if (string.IsNullOrWhiteSpace(request.Reason))
                throw new ArgumentException("Reason is required.");

            if (string.IsNullOrWhiteSpace((string?)request.RedactedBy))
                throw new ArgumentException("RedactedBy is required.");

            if (request.FieldsToRedact.Count == 0)
                throw new ArgumentException("Select fields to redact.");

            var allowedFields = _options.SensitiveFields;

            var invalidFields = request.FieldsToRedact
                .Where(field => !allowedFields.Any(
                    allowed => string.Equals(
                        allowed,
                        field,
                        StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (invalidFields.Count > 0)
                throw new ArgumentException(
                    "One or more fields are not approved for redaction.");

            var originalJson = await _repository.GetPayloadAsync(
                request.AuditEventId,
                cancellationToken);

            if (originalJson == null)
                throw new KeyNotFoundException("Audit event not found.");

            var node = JsonNode.Parse(originalJson)
                ?? throw new JsonException("Invalid stored payload.");

            RedactNode(node, request.FieldsToRedact);

            var redactedJson = node.ToJsonString();

            await _repository.SaveRedactionAsync(
                request,
                redactedJson,
                cancellationToken);

            return new AuditRedactionResult
            {
                AuditEventId = request.AuditEventId,
                RedactedFields = request.FieldsToRedact,
                Status = "Completed",
                RedactedAtUtc = DateTime.UtcNow
            };
        }

        private void RedactNode(
            JsonNode node,
            IEnumerable<string> fields)
        {
            var fieldSet = fields.ToHashSet(
                StringComparer.OrdinalIgnoreCase);

            if (node is JsonObject obj)
            {
                foreach (var property in obj.ToList())
                {
                    if (fieldSet.Contains(property.Key))
                    {
                        obj[property.Key] = _options.ReplacementValue;
                    }
                    else if (property.Value != null)
                    {
                        RedactNode(property.Value, fieldSet);
                    }
                }
            }
            else if (node is JsonArray array)
            {
                foreach (var item in array)
                {
                    if (item != null)
                        RedactNode(item, fieldSet);
                }
            }
        }
    }
}
