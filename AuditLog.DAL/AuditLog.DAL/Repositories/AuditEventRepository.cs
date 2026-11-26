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
    }
}
