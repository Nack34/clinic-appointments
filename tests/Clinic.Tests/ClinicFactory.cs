using Clinic.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace Clinic.Tests;

/// <summary>
/// Runs the real app in memory with a private SQLite database and a frozen clock:
/// Monday 23 Nov 2026, 08:00 in Buenos Aires (11:00 UTC).
/// </summary>
public class ClinicFactory : WebApplicationFactory<Program>
{
    public static readonly DateTimeOffset Start = new(2026, 11, 23, 11, 0, 0, TimeSpan.Zero);
    public FakeTimeProvider Time { get; } = new(Start);
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open(); // the in-memory database lives as long as this connection
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ClinicDb>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ClinicDb>>();
            services.AddDbContext<ClinicDb>(o => o.UseSqlite(_connection));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }
}
