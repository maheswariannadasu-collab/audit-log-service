using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using AuditLog.BLL.Services;
using AuditLog.API.Auth;
using AuditLog.DAL.Data;
using AuditLog.DAL.Repositories;
using Microsoft.AspNetCore.Authentication;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            ComplianceApiKeyDefaults.SchemeName;
        options.DefaultChallengeScheme =
            ComplianceApiKeyDefaults.SchemeName;
    })
    .AddScheme<
        AuthenticationSchemeOptions,
        ComplianceApiKeyAuthenticationHandler>(
            ComplianceApiKeyDefaults.SchemeName,
            _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        ComplianceApiKeyDefaults.PolicyName,
        policy => policy
            .AddAuthenticationSchemes(
                ComplianceApiKeyDefaults.SchemeName)
            .RequireAuthenticatedUser()
            .RequireRole(ComplianceApiKeyDefaults.RoleName));
});

// Read database connection string
var connectionString =
    builder.Configuration.GetConnectionString("AuditLogDb")
    ?? throw new InvalidOperationException(
        "AuditLogDb connection string is missing.");

// Register ADO.NET connection factory
builder.Services.AddSingleton(
    new SqlConnectionFactory(connectionString));

builder.Services.AddSingleton<AuditHashingService>();

builder.Services.AddScoped<IAuditEventRepository>(services =>
{
    return new AuditEventRepository(
        connectionString,
        services.GetRequiredService<AuditHashingService>());
});

builder.Services.AddScoped<AuditEventWriterService>();

builder.Services.AddScoped<AuditEventQueryService>();
builder.Services.AddScoped<
    IComplianceReportService,
    ComplianceReportService>();

// Audit Retention
builder.Services.Configure<AuditRetentionOptions>(
    builder.Configuration.GetSection("AuditRetention"));

builder.Services.AddScoped<IAuditRetentionRepository>(sp =>
{
    var configuration =
        sp.GetRequiredService<IConfiguration>();

    return new AuditRetentionRepository(connectionString);
});

builder.Services.AddScoped<
    IAuditRetentionService,
    AuditRetentionService>();

// Audit Redaction
builder.Services.Configure<AuditRedactionOptions>(
    builder.Configuration.GetSection("AuditRedaction"));

builder.Services.AddScoped<
    IAuditRedactionService,
    AuditRedactionService>();

builder.Services.AddScoped<IAuditRedactionRepository>(sp =>
{
    var configuration =
        sp.GetRequiredService<IConfiguration>();

    return new AuditRedactionRepository(connectionString);
});

// Audit Export
builder.Services.AddScoped<IAuditExportRepository>(sp =>
{
    var configuration =
        sp.GetRequiredService<IConfiguration>();

    return new AuditExportRepository(connectionString);
});

builder.Services.AddScoped<
    IAuditExportService,
    AuditExportService>();

// Audit Integrity
builder.Services.AddScoped<IAuditIntegrityRepository>(sp =>
{
    var configuration =
        sp.GetRequiredService<IConfiguration>();

    return new AuditIntegrityRepository(connectionString);
});

builder.Services.AddScoped<
    IAuditIntegrityService,
    AuditIntegrityService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposes the top-level entry point to WebApplicationFactory in integration tests.
public partial class Program
{
}
