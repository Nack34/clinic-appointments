using System.Text.Json.Serialization;
using Clinic.Api.Data;
using Clinic.Api.Endpoints;
using Clinic.Api.Scheduling;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database: SQLite by default (zero setup), PostgreSQL in Docker/production.
var connection = builder.Configuration.GetConnectionString("Clinic") ?? "Data Source=clinic.db";
var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
builder.Services.AddDbContext<ClinicDb>(options =>
{
    if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase)) options.UseNpgsql(connection);
    else options.UseSqlite(connection);
});

// Time: all rules use the clinic's local time zone, through TimeProvider so tests can freeze it.
var zone = TimeZoneInfo.FindSystemTimeZoneById(builder.Configuration["Clinic:TimeZone"] ?? "America/Argentina/Buenos_Aires");
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp => new ClinicClock(sp.GetRequiredService<TimeProvider>(), zone));
builder.Services.AddScoped<SchedulingService>();

builder.Services.AddValidation(); // DataAnnotations on request records, answered as 400 ProblemDetails
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<SchedulingExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UseExceptionHandler(); // business-rule errors → ProblemDetails (SchedulingExceptionHandler)

app.MapOpenApi(); // OpenAPI document at /openapi/v1.json
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapClinicEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ClinicDb>();
    db.Database.EnsureCreated(); // a real deployment would run EF Core migrations instead
    if (app.Configuration.GetValue("Clinic:DemoData", true)) DemoData.Seed(db);
}

app.Run();

public partial class Program; // lets the test project start the app with WebApplicationFactory
