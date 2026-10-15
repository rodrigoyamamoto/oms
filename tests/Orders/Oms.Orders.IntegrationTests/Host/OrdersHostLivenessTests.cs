using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Oms.Orders.Api;
using Xunit;

namespace Oms.Orders.IntegrationTests.Host;

public sealed class OrdersHostLivenessTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OrdersHostLivenessTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

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
