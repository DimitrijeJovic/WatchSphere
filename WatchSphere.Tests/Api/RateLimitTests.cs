using System.Net;
using System.Net.Http.Json;
using WatchSphere.Tests.Infrastructure;

namespace WatchSphere.Tests.Api;

public class RateLimitTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public RateLimitTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Eleventh_login_attempt_within_a_minute_is_rejected_with_429()
    {
        var client = _factory.CreateClient();
        var attempt = new { email = "nobody@example.test", password = "wrong-password" };

        for (var i = 1; i <= 10; i++)
        {
            var allowed = await client.PostAsJsonAsync("/api/Auth/login", attempt);
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        var blocked = await client.PostAsJsonAsync("/api/Auth/login", attempt);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }
}
