using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace AuditLog.API.Auth;

public sealed class ComplianceApiKeyAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IConfiguration _configuration;

    public ComplianceApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(
                ComplianceApiKeyDefaults.HeaderName,
                out var providedValues) ||
            providedValues.Count != 1)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var configuredKey = _configuration[
            ComplianceApiKeyDefaults.ConfigurationKey];

        if (!ComplianceApiKeyValidator.IsValid(
                providedValues[0],
                configuredKey))
        {
            return Task.FromResult(
                AuthenticateResult.Fail("Invalid compliance API key."));
        }

        var claims = new[]
        {
            new Claim(
                ClaimTypes.NameIdentifier,
                "compliance-report-reader"),
            new Claim(
                ClaimTypes.Role,
                ComplianceApiKeyDefaults.RoleName)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
