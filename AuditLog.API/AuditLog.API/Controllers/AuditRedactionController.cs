using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AuditLog.API.Controllers
{

    [ApiController]
    [Route("api/audit-redaction")]
    public class AuditRedactionController : ControllerBase
    {
        private readonly IAuditRedactionService _redactionService;

        public AuditRedactionController(
            IAuditRedactionService redactionService)
        {
            _redactionService = redactionService;
        }

        [HttpPost("preview")]
        public IActionResult PreviewRedaction(
            [FromBody] JsonElement request)
        {
            try
            {
                var result = _redactionService.RedactJson(
                    request.GetRawText());

                return Ok(new
                {
                    Message = "Redaction preview completed.",
                    RedactedData = JsonDocument.Parse(result).RootElement
                });
            }
            catch (JsonException)
            {
                return BadRequest(new
                {
                    Message = "Invalid JSON supplied."
                });
            }
        }

        [HttpPost("apply")]
        public async Task<IActionResult> ApplyRedaction(
            [FromBody] AuditRedactionRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _redactionService.ApplyRedactionAsync(
                    request,
                    cancellationToken);

                return Ok(new
                {
                    Message = "Redaction applied successfully.",
                    AuditEventId = result.AuditEventId,
                    RedactedFields = result.RedactedFields,
                    RedactionStatus = result.Status,
                    RedactedAtUtc = result.RedactedAtUtc
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { Message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { Message = ex.Message });
            }
            catch (JsonException)
            {
                return BadRequest(new
                {
                    Message = "Invalid audit payload JSON."
                });
            }
        }
    }
}
