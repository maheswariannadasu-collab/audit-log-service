using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuditLog.API.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.ComponentModel.DataAnnotations;
using AuditLog.API.Errors;

namespace AuditLog.API.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [AllowAnonymous]
    [HttpPost("token")]
    [EnableRateLimiting("auth-token")]
    public IActionResult IssueToken([FromBody] DemoTokenRequest request)
    {
        if (!JwtPermissions.DemoUsers.TryGetValue(
                request.Username,
                out var permissions))
        {
            return ApiProblemDetailsFactory.Status(
                HttpContext,
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "Unknown demo user.");
        }

        var mode = _configuration["Jwt:Mode"] ?? "Local";
        if (!string.Equals(mode, "Local", StringComparison.OrdinalIgnoreCase))
        {
            return ApiProblemDetailsFactory.Status(
                HttpContext,
                StatusCodes.Status400BadRequest,
                "Validation failed",
                "Local demo tokens are disabled.");
        }

        var issuer = _configuration["Jwt:Issuer"] ?? "AuditLog.Local";
        var audience = _configuration["Jwt:Audience"] ?? "audit-log-api";
        var signingKey = _configuration["Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            return ApiProblemDetailsFactory.Status(
                HttpContext,
                StatusCodes.Status500InternalServerError,
                "Internal server error",
                "An unexpected error occurred.");
        }

        var lifetimeMinutes = int.TryParse(
            _configuration["Jwt:TokenLifetimeMinutes"],
            out var parsedLifetime)
            ? parsedLifetime
            : 60;

        var subject = GetStableSubject(request.Username);
        var accountId = request.AccountId?.Trim();
        if (string.IsNullOrWhiteSpace(accountId))
        {
            accountId =
                string.Equals(request.Username, "Administrator",
                    StringComparison.Ordinal)
                    ? "*"
                    : "ACCOUNT100";
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject),
            new("tid", "AUDITLOG-DEMO"),
            new("accountId", accountId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("preferred_username", request.Username)
        };

        claims.AddRange(permissions.Select(permission =>
            new Claim("permissions", permission)));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(lifetimeMinutes),
            signingCredentials: credentials);

        return Ok(new
        {
            accessToken = new JwtSecurityTokenHandler().WriteToken(token),
            tokenType = "Bearer",
            expiresAtUtc = token.ValidTo
        });
    }

    private static string GetStableSubject(string username)
    {
        return $"demo-{username.ToLowerInvariant()}";
    }
}

public sealed class DemoTokenRequest
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(100, MinimumLength = 1,
        ErrorMessage = "Username must contain 1 to 100 characters.")]
    public string Username { get; init; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(500, MinimumLength = 1,
        ErrorMessage = "Password must contain 1 to 500 characters.")]
    public string Password { get; init; } = string.Empty;

    public string? AccountId { get; init; }
}
