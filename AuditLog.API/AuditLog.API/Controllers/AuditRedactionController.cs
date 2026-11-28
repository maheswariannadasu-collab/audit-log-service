using AuditLog.API.Auth;
using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using AuditLog.API.Errors;

namespace AuditLog.API.Controllers
{

    [ApiController]
    [Route("api/audit-redaction")]
    [EnableRateLimiting("api-general")]
    public class AuditRedactionController : ControllerBase
    {
        private readonly IAuditRedactionService _redactionService;

        public AuditRedactionController(
            IAuditRedactionService redactionService)
        {
            _redactionService = redactionService;
        }

        [HttpPost("preview")]
        [Authorize(Policy = JwtPolicyNames.RedactionManage)]
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
                return ApiProblemDetailsFactory.Status(
                    HttpContext, 400, "Validation failed",
                    "Invalid JSON supplied.");
            }
        }

        [HttpPost("apply")]
        [Authorize(Policy = JwtPolicyNames.RedactionManage)]
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
                return ApiProblemDetailsFactory.Status(
                    HttpContext, 404, "Not found", ex.Message);
            }
            catch (ArgumentException ex)
            {
                return ApiProblemDetailsFactory.Status(
                    HttpContext, 400, "Validation failed", ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return ApiProblemDetailsFactory.Status(
                    HttpContext, 409, "Conflict", ex.Message);
            }
            catch (JsonException)
            {
                return ApiProblemDetailsFactory.Status(
                    HttpContext, 400, "Validation failed",
                    "Invalid audit payload JSON.");
            }
        }
    }
}
