using System.Net;
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

namespace AuditLog.Tests;

public sealed class ApiKeyAuthorizationTests
    : IClassFixture<ApiKeyAuthorizationApiFactory>
{
    private readonly HttpClient _client;
    private readonly ApiKeyAuthorizationApiFactory _factory;

    public ApiKeyAuthorizationTests(
        ApiKeyAuthorizationApiFactory factory)
    {
        _factory = factory;
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
    public async Task GetAccountAccessReport_WithMultipleApiKeys_ReturnsUnauthorized()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/compliance-reports/accounts/ACCOUNT100/access");
        request.Headers.Add(
            ComplianceApiKeyDefaults.HeaderName,
            [
                ApiKeyAuthorizationApiFactory.ConfiguredKey,
                ApiKeyAuthorizationApiFactory.ConfiguredKey
            ]);

        using var response = await _client.SendAsync(request);

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

    [Fact]
    public async Task AuditEventCreate_WithMissingRequiredFields_ReturnsBadRequest()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/audit-events")
        {
            Content = new StringContent(
                "{}",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add(
            ComplianceApiKeyDefaults.HeaderName,
            ApiKeyAuthorizationApiFactory.ConfiguredKey);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AuditEventQuery_WithInvalidPagination_ReturnsBadRequest()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/audit-events?pageNumber=0");
        request.Headers.Add(
            ComplianceApiKeyDefaults.HeaderName,
            ApiKeyAuthorizationApiFactory.ConfiguredKey);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ComplianceReport_WithInvalidAccountOrPagination_ReturnsBadRequest()
    {
        var overlongAccountId = new string('A', 151);
        using var invalidAccountRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/compliance-reports/accounts/{overlongAccountId}/access");
        invalidAccountRequest.Headers.Add(
            ComplianceApiKeyDefaults.HeaderName,
            ApiKeyAuthorizationApiFactory.ConfiguredKey);

        using var invalidAccountResponse =
            await _client.SendAsync(invalidAccountRequest);

        Assert.Equal(HttpStatusCode.BadRequest, invalidAccountResponse.StatusCode);

        using var invalidPaginationRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/compliance-reports/accounts/ACCOUNT100/access?pageSize=101");
        invalidPaginationRequest.Headers.Add(
            ComplianceApiKeyDefaults.HeaderName,
            ApiKeyAuthorizationApiFactory.ConfiguredKey);

        using var invalidPaginationResponse =
            await _client.SendAsync(invalidPaginationRequest);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            invalidPaginationResponse.StatusCode);
    }

    [Fact]
    public async Task DatabaseHealth_WhenDatabaseUnavailable_ReturnsServiceUnavailable()
    {
        Assert.Equal(
            "Server=127.0.0.1,1;Connect Timeout=1;Encrypt=False;TrustServerCertificate=True",
            _factory.Services
                .GetRequiredService<IConfiguration>()
                .GetConnectionString("AuditLogDb"));

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/health/database");
        request.Headers.Add(
            ComplianceApiKeyDefaults.HeaderName,
            ApiKeyAuthorizationApiFactory.ConfiguredKey);

        using var response = await _client.SendAsync(request);

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.ServiceUnavailable,
            $"Expected 503 but got {(int)response.StatusCode}: {responseBody}");
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
    [InlineData("GET", "/api/health/database")]
    public async Task ProtectedEndpoints_WithoutValidApiKey_ReturnUnauthorized(
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
                        "Server=127.0.0.1,1;Connect Timeout=1;Encrypt=False;TrustServerCertificate=True",
                    ["ComplianceReporting:ApiKey"] =
                        ConfiguredKey
                });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<SqlConnectionFactory>();
            services.AddSingleton(new SqlConnectionFactory(
                "Server=127.0.0.1,1;Connect Timeout=1;Encrypt=False;TrustServerCertificate=True"));
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
