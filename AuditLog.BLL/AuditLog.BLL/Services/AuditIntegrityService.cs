using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using System.Security.Cryptography;
using System.Text.Json;

namespace AuditLog.BLL.Services
{
    public sealed class AuditIntegrityService : IAuditIntegrityService
    {
        private readonly IAuditIntegrityRepository _repository;
        private readonly AuditHashingService _hashingService;

        public AuditIntegrityService(
            IAuditIntegrityRepository repository,
            AuditHashingService hashingService)
        {
            _repository = repository;
            _hashingService = hashingService;
        }

        public async Task<AuditIntegrityResult> VerifyAsync(
            CancellationToken cancellationToken = default)
        {
            var result = new AuditIntegrityResult
            {
                IsValid = true,
                TotalRecords = 0,
                VerifiedRecords = 0
            };

            byte[] expectedPreviousHash =
                _hashingService.CreateGenesisHash();

            await foreach (var integrityRecord in
                _repository.StreamForIntegrityCheckAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                result.TotalRecords = integrityRecord.TotalRecords;
                var record = integrityRecord.Event;

                // 1. Verify previous hash.
                if (!CryptographicOperations.FixedTimeEquals(
                        expectedPreviousHash,
                        record.PreviousHash))
                {
                    return Fail(
                        result,
                        record.Id,
                        "Previous hash mismatch.");
                }

                try
                {
                    // Reconstruct the original event data.
                    using var document =
                        JsonDocument.Parse(record.PayloadJson);

                    var auditEvent = new AuditEventData
                    {
                        EventType = record.EventType,
                        ActorId = record.ActorId,
                        ResourceType = record.ResourceType,
                        ResourceId = record.ResourceId,
                        Payload = document.RootElement,
                        EventTimestampUtc = new DateTimeOffset(
                            DateTime.SpecifyKind(
                                record.EventTimestampUtc,
                                DateTimeKind.Utc))
                    };

                    // 2. Verify content hash.
                    byte[] calculatedContentHash =
                        _hashingService.ComputeContentHash(auditEvent);

                    if (!CryptographicOperations.FixedTimeEquals(
                            calculatedContentHash,
                            record.ContentHash))
                    {
                        return Fail(
                            result,
                            record.Id,
                            "Content hash mismatch.");
                    }

                    // 3. Verify record hash.
                    byte[] calculatedRecordHash =
                        _hashingService.ComputeRecordHash(
                            record.PreviousHash,
                            calculatedContentHash);

                    if (!CryptographicOperations.FixedTimeEquals(
                            calculatedRecordHash,
                            record.RecordHash))
                    {
                        return Fail(
                            result,
                            record.Id,
                            "Record hash mismatch.");
                    }

                    expectedPreviousHash = record.RecordHash;

                    result.VerifiedRecords++;
                }
                catch (JsonException)
                {
                    return Fail(
                        result,
                        record.Id,
                        "Invalid original payload JSON.");
                }
                catch (ArgumentException)
                {
                    return Fail(
                        result,
                        record.Id,
                        "Invalid hash data.");
                }
            }

            result.Message =
                "Audit hash chain verification successful.";

            return result;
        }

        private static AuditIntegrityResult Fail(
            AuditIntegrityResult result,
            long recordId,
            string message)
        {
            result.IsValid = false;
            result.FailedRecordId = recordId;
            result.Message = message;

            return result;
        }
    }
}
