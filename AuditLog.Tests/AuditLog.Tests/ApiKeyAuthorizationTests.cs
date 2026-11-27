using System.Net;
using AuditLog.API.Auth;
using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AuditLog.Tests;

public sealed class ApiKeyAuthorizationTests
    : IClassFixture<ApiKeyAuthorizationApiFactory>
{
    private readonly HttpClient _client;

    public ApiKeyAuthorizationTests(
        ApiKeyAuthorizationApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAccountAccessReport_WithoutApiKey_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync(
            "/api/compliance-reports/accounts/ACCOUNT100/access");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAccountAccessReport_WithInvalidApiKey_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/compliance-reports/accounts/ACCOUNT100/access");
        request.Headers.Add(
            ComplianceApiKeyDefaults.HeaderName,
            "incorrect-key");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAccountAccessReport_WithValidApiKey_IsAuthorized()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/compliance-reports/accounts/ACCOUNT100/access");
        request.Headers.Add(
            ComplianceApiKeyDefaults.HeaderName,
            ApiKeyAuthorizationApiFactory.ConfiguredKey);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/audit-events")]
    [InlineData("GET", "/api/audit-events/1")]
    [InlineData("GET", "/api/audit-events")]
    [InlineData("GET", "/audit/verify")]
    [InlineData("GET", "/api/audit-integrity/verify")]
    [InlineData("POST", "/api/audit-retention/archive")]
    [InlineData("POST", "/api/audit-redaction/preview")]
    [InlineData("POST", "/api/audit-redaction/apply")]
    [InlineData("GET", "/api/audit-export/json")]
    [InlineData("GET", "/api/audit-export/csv")]
    [InlineData("GET", "/api/audit-export/bundle")]
    public async Task ScenarioAAndBEndpoints_WithoutValidApiKey_ReturnUnauthorized(
        string method,
        string path)
    {
        using var request = new HttpRequestMessage(
            new HttpMethod(method),
            path);
        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        using var invalidKeyRequest = new HttpRequestMessage(
            new HttpMethod(method),
            path);
        invalidKeyRequest.Headers.Add(
            ComplianceApiKeyDefaults.HeaderName,
            "incorrect-key");
        using var invalidKeyResponse =
            await _client.SendAsync(invalidKeyRequest);

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            invalidKeyResponse.StatusCode);
    }
}

public sealed class ApiKeyAuthorizationApiFactory
    : WebApplicationFactory<Program>
{
    internal const string ConfiguredKey =
        "TEST_ONLY_ComplianceApiKeyForIntegrationTests_0123456789";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AuditLogDb"] =
                        "Server=localhost;Database=AuditLogTests;Integrated Security=true;TrustServerCertificate=true",
                    ["ComplianceReporting:ApiKey"] =
                        ConfiguredKey
                });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IComplianceReportService>();
            services.AddSingleton<
                IComplianceReportService,
                StubComplianceReportService>();
        });
    }

    private sealed class StubComplianceReportService
        : IComplianceReportService
    {
        public Task<ComplianceAccessReportResult> GetAccountAccessReportAsync(
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
