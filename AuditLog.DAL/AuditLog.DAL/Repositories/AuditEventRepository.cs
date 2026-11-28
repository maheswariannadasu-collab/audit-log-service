using System.Data;
using AuditLog.BLL.Models;
using AuditLog.BLL.Services;
using AuditLog.BLL.Interfaces;
using Microsoft.Data.SqlClient;

namespace AuditLog.DAL.Repositories
{
    public sealed class AuditEventRepository : IAuditEventRepository
    {
        private readonly string _connectionString;
        private readonly AuditHashingService _hashingService;

        public AuditEventRepository(
            string connectionString,
            AuditHashingService hashingService)
        {
            _connectionString = connectionString;
            _hashingService = hashingService;
        }

        public async Task<long> InsertAsync(
            AuditEventData auditEvent,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(auditEvent);

            // Generate event content hash.
            byte[] contentHash =
                _hashingService.ComputeContentHash(auditEvent);

            await using var connection =
                new SqlConnection(_connectionString);

            await connection.OpenAsync(cancellationToken);

            await using var transaction =
                (SqlTransaction)await connection.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);

            try
            {
                // Serialize all audit chain insertions.
                await using (var lockCommand = new SqlCommand(
                    """
                DECLARE @result INT;

                EXEC @result = sys.sp_getapplock
                    @Resource = 'AuditEvents-HashChain',
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = 10000;

                SELECT @result;
                """,
                    connection,
                    transaction))
                {
                    object? result =
                        await lockCommand.ExecuteScalarAsync(
                            cancellationToken);

                    int lockResult = Convert.ToInt32(result);

                    if (lockResult < 0)
                    {
                        throw new InvalidOperationException(
                            "Unable to acquire audit chain lock.");
                    }
                }

                // Get the previous record hash.
                byte[] previousHash;

                await using (var previousCommand = new SqlCommand(
                    """
                SELECT TOP (1) RecordHash
                FROM dbo.AuditEvents
                ORDER BY Id DESC;
                """,
                    connection,
                    transaction))
                {
                    object? result =
                        await previousCommand.ExecuteScalarAsync(
                            cancellationToken);

                    previousHash = result is byte[] hash
                        ? hash
                        : _hashingService.CreateGenesisHash();
                }

                // Generate current record hash.
                byte[] recordHash =
                    _hashingService.ComputeRecordHash(
                        previousHash,
                        contentHash);

                // Insert audit event.
                await using var insertCommand = new SqlCommand(
                    """
                INSERT INTO dbo.AuditEvents
                (
                    EventType,
                    ActorId,
                    CreatedBySubject,
                    CreatedByDisplayName,
                    ResourceType,
                    ResourceId,
                    PayloadJson,
                    EventTimestampUtc,
                    ContentHash,
                    PreviousHash,
                    RecordHash
                )
                OUTPUT INSERTED.Id
                VALUES
                (
                    @EventType,
                    @ActorId,
                    @CreatedBySubject,
                    @CreatedByDisplayName,
                    @ResourceType,
                    @ResourceId,
                    @PayloadJson,
                    @EventTimestampUtc,
                    @ContentHash,
                    @PreviousHash,
                    @RecordHash
                );
                """,
                    connection,
                    transaction);

                insertCommand.Parameters.Add(
                    "@EventType", SqlDbType.NVarChar, 100)
                    .Value = auditEvent.EventType;

                insertCommand.Parameters.Add(
                    "@ActorId", SqlDbType.NVarChar, 150)
                    .Value = auditEvent.ActorId;

                insertCommand.Parameters.Add(
                    "@CreatedBySubject", SqlDbType.NVarChar, 150)
                    .Value = auditEvent.CreatedBySubject;

                insertCommand.Parameters.Add(
                    "@CreatedByDisplayName", SqlDbType.NVarChar, 150)
                    .Value = auditEvent.CreatedByDisplayName;

                insertCommand.Parameters.Add(
                    "@ResourceType", SqlDbType.NVarChar, 100)
                    .Value = auditEvent.ResourceType;

                insertCommand.Parameters.Add(
                    "@ResourceId", SqlDbType.NVarChar, 150)
                    .Value = auditEvent.ResourceId;

                insertCommand.Parameters.Add(
                    "@PayloadJson", SqlDbType.NVarChar, -1)
                    .Value = auditEvent.Payload.GetRawText();

                insertCommand.Parameters.Add(
                    "@EventTimestampUtc", SqlDbType.DateTime2)
                    .Value = auditEvent.EventTimestampUtc.UtcDateTime;

                insertCommand.Parameters.Add(
                    "@ContentHash", SqlDbType.VarBinary, 32)
                    .Value = contentHash;

                insertCommand.Parameters.Add(
                    "@PreviousHash", SqlDbType.VarBinary, 32)
                    .Value = previousHash;

                insertCommand.Parameters.Add(
                    "@RecordHash", SqlDbType.VarBinary, 32)
                    .Value = recordHash;

                object? insertedId =
                    await insertCommand.ExecuteScalarAsync(
                        cancellationToken);

                long id = Convert.ToInt64(insertedId);

                await transaction.CommitAsync(cancellationToken);

                return id;
            }
            catch
            {
                await transaction.RollbackAsync(
                    CancellationToken.None);

                throw;
            }
        }

        public async Task<AuditEventRecord?> GetByIdAsync(
    long id,
    CancellationToken cancellationToken = default)
        {
            const string sql = """
        SELECT
            Id,
            EventType,
            ActorId,
            ResourceType,
            ResourceId,
            PayloadJson,
            EventTimestampUtc,
            ContentHash,
            PreviousHash,
            RecordHash
        FROM dbo.AuditEvents
        WHERE Id = @Id;
        """;

            await using var connection =
                new SqlConnection(_connectionString);

            await connection.OpenAsync(cancellationToken);

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken);

            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

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

        public async Task<AuditEventRecord?> GetRedactedByIdAsync(
    long id,
    CancellationToken cancellationToken = default)
        {
            const string sql = """
        SELECT
            A.Id,
            A.EventType,
            A.ActorId,
            A.ResourceType,
            A.ResourceId,
            COALESCE(R.RedactedPayloadJson, A.PayloadJson),
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

            await connection.OpenAsync(cancellationToken);

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;

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

        public async Task<AuditEventQueryResult> QueryAsync(
            AuditEventQuery query,
            CancellationToken cancellationToken = default)
        {
            const string filter = """
                (@ActorId IS NULL OR A.ActorId = @ActorId)
                AND (@ResourceType IS NULL OR A.ResourceType = @ResourceType)
                AND (@ResourceId IS NULL OR A.ResourceId = @ResourceId)
                AND (@EventType IS NULL OR A.EventType = @EventType)
                AND (@FromUtc IS NULL OR A.EventTimestampUtc >= @FromUtc)
                AND (@ToUtc IS NULL OR A.EventTimestampUtc < @ToUtc)
                """;

            var sql = $"""
                SELECT COUNT_BIG(*)
                FROM dbo.AuditEvents A
                WHERE {filter};

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
                WHERE {filter}
                ORDER BY A.Id DESC
                OFFSET @Offset ROWS
                FETCH NEXT @PageSize ROWS ONLY;
                """;

            await using var connection =
                new SqlConnection(_connectionString);

            await connection.OpenAsync(cancellationToken);

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.Add("@ActorId", SqlDbType.NVarChar, 150)
                .Value = (object?)query.ActorId ?? DBNull.Value;
            command.Parameters.Add("@ResourceType", SqlDbType.NVarChar, 100)
                .Value = (object?)query.ResourceType ?? DBNull.Value;
            command.Parameters.Add("@ResourceId", SqlDbType.NVarChar, 150)
                .Value = (object?)query.ResourceId ?? DBNull.Value;
            command.Parameters.Add("@EventType", SqlDbType.NVarChar, 100)
                .Value = (object?)query.EventType ?? DBNull.Value;
            command.Parameters.Add("@FromUtc", SqlDbType.DateTime2)
                .Value = (object?)query.FromUtc?.UtcDateTime ?? DBNull.Value;
            command.Parameters.Add("@ToUtc", SqlDbType.DateTime2)
                .Value = (object?)query.ToUtc?.UtcDateTime ?? DBNull.Value;
            command.Parameters.Add("@Offset", SqlDbType.BigInt)
                .Value = ((long)query.PageNumber - 1) * query.PageSize;
            command.Parameters.Add("@PageSize", SqlDbType.Int)
                .Value = query.PageSize;

            var records = new List<AuditEventRecord>();
            long totalCount;

            await using (var reader =
                await command.ExecuteReaderAsync(cancellationToken))
            {
                if (!await reader.ReadAsync(cancellationToken))
                {
                    throw new InvalidOperationException(
                        "Audit event query did not return a total count.");
                }

                totalCount = reader.GetInt64(0);

                if (!await reader.NextResultAsync(cancellationToken))
                {
                    throw new InvalidOperationException(
                        "Audit event query did not return a result page.");
                }

                while (await reader.ReadAsync(cancellationToken))
                {
                    records.Add(new AuditEventRecord
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
                    });
                }
            }

            return new AuditEventQueryResult
            {
                Items = records,
                TotalCount = totalCount,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize
            };
        }
    }
}
