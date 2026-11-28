using System;
using System.Collections.Generic;
using System.Text;

namespace AuditLog.BLL.Models
{
    public sealed class AuditEventRecord
    {
        public long Id { get; init; }

        public string EventType { get; init; } = string.Empty;

        public string ActorId { get; init; } = string.Empty;

        public string CreatedBySubject { get; init; } = string.Empty;

        public string CreatedByDisplayName { get; init; } = string.Empty;

        public string ResourceType { get; init; } = string.Empty;

        public string ResourceId { get; init; } = string.Empty;

        public string PayloadJson { get; init; } = string.Empty;

        public DateTime EventTimestampUtc { get; init; }

        public byte[] ContentHash { get; init; } = [];

        public byte[] PreviousHash { get; init; } = [];

        public byte[] RecordHash { get; init; } = [];
    }
}
