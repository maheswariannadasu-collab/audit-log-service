
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using AuditLog.BLL.Models;

namespace AuditLog.BLL.Services;

public sealed class AuditHashingService
{
    private const int HashLength = 32;

    // First record's previous hash: 32 zero bytes.
    public byte[] CreateGenesisHash()
    {
        return new byte[HashLength];
    }

    // Hash of the original event content.
    public byte[] ComputeContentHash(AuditEventData auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();

            writer.WriteString("eventType", auditEvent.EventType);
            writer.WriteString("actorId", auditEvent.ActorId);
            if (!string.IsNullOrWhiteSpace(auditEvent.CreatedBySubject))
            {
                writer.WriteString(
                    "createdBySubject",
                    auditEvent.CreatedBySubject);
            }
            if (!string.IsNullOrWhiteSpace(auditEvent.CreatedByDisplayName))
            {
                writer.WriteString(
                    "createdByDisplayName",
                    auditEvent.CreatedByDisplayName);
            }
            writer.WriteString("resourceType", auditEvent.ResourceType);
            writer.WriteString("resourceId", auditEvent.ResourceId);

            writer.WriteString(
                "timestamp",
                auditEvent.EventTimestampUtc
                    .ToUniversalTime()
                    .ToString("O", CultureInfo.InvariantCulture));

            writer.WritePropertyName("payload");

            WriteCanonicalJson(writer, auditEvent.Payload);

            writer.WriteEndObject();
        }

        return SHA256.HashData(stream.ToArray());
    }

    // Hash of PreviousHash + ContentHash.
    public byte[] ComputeRecordHash(
        byte[] previousHash,
        byte[] contentHash)
    {
        ArgumentNullException.ThrowIfNull(previousHash);
        ArgumentNullException.ThrowIfNull(contentHash);

        if (previousHash.Length != HashLength)
        {
            throw new ArgumentException(
                "Previous hash must be 32 bytes.",
                nameof(previousHash));
        }

        if (contentHash.Length != HashLength)
        {
            throw new ArgumentException(
                "Content hash must be 32 bytes.",
                nameof(contentHash));
        }

        Span<byte> combined = stackalloc byte[HashLength * 2];

        previousHash.CopyTo(combined[..HashLength]);
        contentHash.CopyTo(combined[HashLength..]);

        return SHA256.HashData(combined);
    }

    // Produces a readable hexadecimal representation.
    public string ToHex(byte[] hash)
    {
        ArgumentNullException.ThrowIfNull(hash);

        return Convert.ToHexString(hash);
    }

    // Writes JSON objects with properties sorted alphabetically.
    // Array order is preserved.
    private static void WriteCanonicalJson(
        Utf8JsonWriter writer,
        JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:

                writer.WriteStartObject();

                foreach (var property in element
                    .EnumerateObject()
                    .OrderBy(
                        p => p.Name,
                        StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);

                    WriteCanonicalJson(writer, property.Value);
                }

                writer.WriteEndObject();

                break;

            case JsonValueKind.Array:

                writer.WriteStartArray();

                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonicalJson(writer, item);
                }

                writer.WriteEndArray();

                break;

            case JsonValueKind.String:

                writer.WriteStringValue(element.GetString());

                break;

            case JsonValueKind.Number:

                // Preserve the original JSON numeric representation.
                writer.WriteRawValue(
                    element.GetRawText(),
                    skipInputValidation: false);

                break;

            case JsonValueKind.True:

                writer.WriteBooleanValue(true);

                break;

            case JsonValueKind.False:

                writer.WriteBooleanValue(false);

                break;

            case JsonValueKind.Null:

                writer.WriteNullValue();

                break;

            default:

                throw new InvalidOperationException(
                    "Unsupported or undefined JSON payload.");
        }
    }
}
