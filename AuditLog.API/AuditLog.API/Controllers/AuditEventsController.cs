using AuditLog.API.Auth;
using AuditLog.API.DTOs;
using AuditLog.BLL.Models;
using AuditLog.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using AuditLog.API.Errors;

namespace AuditLog.API.Controllers
{
    [ApiController]
    [Route("api/audit-events")]
    [EnableRateLimiting("api-general")]
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
                return ApiProblemDetailsFactory.Status(
                    HttpContext, 400, "Validation failed", "Payload is required.");
            }

            if (request.Payload.ValueKind != JsonValueKind.Object)
            {
                return ApiProblemDetailsFactory.Status(
                    HttpContext, 400, "Validation failed", "Payload must be a JSON object.");
            }

            if (request.EventTimestampUtc is null)
            {
                return ApiProblemDetailsFactory.Status(
                    HttpContext, 400, "Validation failed", "EventTimestampUtc is required.");
            }

            var subject = User.FindFirst("sub")?.Value;
            var displayName = User.FindFirst("preferred_username")?.Value;
            if (string.IsNullOrWhiteSpace(subject))
            {
                return ApiProblemDetailsFactory.Status(
                    HttpContext, 401, "Unauthorized", "A valid bearer token is required.");
            }

            string? idempotencyKey = null;
            if (Request.Headers.TryGetValue("Idempotency-Key",
                    out var idempotencyValues))
            {
                if (idempotencyValues.Count != 1)
                {
                    return ApiProblemDetailsFactory.Status(
                        HttpContext, 400, "Validation failed",
                        "Idempotency-Key must contain exactly one value.");
                }
                idempotencyKey = idempotencyValues[0]?.Trim();
            }
            else if (Request.Headers.TryGetValue("X-Idempotency-Key",
                    out var legacyIdempotencyValues))
            {
                if (legacyIdempotencyValues.Count != 1)
                {
                    return ApiProblemDetailsFactory.Status(
                        HttpContext, 400, "Validation failed",
                        "X-Idempotency-Key must contain exactly one value.");
                }
                idempotencyKey = legacyIdempotencyValues[0]?.Trim();
            }

            if (idempotencyKey is not null &&
                (idempotencyKey.Length == 0 || idempotencyKey.Length > 128))
            {
                return ApiProblemDetailsFactory.Status(
                    HttpContext, 400, "Validation failed",
                    "Idempotency-Key must contain 1 to 128 characters.");
            }

            var auditEvent = new AuditEventData
            {
                EventType = request.EventType,
                ActorId = subject,
                CreatedBySubject = subject,
                CreatedByDisplayName = displayName ?? subject,
                IdempotencyKey = idempotencyKey,
                ResourceType = request.ResourceType,
                ResourceId = request.ResourceId,
                EventTimestampUtc =
                    request.EventTimestampUtc.Value,
                Payload = request.Payload.Clone()
            };

            AuditEventWriteResult writeResult;
            try
            {
                writeResult = await _writerService.WriteAsync(
                    auditEvent,
                    cancellationToken);
            }
            catch (InvalidOperationException ex)
                when (ex.Message.Contains(
                    "idempotency key",
                    StringComparison.OrdinalIgnoreCase))
            {
                return ApiProblemDetailsFactory.Status(
                    HttpContext, 409, "Conflict", ex.Message);
            }

            var response = new CreateAuditEventResponse
            {
                Id = writeResult.Id,
                Message = writeResult.IsReplay
                    ? "Audit event replayed successfully."
                    : "Audit event created successfully."
            };

            if (writeResult.IsReplay)
            {
                Response.Headers["Idempotent-Replayed"] = "true";
            }

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
                return ApiProblemDetailsFactory.Status(
                    HttpContext, 400, "Validation failed",
                    "ID must be greater than zero.");
            }

            var auditEvent = await _queryService.GetRedactedByIdAsync(
                id,
                cancellationToken);

            if (auditEvent is null)
            {
                return ApiProblemDetailsFactory.Status(
                    HttpContext, 404, "Not found",
                    "Audit event not found.");
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
                var subject = User.FindFirst("sub")?.Value;
                var hasAccountsManage = User.FindAll("permissions")
                    .Any(claim => claim.Value ==
                        JwtPermissions.AccountsManage);

                if (string.IsNullOrWhiteSpace(subject))
                {
                    return ApiProblemDetailsFactory.Status(
                        HttpContext, 401, "Unauthorized",
                        "A valid bearer token is required.");
                }

                string? effectiveAccountId = request.AccountId;
                if (!hasAccountsManage)
                {
                    var claimedAccountId = User.FindFirst("accountId")?.Value;
                    if (string.IsNullOrWhiteSpace(claimedAccountId))
                    {
                        return ApiProblemDetailsFactory.Status(
                            HttpContext, 403, "Forbidden",
                            "The token has no account assignment.");
                    }

                    if (!string.IsNullOrWhiteSpace(request.AccountId) &&
                        !string.Equals(
                            claimedAccountId,
                            request.AccountId,
                            StringComparison.Ordinal))
                    {
                        return ApiProblemDetailsFactory.Status(
                            HttpContext, 403, "Forbidden",
                            "The token is not assigned to this account.");
                    }

                    effectiveAccountId = claimedAccountId;
                }

                var sortBy = string.Equals(request.SortBy, "action",
                    StringComparison.OrdinalIgnoreCase)
                    ? "action"
                    : string.Equals(request.SortBy, "accountId",
                        StringComparison.OrdinalIgnoreCase)
                        ? "accountId"
                        : "createdAt";
                var sortDir = string.Equals(request.SortDir, "asc",
                    StringComparison.OrdinalIgnoreCase)
                    ? "asc"
                    : "desc";
                var pageNumber = request.Page;
                if (Request.Query.TryGetValue("pageNumber", out var legacyPageValues)
                    && !Request.Query.ContainsKey("page"))
                {
                    if (legacyPageValues.Count == 1
                        && int.TryParse(
                            legacyPageValues[0],
                            out var legacyPageNumber)
                        && legacyPageNumber >= 1)
                    {
                        pageNumber = legacyPageNumber;
                    }
                    else
                    {
                        return ApiProblemDetailsFactory.Status(
                            HttpContext, 400, "Validation failed",
                            "PageNumber must be greater than zero.");
                    }
                }
                var fromUtc = request.From ?? request.FromUtc;
                var toUtc = request.To ?? request.ToUtc;

                var result = await _queryService.QueryAsync(
                    new AuditEventQuery
                    {
                        ActorId = request.ActorId,
                        ResourceType = request.ResourceType,
                        ResourceId = request.ResourceId,
                        EventType = request.EventType,
                        AccountId = effectiveAccountId,
                        Action = request.Action,
                        Search = request.Search,
                        SortBy = sortBy,
                        SortDir = sortDir,
                        FromUtc = fromUtc,
                        ToUtc = toUtc,
                        PageNumber = pageNumber,
                        PageSize = request.PageSize
                    },
                    cancellationToken);

                return Ok(new AuditEventPageResponse
                {
                    Items = result.Items.Select(ToResponse).ToArray(),
                    TotalCount = result.TotalCount,
                    PageNumber = result.PageNumber,
                    PageSize = result.PageSize,
                    TotalPages = result.TotalPages
                });
            }
            catch (ArgumentException ex)
            {
                return ApiProblemDetailsFactory.Status(
                    HttpContext, 400, "Validation failed", ex.Message);
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
