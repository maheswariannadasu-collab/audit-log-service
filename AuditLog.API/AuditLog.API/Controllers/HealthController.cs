using AuditLog.DAL.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace AuditLog.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HealthController : ControllerBase
    {
        private readonly SqlConnectionFactory _connectionFactory;

        public HealthController(
            SqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        [HttpGet("database")]
        public async Task<IActionResult> CheckDatabase(
            CancellationToken cancellationToken)
        {
            try
            {
                await using var connection =
                    _connectionFactory.CreateConnection();

                await connection.OpenAsync(cancellationToken);

                await using var command =
                    new SqlCommand("SELECT 1", connection);

                var result =
                    await command.ExecuteScalarAsync(cancellationToken);

                return Ok(new
                {
                    status = "Healthy",
                    database = "Connected",
                    result
                });
            }
            catch (SqlException)
            {
                return StatusCode(503, new
                {
                    status = "Unhealthy",
                    message = "Database connection failed."
                });
            }
        }

    }
}
