using AuditLog.BLL.Services;
using AuditLog.DAL.Data;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Read database connection string
var connectionString =
    builder.Configuration.GetConnectionString("AuditLogDb")
    ?? throw new InvalidOperationException(
        "AuditLogDb connection string is missing.");

// Register ADO.NET connection factory
builder.Services.AddSingleton(
    new SqlConnectionFactory(connectionString));

builder.Services.AddSingleton<AuditHashingService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
