
using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

namespace AuditLog.DAL.Repositories
{
    public class AuditExportRepository : IAuditExportRepository
    {
        private readonly string _connectionString;

        public AuditExportRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<AuditExport>> GetAuditEventsAsync(
            DateTime? fromUtc,
            DateTime? toUtc,
            string? actorId,
            string? resourceId,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    A.Id,
                    A.EventType,
                    A.ActorId,
                    A.CreatedBySubject,
                    A.CreatedByDisplayName,
                    A.ResourceType,
                    A.ResourceId,
                    COALESCE(R.RedactedPayloadJson, A.PayloadJson) AS PayloadJson,
                    A.EventTimestampUtc,
                    A.IsArchived,
                    CONVERT(VARCHAR(64), A.ContentHash, 2) AS ContentHash,
                    CONVERT(VARCHAR(64), R.RedactedContentHash, 2) AS RedactedContentHash,
                    CONVERT(VARCHAR(64), A.PreviousHash, 2) AS PreviousHash,
                    CONVERT(VARCHAR(64), A.RecordHash, 2) AS RecordHash,
                    CASE WHEN R.AuditEventId IS NULL
                        THEN CAST(0 AS BIT)
                        ELSE CAST(1 AS BIT)
                    END AS IsRedacted
                FROM dbo.AuditEvents A
                LEFT JOIN dbo.AuditEventRedactions R
                    ON A.Id = R.AuditEventId
                WHERE
                    (@FromUtc IS NULL OR A.EventTimestampUtc >= @FromUtc)
                    AND
                    (@ToUtc IS NULL OR A.EventTimestampUtc < @ToUtc)
                    AND (@ActorId IS NULL OR A.ActorId = @ActorId)
                    AND (@ResourceId IS NULL OR A.ResourceId = @ResourceId)
                ORDER BY A.EventTimestampUtc, A.Id;
                """;

            var results = new List<AuditExport>();

            await using var connection =
                new SqlConnection(_connectionString);

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.Add("@FromUtc", SqlDbType.DateTime2)
                .Value = (object?)fromUtc ?? DBNull.Value;

            command.Parameters.Add("@ToUtc", SqlDbType.DateTime2)
                .Value = (object?)toUtc ?? DBNull.Value;
            command.Parameters.Add("@ActorId", SqlDbType.NVarChar, 150)
                .Value = (object?)actorId ?? DBNull.Value;
            command.Parameters.Add("@ResourceId", SqlDbType.NVarChar, 150)
                .Value = (object?)resourceId ?? DBNull.Value;

            await connection.OpenAsync(cancellationToken);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new AuditExport
                {
                    Id = reader.GetInt64(0),
                    EventType = reader.GetString(1),
                    ActorId = reader.GetString(2),
                    CreatedBySubject = reader.IsDBNull(3)
                        ? string.Empty
                        : reader.GetString(3),
                    CreatedByDisplayName = reader.IsDBNull(4)
                        ? string.Empty
                        : reader.GetString(4),
                    ResourceType = reader.GetString(5),
                    ResourceId = reader.GetString(6),
                    Payload = ParsePayload(reader.GetString(7)),
                    EventTimestampUtc = reader.GetDateTime(8),
                    IsArchived = reader.GetBoolean(9),
                    ContentHash = reader.GetString(10),
                    RedactedContentHash = reader.IsDBNull(11)
                        ? null
                        : reader.GetString(11),
                    PreviousHash = reader.GetString(12),
                    RecordHash = reader.GetString(13),
                    IsRedacted = reader.GetBoolean(14)
                });
            }

            return results;
        }

        public async Task<List<AuditExportChainRecord>> GetChainProofAsync(
            long throughId,
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    Id,
                    CONVERT(VARCHAR(64), ContentHash, 2) AS ContentHash,
                    CONVERT(VARCHAR(64), PreviousHash, 2) AS PreviousHash,
                    CONVERT(VARCHAR(64), RecordHash, 2) AS RecordHash
                FROM dbo.AuditEvents
                WHERE Id <= @ThroughId
                ORDER BY Id ASC;
                """;

            var records = new List<AuditExportChainRecord>();

            await using var connection =
                new SqlConnection(_connectionString);

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.Add("@ThroughId", SqlDbType.BigInt)
                .Value = throughId;

            await connection.OpenAsync(cancellationToken);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                records.Add(new AuditExportChainRecord
                {
                    Id = reader.GetInt64(0),
                    ContentHash = reader.GetString(1),
                    PreviousHash = reader.GetString(2),
                    RecordHash = reader.GetString(3)
                });
            }

            return records;
        }

        private static JsonElement ParsePayload(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }
}
