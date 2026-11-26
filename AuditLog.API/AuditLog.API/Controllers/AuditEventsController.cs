using AuditLog.API.DTOs;
using AuditLog.BLL.Models;
using AuditLog.BLL.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AuditLog.API.Controllers
{
    [ApiController]
    [Route("api/audit-events")]
    public sealed class AuditEventsController : ControllerBase
    {
        private readonly AuditEventWriterService _writerService;

        public AuditEventsController(
            AuditEventWriterService writerService)
        {
            _writerService = writerService;
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateAuditEventRequest request,
            CancellationToken cancellationToken)
        {
            if (request.Payload.ValueKind == JsonValueKind.Undefined ||
                request.Payload.ValueKind == JsonValueKind.Undefined)
            {
                return BadRequest("Payload is required.");
            }

            if (request.Payload.ValueKind != JsonValueKind.Object)
            {
                return BadRequest("Payload must be a JSON object.");
            }

            if (request.EventTimestampUtc is null)
            {
                return BadRequest("EventTimestampUtc is required.");
            }

            var auditEvent = new AuditEventData
            {
                EventType = request.EventType,
                ActorId = request.ActorId,
                ResourceType = request.ResourceType,
                ResourceId = request.ResourceId,
                EventTimestampUtc =
                    request.EventTimestampUtc.Value,
                Payload = request.Payload.Clone()
            };

            long id = await _writerService.WriteAsync(
                auditEvent,
                cancellationToken);

            var response = new CreateAuditEventResponse
            {
                Id = id,
                Message = "Audit event created successfully."
            };

            return StatusCode(
                StatusCodes.Status201Created,
                response);
        }
    }
}
