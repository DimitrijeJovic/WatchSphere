using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using WatchSphere.Tests.Infrastructure;

namespace WatchSphere.Tests.Api;

public class AuthorizationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthorizationTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Protected_endpoint_without_a_token_returns_401()
    {
        var response = await _factory.CreateClient().GetAsync("/api/UserLibrary/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Regular_user_cannot_create_a_genre_but_an_admin_can()
    {
        var user = _factory.CreateClientFor(_factory.Database.AddUser("plainuser"));
        var admin = _factory.CreateClientFor(_factory.Database.AddUser("boss", role: "Admin"));

        var asUser = await user.PostAsJsonAsync("/api/Genre", new { name = "Documentary" });
        var asAdmin = await admin.PostAsJsonAsync("/api/Genre", new { name = "Documentary" });

        Assert.Equal(HttpStatusCode.Forbidden, asUser.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asAdmin.StatusCode);
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            CreateToken("a-different-key-that-is-also-long-enough-0123456789", DateTime.UtcNow.AddHours(1)));

        var response = await client.GetAsync("/api/Auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            CreateToken(ApiFactory.TestSigningKey, DateTime.UtcNow.AddMinutes(-1)));

        var response = await client.GetAsync("/api/Auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Only_the_author_can_edit_a_review_and_only_author_or_admin_can_delete_it()
    {
        var media = _factory.Database.AddMedia("Review Target");
        var author = _factory.CreateClientFor(_factory.Database.AddUser("author"));
        var stranger = _factory.CreateClientFor(_factory.Database.AddUser("stranger"));
        var admin = _factory.CreateClientFor(_factory.Database.AddUser("moderator", role: "Admin"));

        var created = await author.PostAsJsonAsync("/api/Review",
            new { mediaId = media.Id, rating = 8, reviewText = "Solid first season.", containsSpoilers = false });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var reviewId = body.RootElement.GetProperty("id").GetInt32();

        var edit = new { mediaId = media.Id, rating = 1, reviewText = "Edited by someone else.", containsSpoilers = false };
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.PutAsJsonAsync($"/api/Review/{reviewId}", edit)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PutAsJsonAsync($"/api/Review/{reviewId}", edit)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.DeleteAsync($"/api/Review/{reviewId}")).StatusCode);

        using (var context = _factory.Database.CreateContext())
        {
            Assert.Equal(8, context.Reviews.Single(r => r.Id == reviewId).Rating);
        }

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/Review/{reviewId}")).StatusCode);
    }

    [Fact]
    public async Task Library_entry_with_an_unknown_status_is_rejected_by_validation()
    {
        var media = _factory.Database.AddMedia("Validation Target");
        var client = _factory.CreateClientFor(_factory.Database.AddUser("validator"));

        var response = await client.PostAsJsonAsync("/api/UserLibrary", new { mediaId = media.Id, status = "Binging" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string CreateToken(string key, DateTime expires)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "WatchSphereAPI",
            audience: "WatchSphereUsers",
            claims: new[] { new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Role, "Admin") },
            notBefore: expires.AddHours(-2),
            expires: expires,
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
