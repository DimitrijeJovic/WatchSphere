using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WatchSphere.API.Data;
using WatchSphere.API.Models;

namespace WatchSphere.Tests.Infrastructure;

/// <summary>
/// A private in-memory SQLite database. SQLite is relational, so unique indexes and
/// foreign keys from AppDbContext are enforced, unlike EF Core's InMemory provider.
/// The database lives as long as the open connection.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    public SqliteConnection Connection { get; }

    /// <param name="createSchema">False when the API's own startup will create the schema.</param>
    public TestDatabase(bool createSchema = true)
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();
        if (createSchema)
        {
            using var context = CreateContext();
            context.Database.EnsureCreated();
        }
    }

    public AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(Connection).Options);

    public User AddUser(string username, string role = "User", string password = "Correct-Horse-1")
    {
        using var context = CreateContext();
        var user = new User
        {
            Username = username,
            Email = $"{username}@example.test",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4),
            Role = role
        };
        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }

    public Media AddMedia(string title, string type = "Series", int? totalEpisodes = 12)
    {
        using var context = CreateContext();
        var media = new Media { Title = title, Type = type, TotalEpisodes = totalEpisodes, ReleaseYear = 2020 };
        context.Media.Add(media);
        context.SaveChanges();
        return media;
    }

    public void Dispose() => Connection.Dispose();
}
