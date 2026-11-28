using AuditLog.BLL.Interfaces;
using AuditLog.BLL.Models;
using AuditLog.BLL.Services;
using AuditLog.API.Auth;
using AuditLog.DAL.Data;
using AuditLog.DAL.Repositories;
using AuditLog.API.Errors;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors
                        .Select(error =>
                            string.IsNullOrWhiteSpace(error.ErrorMessage)
                                ? "The value is invalid."
                                : error.ErrorMessage)
                        .ToArray(),
                    StringComparer.OrdinalIgnoreCase);

            var detail = errors.Values
                .SelectMany(messages => messages)
                .FirstOrDefault()
                ?? "The request is invalid.";

            return ApiProblemDetailsFactory.Validation(
                context.HttpContext,
                detail,
                errors);
        };
    });
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth-token", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    options.AddPolicy("api-general", context =>
    {
        var subject = context.User.FindFirst("sub")?.Value;
        var partitionKey = subject is not null
            ? $"jwt:{subject}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = subject is not null ? 100 : 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });

    options.OnRejected = async (rejectionContext, cancellationToken) =>
    {
        var httpContext = rejectionContext.HttpContext;

        if (httpContext.Request.Path.Equals(
                "/auth/token",
                StringComparison.OrdinalIgnoreCase))
        {
            httpContext.Response.Headers.RetryAfter = "60";
        }

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        httpContext.Response.ContentType = "application/problem+json";

        var problem = ApiProblemDetailsFactory.Create(
            httpContext,
            StatusCodes.Status429TooManyRequests,
            "Too many requests",
            "The request limit has been exceeded. Try again later.");

        await httpContext.Response.WriteAsJsonAsync(
            problem,
            cancellationToken);
    };
});
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
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode =
                    StatusCodes.Status401Unauthorized;
                context.Response.ContentType =
                    "application/problem+json";

                var problem = ApiProblemDetailsFactory.Create(
                    context.HttpContext,
                    StatusCodes.Status401Unauthorized,
                    "Unauthorized",
                    "A valid bearer token is required.");

                await context.Response.WriteAsync(
                            System.Text.Json.JsonSerializer.Serialize(
                                problem,
                                new System.Text.Json.JsonSerializerOptions(
                                    System.Text.Json.JsonSerializerDefaults.Web)),
                    context.HttpContext.RequestAborted);
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode =
                    StatusCodes.Status403Forbidden;
                context.Response.ContentType =
                    "application/problem+json";

                var problem = ApiProblemDetailsFactory.Create(
                    context.HttpContext,
                    StatusCodes.Status403Forbidden,
                    "Forbidden",
                    "The authenticated user is not permitted to perform this action.");

                await context.Response.WriteAsync(
                    System.Text.Json.JsonSerializer.Serialize(
                        problem,
                        new System.Text.Json.JsonSerializerOptions(
                            System.Text.Json.JsonSerializerDefaults.Web)),
                    context.HttpContext.RequestAborted);
            }
        };

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

app.UseExceptionHandler();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Exposes the top-level entry point to WebApplicationFactory in integration tests.
public partial class Program
{
}
