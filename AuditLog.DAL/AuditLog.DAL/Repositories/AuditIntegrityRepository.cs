using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using Microsoft.Data.SqlClient;
using System.Runtime.CompilerServices;

namespace AuditLog.DAL.Repositories
{
    public sealed class AuditIntegrityRepository
    : IAuditIntegrityRepository
    {
        private readonly string _connectionString;

        public AuditIntegrityRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async IAsyncEnumerable<AuditIntegrityRecord>
            StreamForIntegrityCheckAsync(
                [EnumeratorCancellation]
                CancellationToken cancellationToken = default)
        {
            const string sql = """
            SELECT
                A.Id,
                A.EventType,
                A.ActorId,
                A.ResourceType,
                A.ResourceId,
                A.PayloadJson,
                A.EventTimestampUtc,
                A.ContentHash,
                A.PreviousHash,
                A.RecordHash,
                R.RedactedContentHash,
                COUNT_BIG(*) OVER() AS TotalRecords
            FROM dbo.AuditEvents A
            LEFT JOIN dbo.AuditEventRedactions R
                ON A.Id = R.AuditEventId
            ORDER BY A.Id ASC;
            """;

            await using var connection =
                new SqlConnection(_connectionString);

            await connection.OpenAsync(cancellationToken);

            await using var command =
                new SqlCommand(sql, connection);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                yield return new AuditIntegrityRecord
                {
                    RedactedContentHash = reader.IsDBNull(10)
                        ? null
                        : (byte[])reader["RedactedContentHash"],
                    TotalRecords = reader.GetInt64(11),
                    Event = new AuditEventRecord
                    {
                        Id = reader.GetInt64(0),
                        EventType = reader.GetString(1),
                        ActorId = reader.GetString(2),
                        ResourceType = reader.GetString(3),
                        ResourceId = reader.GetString(4),
                        PayloadJson = reader.GetString(5),
                        EventTimestampUtc = reader.GetDateTime(6),
                        ContentHash = (byte[])reader["ContentHash"],
                        PreviousHash = (byte[])reader["PreviousHash"],
                        RecordHash = (byte[])reader["RecordHash"]
                    }
                };
            }
        }
    }
}
