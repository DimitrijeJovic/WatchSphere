using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WatchSphere.API.Data;
using WatchSphere.API.Models;
using WatchSphere.API.Services;

namespace WatchSphere.Tests.Infrastructure;

/// <summary>
/// Hosts the real API in memory against a SQLite database.
/// Runs as the "Testing" environment, so appsettings.Development.json (which may hold
/// real local credentials) is never loaded, and the background catalogue sync never starts.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string TestSigningKey = "test-only-signing-key-0123456789-abcdefghijklmnop";

    public TestDatabase Database { get; } = new(createSchema: false);

    public ApiFactory()
    {
        // JwtSettings reads this variable before configuration, so it must be set before the host is built.
        Environment.SetEnvironmentVariable("WATCHSPHERE_JWT_KEY", TestSigningKey);
        // Keeps a developer's real admin seed password (if set on this machine) out of test runs.
        Environment.SetEnvironmentVariable("WATCHSPHERE_ADMIN_PASSWORD", null);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll(typeof(DbContextOptions<AppDbContext>));
            services.RemoveAll(typeof(DbContextOptions));
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<AppDbContext>));
            // The migrations were generated for MySQL; on SQLite, EF Core reports the provider
            // difference as pending model changes, which is expected here.
            services.AddDbContext<AppDbContext>(options => options
                .UseSqlite(Database.Connection)
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

            var syncService = services.Where(d => d.ImplementationType == typeof(CatalogSyncService)).ToList();
            foreach (var descriptor in syncService)
            {
                services.Remove(descriptor);
            }
        });
    }

    public HttpClient CreateClientFor(User user)
    {
        var client = CreateClient();
        using var scope = Services.CreateScope();
        var (token, _) = scope.ServiceProvider.GetRequiredService<JwtService>().GenerateToken(user);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            Database.Dispose();
        }
    }
}

internal static class ServiceCollectionTestExtensions
{
    public static void RemoveAll(this IServiceCollection services, Type serviceType)
    {
        foreach (var descriptor in services.Where(d => d.ServiceType == serviceType).ToList())
        {
            services.Remove(descriptor);
        }
    }
}
