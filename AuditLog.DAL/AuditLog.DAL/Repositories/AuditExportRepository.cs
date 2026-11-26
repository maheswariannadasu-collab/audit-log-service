
using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using Microsoft.Data.SqlClient;
using System.Data;

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
            CancellationToken cancellationToken)
        {
            const string sql = """
                SELECT
                    A.Id,
                    A.EventType,
                    A.ResourceType,
                    A.ResourceId,
                    A.EventTimestampUtc,
                    A.IsArchived,
                    CONVERT(VARCHAR(64), A.ContentHash, 2) AS ContentHash,
                    CONVERT(VARCHAR(64), A.PreviousHash, 2) AS PreviousHash
                FROM dbo.AuditEvents A
                WHERE
                    (@FromUtc IS NULL OR A.EventTimestampUtc >= @FromUtc)
                    AND
                    (@ToUtc IS NULL OR A.EventTimestampUtc < @ToUtc)
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

            await connection.OpenAsync(cancellationToken);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                results.Add(new AuditExport
                {
                    Id = reader.GetInt64(
                        reader.GetOrdinal("Id")),

                    EventType = reader.GetString(
                        reader.GetOrdinal("EventType")),

                    ResourceType = reader.GetString(
                        reader.GetOrdinal("ResourceType")),

                    ResourceId = reader.GetString(
                        reader.GetOrdinal("ResourceId")),

                    EventTimestampUtc = reader.GetDateTime(
                        reader.GetOrdinal("EventTimestampUtc")),

                    IsArchived = reader.GetBoolean(
                        reader.GetOrdinal("IsArchived")),

                    ContentHash = reader.GetString(
                        reader.GetOrdinal("ContentHash")),

                    PreviousHash = reader.GetString(
                        reader.GetOrdinal("PreviousHash"))
                });
            }

            return results;
        }
    }
}
