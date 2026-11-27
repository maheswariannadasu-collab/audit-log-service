using AuditLog.API.Auth;
using AuditLog.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AuditLog.API.Controllers
{
    [Authorize(Policy = ComplianceApiKeyDefaults.PolicyName)]
    [ApiController]
    [Route("api/audit-retention")]
    public class AuditRetentionController : ControllerBase
    {
        private readonly IAuditRetentionService _retentionService;

        public AuditRetentionController(
            IAuditRetentionService retentionService)
        {
            _retentionService = retentionService;
        }

        [HttpPost("archive")]
        public async Task<IActionResult> ArchiveExpiredEvents(
            CancellationToken cancellationToken)
        {
            var archivedCount =
                await _retentionService.ArchiveExpiredEventsAsync(
                    cancellationToken);

            return Ok(new
            {
                Message = "Retention process completed.",
                ArchivedRecords = archivedCount
            });
        }
    }
}