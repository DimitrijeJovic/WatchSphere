using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using WatchSphere.Tests.Infrastructure;

namespace WatchSphere.Tests.Api;

// Each test class gets its own ApiFactory, so the "auth" rate limit (10 requests per minute)
// is not shared between classes. This class makes 7 requests to rate-limited endpoints.
public class AuthTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_returns_a_token_that_identifies_the_new_user()
    {
        var client = _factory.CreateClient();

        var register = await client.PostAsJsonAsync("/api/Auth/register",
            new { username = "newviewer", email = "NewViewer@Example.test", password = "long-enough-pw" });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        using var body = JsonDocument.Parse(await register.Content.ReadAsStringAsync());
        var token = body.RootElement.GetProperty("token").GetString();
        Assert.False(string.IsNullOrEmpty(token));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await client.GetAsync("/api/Auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Contains("newviewer", await me.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Register_stores_a_bcrypt_hash_and_a_normalised_email()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/Auth/register",
            new { username = "hashcheck", email = "  Hash.Check@Example.TEST ", password = "plain-text-pw-1" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var context = _factory.Database.CreateContext();
        var stored = context.Users.Single(u => u.Username == "hashcheck");
        Assert.Equal("hash.check@example.test", stored.Email);
        Assert.NotEqual("plain-text-pw-1", stored.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("plain-text-pw-1", stored.PasswordHash));
        Assert.Equal("User", stored.Role);
    }

    [Fact]
    public async Task Register_with_an_existing_email_returns_409()
    {
        _factory.Database.AddUser("taken");

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/Auth/register",
            new { username = "someoneelse", email = "taken@example.test", password = "long-enough-pw" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_rejects_a_password_shorter_than_8_characters()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/Auth/register",
            new { username = "shortpw", email = "shortpw@example.test", password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var context = _factory.Database.CreateContext();
        Assert.False(context.Users.Any(u => u.Username == "shortpw"));
    }

    [Fact]
    public async Task Login_gives_the_same_401_message_for_unknown_email_and_wrong_password()
    {
        _factory.Database.AddUser("loginuser", password: "Correct-Horse-1");
        var client = _factory.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync("/api/Auth/login",
            new { email = "loginuser@example.test", password = "not-the-password" });
        var unknownEmail = await client.PostAsJsonAsync("/api/Auth/login",
            new { email = "nobody@example.test", password = "not-the-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(await wrongPassword.Content.ReadAsStringAsync(), await unknownEmail.Content.ReadAsStringAsync());
    }
}
