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

        public async Task<AuditEventRecord?> GetEventForRedactionAsync(
            long auditEventId,
            CancellationToken cancellationToken)
        {
            const string sql = """
            SELECT
                A.Id,
                A.EventType,
                A.ActorId,
                A.ResourceType,
                A.ResourceId,
                COALESCE(R.RedactedPayloadJson, A.PayloadJson) AS PayloadJson,
                A.EventTimestampUtc,
                A.ContentHash,
                A.PreviousHash,
                A.RecordHash
            FROM dbo.AuditEvents A
            LEFT JOIN dbo.AuditEventRedactions R
                ON A.Id = R.AuditEventId
            WHERE A.Id = @Id;
            """;

            await using var connection =
                new SqlConnection(_connectionString);

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.Add("@Id", SqlDbType.BigInt)
                .Value = auditEventId;

            await connection.OpenAsync(cancellationToken);

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return new AuditEventRecord
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
            };
        }

        public async Task SaveRedactionAsync(
            AuditRedactionRequest request,
            string redactedPayloadJson,
            byte[] redactedContentHash,
            CancellationToken cancellationToken)
        {
            const string sql = """
            SET XACT_ABORT ON;

            BEGIN TRANSACTION;

            IF EXISTS
            (
                SELECT 1
                FROM dbo.AuditEventRedactions WITH (UPDLOCK, HOLDLOCK)
                WHERE AuditEventId = @AuditEventId
                  AND RedactedContentHash IS NOT NULL
            )
                THROW 50001, 'Redaction already exists.', 1;

            UPDATE dbo.AuditEvents
            SET PayloadJson = @RedactedPayloadJson
            WHERE Id = @AuditEventId;

            IF @@ROWCOUNT = 0
                THROW 50002, 'Audit event was not found.', 1;

            IF EXISTS
            (
                SELECT 1
                FROM dbo.AuditEventRedactions WITH (UPDLOCK, HOLDLOCK)
                WHERE AuditEventId = @AuditEventId
            )
            BEGIN
                UPDATE dbo.AuditEventRedactions
                SET
                    RedactedPayloadJson = @RedactedPayloadJson,
                    RedactedFieldsJson = @RedactedFieldsJson,
                    Reason = @Reason,
                    RedactedBy = @RedactedBy,
                    RedactedContentHash = @RedactedContentHash,
                    RedactedAtUtc = SYSUTCDATETIME()
                WHERE AuditEventId = @AuditEventId;
            END
            ELSE
            BEGIN
                INSERT INTO dbo.AuditEventRedactions
                (
                    AuditEventId,
                    RedactedPayloadJson,
                    RedactedFieldsJson,
                    Reason,
                    RedactedBy,
                    RedactedContentHash
                )
                VALUES
                (
                    @AuditEventId,
                    @RedactedPayloadJson,
                    @RedactedFieldsJson,
                    @Reason,
                    @RedactedBy,
                    @RedactedContentHash
                );
            END;

            COMMIT TRANSACTION;
            """;

            await using var connection =
                new SqlConnection(_connectionString);

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.Add("@AuditEventId", SqlDbType.BigInt)
                .Value = request.AuditEventId;

            command.Parameters.Add("@RedactedPayloadJson", SqlDbType.NVarChar, -1)
                .Value = redactedPayloadJson;

            command.Parameters.Add("@RedactedContentHash", SqlDbType.VarBinary, 32)
                .Value = redactedContentHash;

            command.Parameters.Add("@RedactedFieldsJson", SqlDbType.NVarChar, -1)
                .Value = System.Text.Json.JsonSerializer.Serialize(
                    request.FieldsToRedact);

            command.Parameters.Add("@Reason", SqlDbType.NVarChar, 500)
                .Value = request.Reason;

            command.Parameters.Add("@RedactedBy", SqlDbType.NVarChar, 150)
                .Value = request.RedactedBy;

            await connection.OpenAsync(cancellationToken);

            try
            {
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            catch (SqlException ex) when (ex.Number is 50001 or 2627 or 2601)
            {
                throw new InvalidOperationException(
                    "Redaction has already been applied to this audit event.",
                    ex);
            }
            catch (SqlException ex) when (ex.Number == 50002)
            {
                throw new KeyNotFoundException(
                    "Audit event not found.",
                    ex);
            }
        }
    }
}
