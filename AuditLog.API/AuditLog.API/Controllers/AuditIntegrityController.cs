using AuditLog.BLL.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AuditLog.API.Controllers
{
    [ApiController]
    [Route("api/audit-integrity")]
    [Route("audit")]
    public class AuditIntegrityController : ControllerBase
    {
        private readonly IAuditIntegrityService _integrityService;

        public AuditIntegrityController(
            IAuditIntegrityService integrityService)
        {
            _integrityService = integrityService;
        }

        [HttpGet("verify")]
        public async Task<IActionResult> Verify(
            CancellationToken cancellationToken)
        {
            var result = await _integrityService.VerifyAsync(
                cancellationToken);

            if (!result.IsValid)
            {
                return Conflict(result);
            }

            return Ok(result);
        }
    }
}
