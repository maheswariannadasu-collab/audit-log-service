using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace AuditLog.DAL.Repositories
{
    public sealed class AuditRedactionRepository
    : IAuditRedactionRepository
    {
        private readonly string _connectionString;

        public AuditRedactionRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<string?> GetPayloadAsync(
            long auditEventId,
            CancellationToken cancellationToken)
        {
            const string sql = """
            SELECT PayloadJson
            FROM dbo.AuditEvents
            WHERE Id = @Id;
            """;

            await using var connection =
                new SqlConnection(_connectionString);

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.Add("@Id", SqlDbType.BigInt)
                .Value = auditEventId;

            await connection.OpenAsync(cancellationToken);

            return await command.ExecuteScalarAsync(cancellationToken)
                as string;
        }

        public async Task SaveRedactionAsync(
            AuditRedactionRequest request,
            string redactedPayloadJson,
            CancellationToken cancellationToken)
        {
            const string sql = """
            INSERT INTO dbo.AuditEventRedactions
            (
                AuditEventId,
                RedactedPayloadJson,
                RedactedFieldsJson,
                Reason,
                RedactedBy
            )
            SELECT
                @AuditEventId,
                @RedactedPayloadJson,
                @RedactedFieldsJson,
                @Reason,
                @RedactedBy
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM dbo.AuditEventRedactions
                WHERE AuditEventId = @AuditEventId
            );
            
            IF @@ROWCOUNT = 0
                THROW 50001, 'Redaction already exists or was not inserted.', 1;
            """;

            await using var connection =
                new SqlConnection(_connectionString);

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.Add("@AuditEventId", SqlDbType.BigInt)
                .Value = request.AuditEventId;

            command.Parameters.Add("@RedactedPayloadJson", SqlDbType.NVarChar, -1)
                .Value = redactedPayloadJson;

            command.Parameters.Add("@RedactedFieldsJson", SqlDbType.NVarChar, -1)
                .Value = System.Text.Json.JsonSerializer.Serialize(
                    request.FieldsToRedact);

            command.Parameters.Add("@Reason", SqlDbType.NVarChar, 500)
                .Value = request.Reason;

            command.Parameters.Add("@RedactedBy", SqlDbType.NVarChar, 150)
                .Value = request.RedactedBy;

            await connection.OpenAsync(cancellationToken);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
