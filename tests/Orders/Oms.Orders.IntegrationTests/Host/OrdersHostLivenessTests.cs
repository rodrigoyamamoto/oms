using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Oms.Orders.Api;
using Xunit;

namespace Oms.Orders.IntegrationTests.Host;

/// <summary>
/// Verifies that the Orders ASP.NET Core host starts and responds on its liveness probe endpoint.
/// </summary>
public sealed class OrdersHostLivenessTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    /// <summary>
    /// Initialises a new instance of the <see cref="OrdersHostLivenessTests"/> class.
    /// </summary>
    /// <param name="factory">The in-memory web application factory for the Orders API host.</param>
    public OrdersHostLivenessTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Verifies that <c>GET /health/live</c> exercises the real ASP.NET Core pipeline and returns HTTP 200 OK with <c>Healthy</c>.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous test execution.</returns>
    [Fact]
    public async Task GetHealthLiveWhenHostIsRunningShouldReturnOkAndHealthyPayload()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", content);
    }
}
