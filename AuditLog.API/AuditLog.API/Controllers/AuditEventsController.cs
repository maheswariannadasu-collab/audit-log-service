using AuditLog.API.Auth;
using AuditLog.API.DTOs;
using AuditLog.BLL.Models;
using AuditLog.BLL.Services;
using Microsoft.AspNetCore.Authorization;
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
        [Authorize(Policy = JwtPolicyNames.EventsWrite)]
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

            var subject = User.FindFirst("sub")?.Value;
            var displayName = User.FindFirst("preferred_username")?.Value;
            if (string.IsNullOrWhiteSpace(subject))
            {
                return Unauthorized();
            }

            var auditEvent = new AuditEventData
            {
                EventType = request.EventType,
                ActorId = subject,
                CreatedBySubject = subject,
                CreatedByDisplayName = displayName ?? subject,
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
        [Authorize(Policy = JwtPolicyNames.EventsRead)]
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

            return Ok(ToResponse(auditEvent));
        }

        [HttpGet]
        [Authorize(Policy = JwtPolicyNames.EventsRead)]
        public async Task<IActionResult> Query(
            [FromQuery] AuditEventQueryRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await _queryService.QueryAsync(
                    new AuditEventQuery
                    {
                        ActorId = request.ActorId,
                        ResourceType = request.ResourceType,
                        ResourceId = request.ResourceId,
                        EventType = request.EventType,
                        FromUtc = request.FromUtc,
                        ToUtc = request.ToUtc,
                        PageNumber = request.PageNumber,
                        PageSize = request.PageSize
                    },
                    cancellationToken);

                return Ok(new AuditEventPageResponse
                {
                    Items = result.Items.Select(ToResponse).ToArray(),
                    TotalCount = result.TotalCount,
                    PageNumber = result.PageNumber,
                    PageSize = result.PageSize
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private static AuditEventResponse ToResponse(
            AuditEventRecord auditEvent)
        {
            using var payloadDocument =
                JsonDocument.Parse(auditEvent.PayloadJson);

            return new AuditEventResponse
            {
                Id = auditEvent.Id,
                EventType = auditEvent.EventType,
                ActorId = auditEvent.ActorId,
                ResourceType = auditEvent.ResourceType,
                ResourceId = auditEvent.ResourceId,
                EventTimestampUtc = auditEvent.EventTimestampUtc,
                Payload = payloadDocument.RootElement.Clone(),
                ContentHash = Convert.ToHexString(auditEvent.ContentHash),
                PreviousHash = Convert.ToHexString(auditEvent.PreviousHash),
                RecordHash = Convert.ToHexString(auditEvent.RecordHash)
            };
        }
    }
}
