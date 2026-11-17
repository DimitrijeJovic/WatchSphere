using System.Net;
using WatchSphere.Tests.Infrastructure;

namespace WatchSphere.Tests.Api;

public class SmokeTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public SmokeTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_endpoint_responds()
    {
        var response = await _factory.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
