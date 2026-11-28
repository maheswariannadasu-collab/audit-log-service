using AuditLog.API.Auth;
using AuditLog.API.DTOs;
using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.RateLimiting;
using AuditLog.API.Errors;

namespace AuditLog.API.Controllers;

[ApiController]
[Route("api/compliance-reports")]
[Authorize(Policy = JwtPolicyNames.ReportsRead)]
[EnableRateLimiting("api-general")]
public sealed class ComplianceReportsController : ControllerBase
{
    private readonly IComplianceReportService _reportService;

    public ComplianceReportsController(
        IComplianceReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("accounts/{accountId}/access")]
    public async Task<IActionResult> GetAccountAccessReport(
        [FromRoute]
        [Required]
        [StringLength(150, MinimumLength = 1)]
        string accountId,
        [FromQuery] ComplianceAccessReportQueryRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var accountIdClaim = User.FindFirst("accountId")?.Value;
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

            if (!hasAccountsManage &&
                !string.Equals(
                    accountIdClaim,
                    accountId,
                    StringComparison.Ordinal))
            {
                return ApiProblemDetailsFactory.Status(
                    HttpContext, 403, "Forbidden",
                    "The token is not assigned to this account.");
            }

            var report = await _reportService.GetAccountAccessReportAsync(
                new ComplianceAccessReportQuery
                {
                    AccountId = accountId,
                    FromUtc = request.FromUtc,
                    ToUtc = request.ToUtc,
                    PageNumber = request.PageNumber,
                    PageSize = request.PageSize
                },
                cancellationToken);

            return Ok(report);
        }
        catch (ArgumentException ex)
        {
            return ApiProblemDetailsFactory.Status(
                HttpContext, 400, "Validation failed", ex.Message);
        }
    }
}
