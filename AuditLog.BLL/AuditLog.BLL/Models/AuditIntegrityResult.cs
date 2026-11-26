using System;
using System.Collections.Generic;
using System.Text;

namespace AuditLog.BLL.Models
{
    public sealed class AuditIntegrityResult
    {
        public bool IsValid { get; set; }

        public long TotalRecords { get; set; }

        public long VerifiedRecords { get; set; }

        public long? FailedRecordId { get; set; }

        public string Message { get; set; } = string.Empty;
    }
}
