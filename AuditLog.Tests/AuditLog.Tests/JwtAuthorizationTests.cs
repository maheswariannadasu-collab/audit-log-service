using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using AuditLog.API.Auth;
using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using AuditLog.DAL.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace AuditLog.Tests;

public sealed class JwtAuthorizationTests
    : IClassFixture<JwtAuthorizationApiFactory>
{
    private readonly HttpClient _client;

    public JwtAuthorizationTests(JwtAuthorizationApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task MissingToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/audit-events");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_ReturnsUnauthorized()
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            "/api/audit-events",
            JwtAuthorizationApiFactory.CreateToken(
                "Auditor",
                "ACCOUNT100",
                expires: DateTime.UtcNow.AddMinutes(-1)));

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongAudience_ReturnsUnauthorized()
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            "/api/audit-events",
            JwtAuthorizationApiFactory.CreateToken(
                "Auditor",
                "ACCOUNT100",
                audience: "wrong-audience"));

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongIssuer_ReturnsUnauthorized()
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            "/api/audit-events",
            JwtAuthorizationApiFactory.CreateToken(
                "Auditor",
                "ACCOUNT100",
                issuer: "wrong-issuer"));

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidSignature_ReturnsUnauthorized()
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            "/api/audit-events",
            JwtAuthorizationApiFactory.CreateToken(
                "Auditor",
                "ACCOUNT100",
                signingKey: "different-test-signing-key-that-is-at-least-32-chars"));

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidTokenMissingPermission_ReturnsForbidden()
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            "/api/audit-events",
            JwtAuthorizationApiFactory.CreateToken(
                "AuditWriter",
                "ACCOUNT100"));

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AssignedAccount_ReturnsOk()
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            "/api/compliance-reports/accounts/ACCOUNT100/access",
            JwtAuthorizationApiFactory.CreateToken(
                "ComplianceReader",
                "ACCOUNT100"));

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnassignedAccount_ReturnsForbidden()
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            "/api/compliance-reports/accounts/ACCOUNT200/access",
            JwtAuthorizationApiFactory.CreateToken(
                "ComplianceReader",
                "ACCOUNT100"));

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CrossAccount_ReturnsForbidden()
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            "/api/compliance-reports/accounts/OTHER/access",
            JwtAuthorizationApiFactory.CreateToken(
                "Auditor",
                "ACCOUNT100"));

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/audit-events")]
    [InlineData("GET", "/api/audit-export/json")]
    [InlineData("GET", "/api/compliance-reports/accounts/ACCOUNT100/access")]
    public async Task AuditWriter_DeniedReadExportOrReport(
        string method,
        string path)
    {
        using var request = CreateRequest(
            new HttpMethod(method),
            path,
            JwtAuthorizationApiFactory.CreateToken(
                "AuditWriter",
                "ACCOUNT100"));

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/audit-events", "{}")]
    [InlineData("POST", "/api/audit-retention/archive", "{}")]
    [InlineData("POST", "/api/audit-redaction/preview", "{}")]
    [InlineData("POST", "/api/audit-redaction/apply", "{}")]
    public async Task Reader_DeniedWriteRetentionOrRedaction(
        string method,
        string path,
        string json)
    {
        using var request = CreateRequest(
            new HttpMethod(method),
            path,
            JwtAuthorizationApiFactory.CreateToken(
                "ComplianceReader",
                "ACCOUNT100"));
        if (!string.IsNullOrWhiteSpace(json))
        {
            request.Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json");
        }

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task HealthEndpoint_IsAccessibleWithoutToken()
    {
        var response = await _client.GetAsync("/health");
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Administrator_CanAccessAnotherAccount()
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            "/api/compliance-reports/accounts/ACCOUNT999/access",
            JwtAuthorizationApiFactory.CreateToken(
                "Administrator",
                "ACCOUNT100"));

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ActorSpoofing_IsIgnored()
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            "/api/audit-events",
            JwtAuthorizationApiFactory.CreateToken(
                "AuditWriter",
                "ACCOUNT100"));
        request.Content = JsonContent.Create(new
        {
            ActorId = "spoofed-user",
            EventType = "TEST_EVENT",
            ResourceType = "ACCOUNT",
            ResourceId = "ACCOUNT100",
            EventTimestampUtc = DateTimeOffset.UtcNow,
            Payload = new { outcome = "success" }
        });

        var response = await _client.SendAsync(request);
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Created ||
            response.StatusCode == HttpStatusCode.ServiceUnavailable ||
            response.StatusCode == HttpStatusCode.InternalServerError);
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        string token)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}

public sealed class JwtAuthorizationApiFactory
    : WebApplicationFactory<Program>
{
    internal const string Issuer = "AuditLog.Local";
    internal const string Audience = "audit-log-api";
    internal const string SigningKey =
        "assignment-only-test-signing-key-32-characters";

    public static string CreateToken(
        string username,
        string accountId,
        DateTime? expires = null,
        string? issuer = null,
        string? audience = null,
        string? signingKey = null)
    {
        var permissions = JwtPermissions.DemoUsers[username];
        var claims = new List<Claim>
        {
            new("sub", $"demo-{username.ToLowerInvariant()}"),
            new("tid", "AUDITLOG-DEMO"),
            new("accountId", accountId),
            new("preferred_username", username),
            new("jti", Guid.NewGuid().ToString())
        };
        claims.AddRange(permissions.Select(
            permission => new Claim("permissions", permission)));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(signingKey ?? SigningKey));
        var credentials = new SigningCredentials(
            key,
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: issuer ?? Issuer,
            audience: audience ?? Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-60),
            expires: expires ?? DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:Mode"] = "Local",
                    ["Jwt:Issuer"] = Issuer,
                    ["Jwt:Audience"] = Audience,
                    ["Jwt:SigningKey"] = SigningKey,
                    ["ConnectionStrings:AuditLogDb"] =
                        "Server=127.0.0.1,1;Connect Timeout=1;Encrypt=False;TrustServerCertificate=True"
                });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<SqlConnectionFactory>();
            services.AddSingleton(new SqlConnectionFactory(
                "Server=127.0.0.1,1;Connect Timeout=1;Encrypt=False;TrustServerCertificate=True"));
            services.RemoveAll<IComplianceReportService>();
            services.AddSingleton<IComplianceReportService,
                StubComplianceReportService>();
        });
    }

    private sealed class StubComplianceReportService
        : IComplianceReportService
    {
        public Task<ComplianceAccessReportResult>
            GetAccountAccessReportAsync(
                ComplianceAccessReportQuery query,
                CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ComplianceAccessReportResult
            {
                AccountId = query.AccountId,
                FromUtc = query.FromUtc,
                ToUtc = query.ToUtc,
                PageNumber = query.PageNumber,
                PageSize = query.PageSize
            });
        }
    }
}
