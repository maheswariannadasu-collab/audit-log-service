

using AuditLog.BLL.Interfaces;
using Microsoft.Data.SqlClient;

namespace AuditLog.DAL.Repositories
{
    public class AuditRetentionRepository : IAuditRetentionRepository
    {
        private readonly string _connectionString;

        public AuditRetentionRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<int> ArchiveOlderThanAsync(
            DateTime cutoffUtc,
            CancellationToken cancellationToken)
        {
            const string sql = """
            UPDATE dbo.AuditEvents
            SET
                IsArchived = 1,
                ArchivedAtUtc = SYSUTCDATETIME()
            WHERE
                IsArchived = 0
                AND EventTimestampUtc < @CutoffUtc;
            """;

            await using var connection =
                new SqlConnection(_connectionString);

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@CutoffUtc", cutoffUtc);

            await connection.OpenAsync(cancellationToken);

            return await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}