using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Services;
using AuditLog.DAL.Data;
using AuditLog.DAL.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace AuditLog.Tests;

public sealed class SqlServerIntegrationFactAttribute : FactAttribute
{
    public SqlServerIntegrationFactAttribute()
    {
        var enabled = Environment.GetEnvironmentVariable(
            SqlServerDatabaseFixture.EnableIntegrationTestsVariable);
        var connectionString = Environment.GetEnvironmentVariable(
            SqlServerDatabaseFixture.AdminConnectionStringVariable);

        if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(connectionString))
        {
            Skip =
                $"Set {SqlServerDatabaseFixture.EnableIntegrationTestsVariable}=true and " +
                $"provide {SqlServerDatabaseFixture.AdminConnectionStringVariable} " +
                "for an isolated SQL Server test instance.";
        }
    }
}

[Trait("Category", "SqlServerIntegration")]
public sealed class SqlServerApiIntegrationTests
    : IClassFixture<SqlServerDatabaseFixture>, IAsyncLifetime
{
    private readonly SqlServerDatabaseFixture _fixture;
    private readonly HttpClient _client;

    public SqlServerApiIntegrationTests(
        SqlServerDatabaseFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.Factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                JwtAuthorizationApiFactory.CreateToken(
                    "Administrator",
                    "ACCOUNT100"));
    }

    public Task InitializeAsync()
    {
        return _fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [SqlServerIntegrationFact]
    public async Task EveryApiEndpoint_RejectsMissingAndInvalidTokens()
    {
        var endpoints = new (HttpMethod Method, string Path)[]
        {
            (HttpMethod.Post, "/api/audit-events"),
            (HttpMethod.Get, "/api/audit-events/1"),
            (HttpMethod.Get, "/api/audit-events"),
            (HttpMethod.Get, "/audit/verify"),
            (HttpMethod.Get, "/api/audit-integrity/verify"),
            (HttpMethod.Post, "/api/audit-retention/archive"),
            (HttpMethod.Post, "/api/audit-redaction/preview"),
            (HttpMethod.Post, "/api/audit-redaction/apply"),
            (HttpMethod.Get, "/api/audit-export/json"),
            (HttpMethod.Get, "/api/audit-export/csv"),
            (HttpMethod.Get, "/api/audit-export/bundle"),
            (HttpMethod.Get,
                "/api/compliance-reports/accounts/ACCOUNT100/access"),
        };

        using var anonymousClient = _fixture.Factory.CreateClient();

        foreach (var endpoint in endpoints)
        {
            using var missingKeyRequest = new HttpRequestMessage(
                endpoint.Method,
                endpoint.Path);
            using var missingKeyResponse =
                await anonymousClient.SendAsync(missingKeyRequest);

            Assert.True(
                missingKeyResponse.StatusCode == HttpStatusCode.Unauthorized,
                $"{endpoint.Method} {endpoint.Path} without a key returned " +
                $"{(int)missingKeyResponse.StatusCode}.");

            using var invalidKeyRequest = new HttpRequestMessage(
                endpoint.Method,
                endpoint.Path);
            invalidKeyRequest.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    "invalid-integration-test-token");
            using var invalidKeyResponse =
                await anonymousClient.SendAsync(invalidKeyRequest);

            Assert.True(
                invalidKeyResponse.StatusCode == HttpStatusCode.Unauthorized,
                $"{endpoint.Method} {endpoint.Path} with an invalid key returned " +
                $"{(int)invalidKeyResponse.StatusCode}.");
        }
    }

    [SqlServerIntegrationFact]
    public async Task HealthEndpoint_ReportsSqlServerHealthy()
    {
        using var response = await _client.GetAsync("/api/health/database");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 but received {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);
        Assert.Equal(
            "Healthy",
            GetProperty(document.RootElement, "status").GetString());
        Assert.Equal(
            "Connected",
            GetProperty(document.RootElement, "database").GetString());
    }

    [SqlServerIntegrationFact]
    public async Task CreateReadAndFilterEvents_PersistThroughSqlServer()
    {
        var actorId = NewIdentifier("actor");
        var resourceId = NewIdentifier("account");
        var eventId = await CreateEventAsync(
            actorId,
            "ACCOUNT",
            resourceId,
            DateTimeOffset.UtcNow,
            new { device = "integration-test", outcome = "success" });
        await CreateEventAsync(
            actorId,
            "ACCOUNT",
            resourceId,
            DateTimeOffset.UtcNow.AddSeconds(1),
            new { device = "second-event", outcome = "success" });

        using var getResponse = await _client.GetAsync(
            $"/api/audit-events/{eventId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        using var getDocument = JsonDocument.Parse(
            await getResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            eventId,
            GetProperty(getDocument.RootElement, "id").GetInt64());
        Assert.Equal(
            "demo-administrator",
            GetProperty(getDocument.RootElement, "actorId").GetString());
        Assert.Equal(
            "integration-test",
            GetProperty(
                GetProperty(getDocument.RootElement, "payload"),
                "device").GetString());

        var queryPath =
            "/api/audit-events?actorId=demo-administrator" +
            "&resourceId=" + Uri.EscapeDataString(resourceId) +
            "&pageNumber=2&pageSize=1";
        using var queryResponse = await _client.GetAsync(queryPath);
        Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);

        using var queryDocument = JsonDocument.Parse(
            await queryResponse.Content.ReadAsStringAsync());
        var items = GetProperty(queryDocument.RootElement, "items");
        Assert.Single(items.EnumerateArray());
        Assert.Equal(
            eventId,
            GetProperty(items[0], "id").GetInt64());
        Assert.Equal(
            2,
            GetProperty(queryDocument.RootElement, "totalCount").GetInt64());
        Assert.Equal(
            2,
            GetProperty(queryDocument.RootElement, "pageNumber").GetInt32());

        Assert.Equal(
            """{"device":"integration-test","outcome":"success"}""",
            await _fixture.GetEventPayloadAsync(eventId));
    }

    [SqlServerIntegrationFact]
    public async Task BothIntegrityRoutes_VerifySqlServerChain()
    {
        await CreateEventAsync(
            NewIdentifier("actor"),
            "ACCOUNT",
            NewIdentifier("account"),
            DateTimeOffset.UtcNow.AddMinutes(-1),
            new { sequence = 1 });
        await CreateEventAsync(
            NewIdentifier("actor"),
            "ACCOUNT",
            NewIdentifier("account"),
            DateTimeOffset.UtcNow,
            new { sequence = 2 });

        foreach (var path in new[]
                 {
                     "/audit/verify",
                     "/api/audit-integrity/verify"
                 })
        {
            using var response = await _client.GetAsync(path);
            var body = await response.Content.ReadAsStringAsync();
            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"Expected 200 from {path}, got {(int)response.StatusCode}: {body}");

            using var document = JsonDocument.Parse(body);
            Assert.True(GetProperty(
                document.RootElement,
                "isValid").GetBoolean());
            Assert.Equal(
                2,
                GetProperty(
                    document.RootElement,
                    "verifiedRecords").GetInt64());
        }
    }

    [SqlServerIntegrationFact]
    public async Task RetentionEndpoint_ArchivesOnlyExpiredEventsAndIsIdempotent()
    {
        var expiredId = await CreateEventAsync(
            NewIdentifier("actor"),
            "ACCOUNT",
            NewIdentifier("expired"),
            DateTimeOffset.UtcNow.AddDays(-3),
            new { status = "expired" });
        var currentId = await CreateEventAsync(
            NewIdentifier("actor"),
            "ACCOUNT",
            NewIdentifier("current"),
            DateTimeOffset.UtcNow,
            new { status = "current" });

        using var response = await _client.PostAsync(
            "/api/audit-retention/archive",
            content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Assert.Equal(
            1,
            GetProperty(document.RootElement, "archivedRecords").GetInt32());
        Assert.True(await _fixture.IsArchivedAsync(expiredId));
        Assert.False(await _fixture.IsArchivedAsync(currentId));

        using var secondResponse = await _client.PostAsync(
            "/api/audit-retention/archive",
            content: null);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        using var secondDocument = JsonDocument.Parse(
            await secondResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            0,
            GetProperty(secondDocument.RootElement, "archivedRecords").GetInt32());
    }

    [SqlServerIntegrationFact]
    public async Task RedactionEndpoint_OverwritesStoredPayloadAndRetainsChainValidity()
    {
        var secret = "sql-redaction-" + Guid.NewGuid().ToString("N");
        var eventId = await CreateEventAsync(
            NewIdentifier("actor"),
            "ACCOUNT",
            NewIdentifier("account"),
            DateTimeOffset.UtcNow,
            new { password = secret, keep = "visible" });

        using var previewResponse = await _client.PostAsJsonAsync(
            "/api/audit-redaction/preview",
            new { password = secret, keep = "visible" });
        Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);
        using var previewDocument = JsonDocument.Parse(
            await previewResponse.Content.ReadAsStringAsync());
        var preview = GetProperty(previewDocument.RootElement, "redactedData");
        Assert.Equal(
            "[REDACTED]",
            GetProperty(preview, "password").GetString());

        var redactionRequest = new
        {
            auditEventId = eventId,
            fieldsToRedact = new[] { "password" },
            reason = "SQL Server integration validation",
            redactedBy = "integration-test"
        };
        using var applyResponse = await _client.PostAsJsonAsync(
            "/api/audit-redaction/apply",
            redactionRequest);
        Assert.Equal(HttpStatusCode.OK, applyResponse.StatusCode);

        var storedPayload = await _fixture.GetEventPayloadAsync(eventId);
        Assert.DoesNotContain(secret, storedPayload, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", storedPayload, StringComparison.Ordinal);
        Assert.Equal(32, (await _fixture.GetRedactedContentHashAsync(eventId)).Length);

        using var getResponse = await _client.GetAsync(
            $"/api/audit-events/{eventId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        using var getDocument = JsonDocument.Parse(
            await getResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            "[REDACTED]",
            GetProperty(
                GetProperty(getDocument.RootElement, "payload"),
                "password").GetString());

        using var verifyResponse = await _client.GetAsync("/audit/verify");
        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);

        using var duplicateResponse = await _client.PostAsJsonAsync(
            "/api/audit-redaction/apply",
            redactionRequest);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task CreateEventWithIdempotencyKey_ReplaysOriginalAuditEvent()
    {
        var timestamp = DateTimeOffset.UtcNow;

        using var firstRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/audit-events");
        firstRequest.Headers.Add("Idempotency-Key", "integration-idempotency-key");
        firstRequest.Content = JsonContent.Create(new
        {
            eventType = "IDEMPOTENCY_TEST",
            resourceType = "ACCOUNT",
            resourceId = "ACCOUNT100",
            eventTimestampUtc = timestamp,
            payload = new { attempt = 1 }
        });

        using var firstResponse = await _client.SendAsync(firstRequest);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        using var firstDocument = JsonDocument.Parse(
            await firstResponse.Content.ReadAsStringAsync());
        var firstId = GetProperty(firstDocument.RootElement, "id").GetInt64();
        Assert.False(
            firstResponse.Headers.Contains("Idempotent-Replayed"));

        using var replayRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/audit-events");
        replayRequest.Headers.Add("Idempotency-Key", "integration-idempotency-key");
        replayRequest.Content = JsonContent.Create(new
        {
            eventType = "IDEMPOTENCY_TEST",
            resourceType = "ACCOUNT",
            resourceId = "ACCOUNT100",
            eventTimestampUtc = timestamp,
            payload = new { attempt = 1 }
        });

        using var replayResponse = await _client.SendAsync(replayRequest);
        Assert.Equal(HttpStatusCode.Created, replayResponse.StatusCode);
        Assert.True(
            replayResponse.Headers.Contains("Idempotent-Replayed"));

        using var replayDocument = JsonDocument.Parse(
            await replayResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            firstId,
            GetProperty(replayDocument.RootElement, "id").GetInt64());

        using var conflictRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/audit-events");
        conflictRequest.Headers.Add(
            "Idempotency-Key",
            "integration-idempotency-key");
        conflictRequest.Content = JsonContent.Create(new
        {
            eventType = "IDEMPOTENCY_TEST",
            resourceType = "ACCOUNT",
            resourceId = "ACCOUNT100",
            eventTimestampUtc = timestamp,
            payload = new { attempt = 2 }
        });

        using var conflictResponse =
            await _client.SendAsync(conflictRequest);
        Assert.Equal(
            HttpStatusCode.Conflict,
            conflictResponse.StatusCode);
    }

    [SqlServerIntegrationFact]
    public async Task ExportEndpoints_ReturnFilteredDataAndVerifiableChainProof()
    {
        var targetResource = NewIdentifier("target");
        await CreateEventAsync(
            NewIdentifier("export-before"),
            "ACCOUNT",
            NewIdentifier("other"),
            DateTimeOffset.UtcNow.AddMinutes(-2),
            new { marker = "before" });
        var targetId = await CreateEventAsync(
            "demo-administrator",
            "ACCOUNT",
            targetResource,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            new { marker = "target-export" });
        await CreateEventAsync(
            NewIdentifier("export-after"),
            "ACCOUNT",
            NewIdentifier("other"),
            DateTimeOffset.UtcNow,
            new { marker = "after" });

        var resourceQuery = Uri.EscapeDataString(targetResource);
        using var jsonResponse = await _client.GetAsync(
            $"/api/audit-export/json?resourceId={resourceQuery}");
        Assert.Equal(HttpStatusCode.OK, jsonResponse.StatusCode);
        Assert.Equal(
            "application/json",
            jsonResponse.Content.Headers.ContentType?.MediaType);
        using var jsonDocument = JsonDocument.Parse(
            await jsonResponse.Content.ReadAsStringAsync());
        var jsonEvents = jsonDocument.RootElement.EnumerateArray().ToArray();
        Assert.Single(jsonEvents);
        Assert.Equal(
            targetId,
            GetProperty(jsonEvents[0], "id").GetInt64());

        using var csvResponse = await _client.GetAsync(
            $"/api/audit-export/csv?resourceId={resourceQuery}");
        Assert.Equal(HttpStatusCode.OK, csvResponse.StatusCode);
        Assert.Equal(
            "text/csv",
            csvResponse.Content.Headers.ContentType?.MediaType);
        var csv = await csvResponse.Content.ReadAsStringAsync();
        Assert.Contains("target-export", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("before", csv, StringComparison.Ordinal);
        Assert.DoesNotContain("after", csv, StringComparison.Ordinal);

        using var bundleResponse = await _client.GetAsync(
            $"/api/audit-export/bundle?resourceId={resourceQuery}");
        Assert.Equal(HttpStatusCode.OK, bundleResponse.StatusCode);
        using var bundleDocument = JsonDocument.Parse(
            await bundleResponse.Content.ReadAsStringAsync());
        var bundle = bundleDocument.RootElement;
        var bundleEvents = GetProperty(bundle, "events");
        var chainProof = GetProperty(bundle, "chainProof")
            .EnumerateArray()
            .ToArray();
        Assert.Single(bundleEvents.EnumerateArray());
        Assert.Equal(targetId, GetProperty(bundleEvents[0], "id").GetInt64());
        Assert.Equal(2, chainProof.Length);

        var previousHash = Convert.FromHexString(
            GetProperty(bundle, "genesisHash").GetString()!);
        Assert.All(previousHash, value => Assert.Equal(0, value));

        foreach (var proofRecord in chainProof)
        {
            var recordPreviousHash = Convert.FromHexString(
                GetProperty(proofRecord, "previousHash").GetString()!);
            var contentHash = Convert.FromHexString(
                GetProperty(proofRecord, "contentHash").GetString()!);
            var recordHash = Convert.FromHexString(
                GetProperty(proofRecord, "recordHash").GetString()!);

            Assert.Equal(previousHash, recordPreviousHash);
            var hashInput = new byte[
                recordPreviousHash.Length + contentHash.Length];
            Buffer.BlockCopy(
                recordPreviousHash,
                0,
                hashInput,
                0,
                recordPreviousHash.Length);
            Buffer.BlockCopy(
                contentHash,
                0,
                hashInput,
                recordPreviousHash.Length,
                contentHash.Length);
            Assert.Equal(SHA256.HashData(hashInput), recordHash);
            previousHash = recordHash;
        }

        Assert.Equal(
            Convert.ToHexString(previousHash),
            GetProperty(bundle, "chainTipRecordHash").GetString());
        Assert.Equal(
            GetProperty(chainProof[^1], "recordHash").GetString(),
            GetProperty(bundleEvents[0], "recordHash").GetString());
    }

    [SqlServerIntegrationFact]
    public async Task ComplianceReport_FiltersAccountDateRangeAndPagination()
    {
        var accountId = NewIdentifier("account-report");
        var fromUtc = DateTimeOffset.UtcNow.AddHours(-3);
        var includedTime = fromUtc.AddMinutes(30);
        var excludedByDateTime = fromUtc.AddHours(2);
        var firstIncludedEventId = await CreateEventAsync(
            NewIdentifier("report-actor"),
            "ACCOUNT",
            accountId,
            includedTime,
            new { action = "included-first" });
        var secondIncludedEventId = await CreateEventAsync(
            NewIdentifier("report-actor"),
            "ACCOUNT",
            accountId,
            includedTime.AddMinutes(10),
            new { action = "included-second" });
        await CreateEventAsync(
            NewIdentifier("report-actor"),
            "ACCOUNT",
            accountId,
            excludedByDateTime,
            new { action = "outside-range" });
        await CreateEventAsync(
            NewIdentifier("report-actor"),
            "ACCOUNT",
            NewIdentifier("different-account"),
            includedTime,
            new { action = "different-account" });

        var fromQuery = Uri.EscapeDataString(fromUtc.ToString("O"));
        var toQuery = Uri.EscapeDataString(
            fromUtc.AddHours(1).ToString("O"));
        var path =
            $"/api/compliance-reports/accounts/{Uri.EscapeDataString(accountId)}/access" +
            $"?fromUtc={fromQuery}&toUtc={toQuery}&pageNumber=1&pageSize=1";
        using var response = await _client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Assert.Equal(
            accountId,
            GetProperty(document.RootElement, "accountId").GetString());
        Assert.Equal(
            2,
            GetProperty(document.RootElement, "totalCount").GetInt64());
        Assert.Equal(
            1,
            GetProperty(document.RootElement, "pageSize").GetInt32());
        Assert.Equal(
            1,
            GetProperty(document.RootElement, "pageNumber").GetInt32());
        var items = GetProperty(document.RootElement, "items");
        Assert.Single(items.EnumerateArray());
        Assert.Equal(
            secondIncludedEventId,
            GetProperty(items[0], "eventId").GetInt64());
        Assert.Equal(
            "included-second",
            GetProperty(
                GetProperty(items[0], "payload"),
                "action").GetString());

        var secondPagePath = path.Replace(
            "pageNumber=1",
            "pageNumber=2",
            StringComparison.Ordinal);
        using var secondPageResponse =
            await _client.GetAsync(secondPagePath);
        Assert.Equal(HttpStatusCode.OK, secondPageResponse.StatusCode);
        using var secondPageDocument = JsonDocument.Parse(
            await secondPageResponse.Content.ReadAsStringAsync());
        var secondPageItems = GetProperty(
            secondPageDocument.RootElement,
            "items");
        Assert.Single(secondPageItems.EnumerateArray());
        Assert.Equal(
            2,
            GetProperty(
                secondPageDocument.RootElement,
                "pageNumber").GetInt32());
        Assert.Equal(
            firstIncludedEventId,
            GetProperty(secondPageItems[0], "eventId").GetInt64());
    }

    [SqlServerIntegrationFact]
    public async Task IntegrityEndpoint_DetectsSqlServerPayloadTampering()
    {
        var eventId = await CreateEventAsync(
            NewIdentifier("tamper-actor"),
            "ACCOUNT",
            NewIdentifier("tamper-account"),
            DateTimeOffset.UtcNow,
            new { state = "original" });

        await _fixture.UpdateEventPayloadAsync(
            eventId,
            """{"state":"tampered"}""");

        using var response = await _client.GetAsync("/api/audit-integrity/verify");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Assert.False(GetProperty(document.RootElement, "isValid").GetBoolean());
        Assert.Equal(
            eventId,
            GetProperty(document.RootElement, "failedRecordId").GetInt64());
    }

    [SqlServerIntegrationFact]
    public async Task IntegrityEndpoint_DetectsTamperingWithRedactedSqlPayload()
    {
        var eventId = await CreateEventAsync(
            NewIdentifier("redacted-tamper-actor"),
            "ACCOUNT",
            NewIdentifier("redacted-tamper-account"),
            DateTimeOffset.UtcNow,
            new { password = "original-secret" });

        using var redactionResponse = await _client.PostAsJsonAsync(
            "/api/audit-redaction/apply",
            new
            {
                auditEventId = eventId,
                fieldsToRedact = new[] { "password" },
                reason = "Tampering integration test",
                redactedBy = "integration-test"
            });
        Assert.Equal(HttpStatusCode.OK, redactionResponse.StatusCode);

        await _fixture.UpdateEventPayloadAsync(
            eventId,
            """{"password":"tampered-secret"}""");

        using var response = await _client.GetAsync("/audit/verify");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());
        Assert.False(GetProperty(document.RootElement, "isValid").GetBoolean());
        Assert.Equal(
            eventId,
            GetProperty(document.RootElement, "failedRecordId").GetInt64());
    }

    private async Task<long> CreateEventAsync(
        string actorId,
        string resourceType,
        string resourceId,
        DateTimeOffset timestamp,
        object payload)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/audit-events",
            new
            {
                eventType = "INTEGRATION_TEST_EVENT",
                resourceType,
                resourceId,
                eventTimestampUtc = timestamp,
                payload
            });
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(
            response.StatusCode == HttpStatusCode.Created,
            $"Expected 201 but received {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);
        return GetProperty(document.RootElement, "id").GetInt64();
    }

    private static string NewIdentifier(string prefix)
    {
        return $"{prefix}-{Guid.NewGuid():N}";
    }

    private static JsonElement GetProperty(
        JsonElement element,
        string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(
                    property.Name,
                    propertyName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return property.Value;
            }
        }

        throw new InvalidOperationException(
            $"JSON property '{propertyName}' was not present.");
    }
}

public sealed class SqlServerDatabaseFixture : IAsyncLifetime
{
    public const string AdminConnectionStringVariable =
        "AUDITLOG_SQLSERVER_ADMIN_CONNECTION_STRING";
    public const string EnableIntegrationTestsVariable =
        "AUDITLOG_RUN_SQLSERVER_INTEGRATION_TESTS";

    private readonly string _adminConnectionString;
    private bool _databaseCreated;

    public SqlServerDatabaseFixture()
    {
        var configuredConnectionString =
            Environment.GetEnvironmentVariable(
                AdminConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            throw new InvalidOperationException(
                $"{AdminConnectionStringVariable} must point to master on a " +
                "dedicated SQL Server test instance.");
        }

        var adminBuilder =
            new SqlConnectionStringBuilder(configuredConnectionString);
        if (!string.Equals(
                adminBuilder.InitialCatalog,
                "master",
                StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(adminBuilder.AttachDBFilename))
        {
            throw new InvalidOperationException(
                $"{AdminConnectionStringVariable} must use the master " +
                "database and must not attach a database file.");
        }

        _adminConnectionString = adminBuilder.ConnectionString;
        DatabaseName = $"AuditLogIntegration_{Guid.NewGuid():N}";
        var databaseBuilder = new SqlConnectionStringBuilder(
            _adminConnectionString)
        {
            InitialCatalog = DatabaseName
        };
        TestConnectionString = databaseBuilder.ConnectionString;
    }

    public string DatabaseName { get; }
    public string TestConnectionString { get; }
    public SqlServerApiFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await CreateDatabaseAsync();

        try
        {
            await ApplyMigrationsAsync();
            Factory = new SqlServerApiFactory(TestConnectionString);
        }
        catch
        {
            await DropDatabaseAsync();
            throw;
        }
    }

    public async Task ResetDatabaseAsync()
    {
        const string sql = """
            DELETE FROM dbo.AuditEventIdempotency;
            DELETE FROM dbo.AuditEventRedactions;
            DELETE FROM dbo.AuditEvents;
            DBCC CHECKIDENT ('dbo.AuditEventIdempotency', RESEED, 0);
            DBCC CHECKIDENT ('dbo.AuditEventRedactions', RESEED, 0);
            DBCC CHECKIDENT ('dbo.AuditEvents', RESEED, 0);
            """;

        await ExecuteTestDatabaseCommandAsync(sql);
    }

    public async Task<string> GetEventPayloadAsync(long eventId)
    {
        await using var connection =
            new SqlConnection(TestConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT PayloadJson FROM dbo.AuditEvents WHERE Id = @Id;",
            connection);
        command.Parameters.Add("@Id", SqlDbType.BigInt).Value = eventId;
        var result = await command.ExecuteScalarAsync();

        return result as string
            ?? throw new InvalidOperationException(
                $"Audit event {eventId} was not found in the test database.");
    }

    public async Task<bool> IsArchivedAsync(long eventId)
    {
        await using var connection =
            new SqlConnection(TestConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            "SELECT IsArchived FROM dbo.AuditEvents WHERE Id = @Id;",
            connection);
        command.Parameters.Add("@Id", SqlDbType.BigInt).Value = eventId;
        var result = await command.ExecuteScalarAsync();

        return result is not null and not DBNull &&
            Convert.ToBoolean(result);
    }

    public async Task<byte[]> GetRedactedContentHashAsync(long eventId)
    {
        await using var connection =
            new SqlConnection(TestConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            """
            SELECT RedactedContentHash
            FROM dbo.AuditEventRedactions
            WHERE AuditEventId = @Id;
            """,
            connection);
        command.Parameters.Add("@Id", SqlDbType.BigInt).Value = eventId;
        var result = await command.ExecuteScalarAsync();

        return result as byte[]
            ?? throw new InvalidOperationException(
                $"Redaction hash for event {eventId} was not found.");
    }

    public async Task UpdateEventPayloadAsync(
        long eventId,
        string payloadJson)
    {
        await using var connection =
            new SqlConnection(TestConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            """
            UPDATE dbo.AuditEvents
            SET PayloadJson = @PayloadJson
            WHERE Id = @Id;
            """,
            connection);
        command.Parameters.Add("@PayloadJson", SqlDbType.NVarChar, -1)
            .Value = payloadJson;
        command.Parameters.Add("@Id", SqlDbType.BigInt).Value = eventId;

        if (await command.ExecuteNonQueryAsync() != 1)
        {
            throw new InvalidOperationException(
                $"Audit event {eventId} was not found in the test database.");
        }
    }

    public async Task DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
            SqlConnection.ClearAllPools();
        }

        await DropDatabaseAsync();
    }

    private async Task CreateDatabaseAsync()
    {
        await using var connection =
            new SqlConnection(_adminConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            $"CREATE DATABASE [{DatabaseName}];",
            connection);
        await command.ExecuteNonQueryAsync();
        _databaseCreated = true;
    }

    private async Task ApplyMigrationsAsync()
    {
        var migrationFiles = new[]
        {
            "001_CreateAuditEvents.sql",
            "002_AddAuditRetentionColumns.sql",
            "003_CreateAuditEventRedactions.sql",
            "004_AddRedactedContentHash.sql",
            "006_AddAuditActorIdentity.sql",
            "007_CreateAuditEventIdempotency.sql"
        };

        foreach (var migrationFile in migrationFiles)
        {
            var path = Path.Combine(
                AppContext.BaseDirectory,
                "DatabaseScripts",
                migrationFile);
            var script = await File.ReadAllTextAsync(path);
            script = Regex.Replace(
                script,
                @"(?im)^[ \t]*USE[ \t]+\[?AuditLogDb\]?[ \t]*;[ \t]*\r?$",
                string.Empty);

            var batches = Regex.Split(
                script,
                @"(?im)^[ \t]*GO[ \t]*\r?$");

            await using var connection =
                new SqlConnection(TestConnectionString);
            await connection.OpenAsync();

            foreach (var batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch))
                {
                    continue;
                }

                await using var command =
                    new SqlCommand(batch, connection)
                    {
                        CommandTimeout = 60
                    };
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    private async Task ExecuteTestDatabaseCommandAsync(string sql)
    {
        await using var connection =
            new SqlConnection(TestConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropDatabaseAsync()
    {
        if (!_databaseCreated)
        {
            return;
        }

        await using var connection =
            new SqlConnection(_adminConnectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(
            $"""
            ALTER DATABASE [{DatabaseName}]
                SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            DROP DATABASE [{DatabaseName}];
            """,
            connection);
        await command.ExecuteNonQueryAsync();
        _databaseCreated = false;
    }
}

public sealed class SqlServerApiFactory
    : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public SqlServerApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:AuditLogDb"] = _connectionString,
                    ["Jwt:Mode"] = "Local",
                    ["Jwt:Issuer"] = JwtAuthorizationApiFactory.Issuer,
                    ["Jwt:Audience"] = JwtAuthorizationApiFactory.Audience,
                    ["Jwt:SigningKey"] = JwtAuthorizationApiFactory.SigningKey,
                    ["AuditRetention:RetentionDays"] = "1"
                });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<SqlConnectionFactory>();
            services.AddSingleton(
                new SqlConnectionFactory(_connectionString));

            services.RemoveAll<IAuditEventRepository>();
            services.AddScoped<IAuditEventRepository>(serviceProvider =>
                new AuditEventRepository(
                    _connectionString,
                    serviceProvider.GetRequiredService<AuditHashingService>()));

            services.RemoveAll<IAuditRetentionRepository>();
            services.AddScoped<IAuditRetentionRepository>(_ =>
                new AuditRetentionRepository(_connectionString));

            services.RemoveAll<IAuditRedactionRepository>();
            services.AddScoped<IAuditRedactionRepository>(_ =>
                new AuditRedactionRepository(_connectionString));

            services.RemoveAll<IAuditExportRepository>();
            services.AddScoped<IAuditExportRepository>(_ =>
                new AuditExportRepository(_connectionString));

            services.RemoveAll<IAuditIntegrityRepository>();
            services.AddScoped<IAuditIntegrityRepository>(_ =>
                new AuditIntegrityRepository(_connectionString));
        });
    }
}
