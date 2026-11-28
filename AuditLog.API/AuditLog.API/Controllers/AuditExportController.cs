using AuditLog.API.Auth;
using AuditLog.BLL.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using AuditLog.API.Errors;

namespace AuditLog.API.Controllers
{
    [ApiController]
    [Route("api/audit-export")]
    [EnableRateLimiting("api-general")]
    public class AuditExportController : ControllerBase
    {
        private readonly IAuditExportService _service;

        public AuditExportController(IAuditExportService service)
        {
            _service = service;
        }

        [HttpGet("json")]
        [Authorize(Policy = JwtPolicyNames.ExportRead)]
        public async Task<IActionResult> ExportJson(
            DateTime? fromUtc,
            DateTime? toUtc,
            string? actorId,
            string? resourceId,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.ExportJsonAsync(
                    fromUtc, toUtc, actorId, resourceId, cancellationToken);

                return File(
                    data,
                    "application/json",
                    "audit-events.json");
            }
            catch (ArgumentException ex)
            {
                return ApiProblemDetailsFactory.Status(
                    HttpContext, 400, "Validation failed", ex.Message);
            }
        }

        [HttpGet("csv")]
        [Authorize(Policy = JwtPolicyNames.ExportRead)]
        public async Task<IActionResult> ExportCsv(
            DateTime? fromUtc,
            DateTime? toUtc,
            string? actorId,
            string? resourceId,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.ExportCsvAsync(
                    fromUtc, toUtc, actorId, resourceId, cancellationToken);

                return File(
                    data,
                    "text/csv",
                    "audit-events.csv");
            }
            catch (ArgumentException ex)
            {
                return ApiProblemDetailsFactory.Status(
                    HttpContext, 400, "Validation failed", ex.Message);
            }
        }

        [HttpGet("bundle")]
        [Authorize(Policy = JwtPolicyNames.ExportRead)]
        public async Task<IActionResult> ExportBundle(
            string? actorId,
            string? resourceId,
            DateTime? fromUtc,
            DateTime? toUtc,
            CancellationToken cancellationToken)
        {
            try
            {
                var data = await _service.ExportBundleAsync(
                    actorId,
                    resourceId,
                    fromUtc,
                    toUtc,
                    cancellationToken);

                return File(
                    data,
                    "application/json",
                    "audit-export-bundle.json");
            }
            catch (ArgumentException ex)
            {
                return ApiProblemDetailsFactory.Status(
                    HttpContext, 400, "Validation failed", ex.Message);
            }
        }
    }
}
