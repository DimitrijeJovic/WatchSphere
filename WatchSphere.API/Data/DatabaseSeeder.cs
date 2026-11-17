using Microsoft.EntityFrameworkCore;
using WatchSphere.API.Models;

namespace WatchSphere.API.Data
{
    /// <summary>
    /// Brings a fresh database to a usable state: schema, the standard genre
    /// list, and one administrator so the dashboard is reachable.
    /// </summary>
    public static class DatabaseSeeder
    {
        private static readonly string[] DefaultGenres =
        {
            "Action", "Adventure", "Comedy", "Crime", "Drama", "Fantasy",
            "Horror", "Mecha", "Mystery", "Psychological", "Romance",
            "Sci-Fi", "Slice of Life", "Sports", "Supernatural", "Thriller"
        };

        public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
        {
            using var scope = services.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var logger = scope.ServiceProvider
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger(nameof(DatabaseSeeder));

            // The migrations contain MySQL-specific SQL. Other providers (the SQLite
            // database used by WatchSphere.Tests) get the schema straight from the model.
            if (context.Database.IsMySql())
            {
                await context.Database.MigrateAsync();
            }
            else
            {
                await context.Database.EnsureCreatedAsync();
            }

            var existing = await context.Genres
                .Select(g => g.Name)
                .ToListAsync();

            var missing = DefaultGenres
                .Except(existing, StringComparer.OrdinalIgnoreCase)
                .Select(name => new Genre { Name = name })
                .ToList();

            if (missing.Count > 0)
            {
                context.Genres.AddRange(missing);

                logger.LogInformation("Seeded {Count} genres.", missing.Count);
            }

            if (!await context.Users.AnyAsync(u => u.Role == "Admin"))
            {
                var email = (configuration["Seed:AdminEmail"] ?? "admin@watchsphere.local")
                    .ToLowerInvariant();

                var password = Environment.GetEnvironmentVariable("WATCHSPHERE_ADMIN_PASSWORD")
                               ?? configuration["Seed:AdminPassword"];

                if (string.IsNullOrWhiteSpace(password))
                {
                    logger.LogWarning(
                        "No administrator exists and no seed password is configured. " +
                        "Set WATCHSPHERE_ADMIN_PASSWORD and restart to create one.");
                }
                else if (!await context.Users.AnyAsync(u => u.Email == email))
                {
                    context.Users.Add(new User
                    {
                        Username = configuration["Seed:AdminUsername"] ?? "admin",
                        Email = email,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                        Role = "Admin"
                    });

                    logger.LogInformation("Created the initial administrator {Email}.", email);
                }
            }

            await context.SaveChangesAsync();
        }
    }
}
