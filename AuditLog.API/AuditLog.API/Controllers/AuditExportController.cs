using AuditLog.BLL.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AuditLog.API.Controllers
{
    [ApiController]
    [Route("api/audit-export")]
    public class AuditExportController : ControllerBase
    {
        private readonly IAuditExportService _service;

        public AuditExportController(IAuditExportService service)
        {
            _service = service;
        }

        [HttpGet("json")]
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
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("csv")]
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
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("bundle")]
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
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
