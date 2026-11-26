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
        private readonly AuditEventQueryService _queryService;


        public AuditEventsController(
            AuditEventWriterService writerService, AuditEventQueryService queryService)
        {
            _writerService = writerService;
            _queryService = queryService;
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

        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetById(
        long id,
        CancellationToken cancellationToken)
        {
            if (id <= 0)
            {
                return BadRequest("ID must be greater than zero.");
            }

            var auditEvent = await _queryService.GetRedactedByIdAsync(
                id,
                cancellationToken);

            if (auditEvent is null)
            {
                return NotFound(new
                {
                    message = "Audit event not found."
                });
            }

            var response = new AuditEventResponse
            {
                Id = auditEvent.Id,
                EventType = auditEvent.EventType,
                ActorId = auditEvent.ActorId,
                ResourceType = auditEvent.ResourceType,
                ResourceId = auditEvent.ResourceId,
                EventTimestampUtc = auditEvent.EventTimestampUtc,
                Payload = System.Text.Json.JsonDocument
                    .Parse(auditEvent.PayloadJson).RootElement.Clone(),
                ContentHash = Convert.ToHexString(auditEvent.ContentHash),
                PreviousHash = Convert.ToHexString(auditEvent.PreviousHash),
                RecordHash = Convert.ToHexString(auditEvent.RecordHash)
            };

            return Ok(response);
        }
    }
}
