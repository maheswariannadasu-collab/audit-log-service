using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using AuditLog.BLL.Services;
using AuditLog.API.Auth;
using AuditLog.DAL.Data;
using AuditLog.DAL.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

var jwtMode = builder.Configuration["Jwt:Mode"] ?? "Local";
var issuer = builder.Configuration["Jwt:Issuer"] ?? "AuditLog.Local";
var audience = builder.Configuration["Jwt:Audience"] ?? "audit-log-api";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = false;

        if (string.Equals(jwtMode, "Local",
            StringComparison.OrdinalIgnoreCase))
        {
            var signingKey =
                builder.Configuration["Jwt:SigningKey"];
            if (string.IsNullOrWhiteSpace(signingKey))
            {
                throw new InvalidOperationException(
                    "Jwt:SigningKey must be configured in user secrets or configuration.");
            }

            options.TokenValidationParameters =
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(signingKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = "preferred_username",
                    RoleClaimType = "roles"
                };
        }
        else
        {
            var authority = builder.Configuration["Jwt:Authority"];
            if (string.IsNullOrWhiteSpace(authority))
            {
                throw new InvalidOperationException(
                    "Jwt:Authority must be configured for non-local JWT mode.");
            }

            options.Authority = authority;
            options.Audience = audience;
            options.RequireHttpsMetadata = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                NameClaimType = "preferred_username",
                RoleClaimType = "roles"
            };
        }
    });

builder.Services.AddAuthorization(options =>
{
    void PermissionPolicy(string name, string permission)
    {
        options.AddPolicy(name, policy =>
        {
            policy.AddAuthenticationSchemes(
                JwtBearerDefaults.AuthenticationScheme);
            policy.RequireAuthenticatedUser();
            policy.RequireClaim("permissions", permission);
        });
    }

    PermissionPolicy(JwtPolicyNames.EventsRead,
        JwtPermissions.EventsRead);
    PermissionPolicy(JwtPolicyNames.EventsWrite,
        JwtPermissions.EventsWrite);
    PermissionPolicy(JwtPolicyNames.IntegrityVerify,
        JwtPermissions.IntegrityVerify);
    PermissionPolicy(JwtPolicyNames.RetentionManage,
        JwtPermissions.RetentionManage);
    PermissionPolicy(JwtPolicyNames.RedactionManage,
        JwtPermissions.RedactionManage);
    PermissionPolicy(JwtPolicyNames.ExportRead,
        JwtPermissions.ExportRead);
    PermissionPolicy(JwtPolicyNames.ReportsRead,
        JwtPermissions.ReportsRead);
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
