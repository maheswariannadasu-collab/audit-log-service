using AuditLog.DAL.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Data.SqlClient;
using AuditLog.API.Errors;

namespace AuditLog.API.Controllers
{
    [Route("/health")]
    [ApiController]
    [AllowAnonymous]
    [DisableRateLimiting]
    public class HealthController : ControllerBase
    {
        private readonly SqlConnectionFactory _connectionFactory;

        public HealthController(SqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        // GET /health - Public, No DB, No Auth, No Rate Limit
        [HttpGet]
        public IActionResult Check()
        {
            return Ok(new { status = "Healthy" });
        }

        // GET /health/database - Checks DB
        [HttpGet("database")]
        public async Task<IActionResult> CheckDatabase(CancellationToken cancellationToken)
        {
            try
            {
                await using var connection = _connectionFactory.CreateConnection();
                await connection.OpenAsync(cancellationToken);
                await using var command = new SqlCommand("SELECT 1", connection);
                var result = await command.ExecuteScalarAsync(cancellationToken);

                return Ok(new
                {
                    status = "Healthy",
                    database = "Connected",
                    result
                });
            }
            catch (SqlException)
            {
                return ApiProblemDetailsFactory.Status(
                    HttpContext,
                    StatusCodes.Status503ServiceUnavailable,
                    "Service unavailable",
                    "Database health check failed.");
            }
        }
    }
}