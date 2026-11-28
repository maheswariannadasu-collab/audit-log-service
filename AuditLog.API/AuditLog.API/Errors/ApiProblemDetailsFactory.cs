using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AuditLog.API.Errors;

public sealed class ApiError
{
    public string Type { get; init; } = "about:blank";
    public string Title { get; init; } = "Error";
    public int Status { get; init; }
    public string Detail { get; init; } = "The request could not be completed.";
    public string TraceId { get; init; } = string.Empty;
    public Dictionary<string, string[]> Errors { get; init; } =
        new(StringComparer.Ordinal);
}

public static class ApiProblemDetailsFactory
{
    public const string ValidationType =
        "urn:audit-log-service:error:validation";

    public static ApiError Create(
        HttpContext? httpContext,
        int status,
        string title,
        string detail,
        Dictionary<string, string[]>? errors = null,
        string? type = null)
    {
        return new ApiError
        {
            Type = type ?? "about:blank",
            Title = title,
            Status = status,
            Detail = detail,
            TraceId = httpContext?.TraceIdentifier
                ?? Guid.NewGuid().ToString("N"),
            Errors = errors ??
                new Dictionary<string, string[]>(StringComparer.Ordinal)
        };
    }

    public static ObjectResult Validation(
        HttpContext? httpContext,
        string detail,
        Dictionary<string, string[]> errors)
    {
        var problem = Create(
            httpContext,
            StatusCodes.Status400BadRequest,
            "Validation failed",
            detail,
            errors,
            ValidationType);

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status400BadRequest,
            ContentTypes = { "application/problem+json" }
        };
    }

    public static ObjectResult Status(
        HttpContext? httpContext,
        int status,
        string title,
        string detail,
        Dictionary<string, string[]>? errors = null)
    {
        var problem = Create(
            httpContext,
            status,
            title,
            detail,
            errors);

        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }
}

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(
            exception,
            "Unhandled exception for {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        var status = exception switch
        {
            ArgumentException => StatusCodes.Status400BadRequest,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError
        };

        var title = status switch
        {
            StatusCodes.Status400BadRequest => "Validation failed",
            StatusCodes.Status404NotFound => "Not found",
            _ => "Internal server error"
        };

        var detail = status switch
        {
            StatusCodes.Status400BadRequest => "The request is invalid.",
            StatusCodes.Status404NotFound => "The requested resource was not found.",
            _ => "An unexpected error occurred."
        };

        var problem = ApiProblemDetailsFactory.Create(
            httpContext,
            status,
            title,
            detail);

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(
                problem,
                new System.Text.Json.JsonSerializerOptions(
                    System.Text.Json.JsonSerializerDefaults.Web)),
            cancellationToken);

        return true;
    }
}
