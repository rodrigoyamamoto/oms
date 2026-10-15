using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Oms.Orders.Api;
using Oms.Orders.Api.Contracts;
using Oms.Orders.Domain;
using Oms.Orders.Domain.Errors;
using Oms.Orders.IntegrationTests.Persistence;
using Xunit;

namespace Oms.Orders.IntegrationTests.Api;

public sealed class OrdersHttpApiIntegrationTests : IClassFixture<OrdersPostgreSqlFixture>
{
    private const int UuidVersion7 = 7;

    private readonly OrdersPostgreSqlFixture _fixture;

    public OrdersHttpApiIntegrationTests(OrdersPostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PostAndGetOrderWhenRequestIsValidShouldReturnCreatedWithLocationAndRetrievePersistedOrder()
    {
        await using var factory = CreateHostFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var firstProductId = Guid.CreateVersion7();
        var secondProductId = Guid.CreateVersion7();

        var createRequest = new CreateOrderRequest(
        [
            new CreateOrderItemRequest(firstProductId, 2, 10.125m, "gbp"),
            new CreateOrderItemRequest(secondProductId, 3, 0.625m, "GBP"),
        ]);

        using var postResponse = await client.PostAsJsonAsync("/api/orders", createRequest);

        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
        Assert.NotNull(postResponse.Headers.Location);

        var createdPayload = await postResponse.Content.ReadFromJsonAsync<CreateOrderResponse>();
        Assert.NotNull(createdPayload);
        Assert.NotEqual(Guid.Empty, createdPayload.OrderId);
        Assert.Equal(UuidVersion7, createdPayload.OrderId.Version);
        Assert.EndsWith($"/api/orders/{createdPayload.OrderId}", postResponse.Headers.Location.ToString(), StringComparison.OrdinalIgnoreCase);

        await using (var verifyContext = _fixture.CreateDbContext())
        {
            var persistedOrder = await verifyContext.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .SingleAsync(o => o.Id == createdPayload.OrderId);

            Assert.Equal(OrderStatus.Pending, persistedOrder.Status);
            Assert.Equal(2, persistedOrder.Items.Count);
            Assert.Equal(Money.Create(22.125m, "GBP").Value, persistedOrder.Total);
        }

        using var getResponse = await client.GetAsync(postResponse.Headers.Location);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var orderResponse = await getResponse.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(orderResponse);
        Assert.Equal(createdPayload.OrderId, orderResponse.Id);
        Assert.Equal("Pending", orderResponse.Status);
        Assert.Equal(22.125m, orderResponse.TotalAmount);
        Assert.Equal("GBP", orderResponse.Currency);
        Assert.Equal(2, orderResponse.Items.Count);

        var firstItem = orderResponse.Items.Single(i => i.ProductId == firstProductId);
        Assert.NotEqual(Guid.Empty, firstItem.Id);
        Assert.Equal(UuidVersion7, firstItem.Id.Version);
        Assert.Equal(2, firstItem.Quantity);
        Assert.Equal(10.125m, firstItem.UnitPriceAmount);
        Assert.Equal("GBP", firstItem.Currency);
        Assert.Equal(20.250m, firstItem.LineTotalAmount);

        var secondItem = orderResponse.Items.Single(i => i.ProductId == secondProductId);
        Assert.NotEqual(Guid.Empty, secondItem.Id);
        Assert.Equal(UuidVersion7, secondItem.Id.Version);
        Assert.Equal(3, secondItem.Quantity);
        Assert.Equal(0.625m, secondItem.UnitPriceAmount);
        Assert.Equal("GBP", secondItem.Currency);
        Assert.Equal(1.875m, secondItem.LineTotalAmount);
    }

    [Fact]
    public async Task PostOrderWhenBusinessValidationFailsShouldReturnBadRequestWithProblemDetailsCodeAndPersistNothing()
    {
        await using var factory = CreateHostFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        await using var beforeContext = _fixture.CreateDbContext();
        var initialCount = await beforeContext.Orders.CountAsync();

        var duplicateProductId = Guid.CreateVersion7();

        var invalidRequests = new (CreateOrderRequest Request, string ExpectedCode)[]
        {
            (new CreateOrderRequest(null), OrderErrors.EmptyItems.Code),
            (new CreateOrderRequest([]), OrderErrors.EmptyItems.Code),
            (new CreateOrderRequest([new CreateOrderItemRequest(Guid.Empty, 1, 10.00m, "GBP")]), OrderErrors.InvalidProductId.Code),
            (new CreateOrderRequest([new CreateOrderItemRequest(Guid.CreateVersion7(), 0, 10.00m, "GBP")]), OrderErrors.InvalidQuantity.Code),
            (new CreateOrderRequest([new CreateOrderItemRequest(Guid.CreateVersion7(), 1, 10.00m, "INVALID")]), MoneyErrors.InvalidCurrency.Code),
            (new CreateOrderRequest([new CreateOrderItemRequest(Guid.CreateVersion7(), 1, -5.00m, "GBP")]), MoneyErrors.NegativeAmount.Code),
            (new CreateOrderRequest([new CreateOrderItemRequest(Guid.CreateVersion7(), 1, 0.00m, "GBP")]), OrderErrors.InvalidUnitPrice.Code),
            (
                new CreateOrderRequest(
                [
                    new CreateOrderItemRequest(duplicateProductId, 1, 10.00m, "GBP"),
                    new CreateOrderItemRequest(duplicateProductId, 2, 10.00m, "GBP"),
                ]),
                "Orders.DuplicateProduct"),
            (
                new CreateOrderRequest(
                [
                    new CreateOrderItemRequest(Guid.CreateVersion7(), 1, 10.00m, "GBP"),
                    new CreateOrderItemRequest(Guid.CreateVersion7(), 1, 10.00m, "EUR"),
                ]),
                "Orders.CurrencyMismatch"),
        };

        foreach (var (request, expectedCode) in invalidRequests)
        {
            using var response = await client.PostAsJsonAsync("/api/orders", request);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            Assert.Equal(400, document.RootElement.GetProperty("status").GetInt32());
            Assert.Equal(expectedCode, document.RootElement.GetProperty("code").GetString());
        }

        await using var afterContext = _fixture.CreateDbContext();
        var finalCount = await afterContext.Orders.CountAsync();

        Assert.Equal(initialCount, finalCount);
    }

    [Fact]
    public async Task GetOrderByIdWhenOrderDoesNotExistShouldReturnNotFoundWithOrdersNotFoundCode()
    {
        await using var factory = CreateHostFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        var unknownId = Guid.CreateVersion7();

        using var response = await client.GetAsync(new Uri($"/api/orders/{unknownId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(404, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Orders.NotFound", document.RootElement.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("not-a-valid-guid")]
    [InlineData("12345")]
    public async Task GetOrderByIdWhenIdentifierIsEmptyOrMalformedShouldReturnBadRequestWithInvalidOrderIdCode(string invalidId)
    {
        await using var factory = CreateHostFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri($"/api/orders/{invalidId}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        Assert.Equal(400, document.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Orders.InvalidOrderId", document.RootElement.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("{ malformed json ")]
    [InlineData("")]
    [InlineData("{\"items\": [{\"productId\": \"not-a-guid\", \"quantity\": 1, \"unitPriceAmount\": 10.0, \"currency\": \"GBP\"}]}")]
    public async Task PostOrderWhenBodyIsMissingOrMalformedJsonShouldReturnBadRequestAndNotInternalServerError(string rawJsonBody)
    {
        await using var factory = CreateHostFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var content = new StringContent(rawJsonBody, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(new Uri("/api/orders", UriKind.Relative), content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WhenDatabaseIsUnavailableHealthLiveShouldRemainOkWhileOrdersEndpointsReturnServerErrorWithoutLeakingConnectionDetails()
    {
        const string UnreachableConnectionString = "Host=127.0.0.1;Port=1;Database=orders_db;Username=postgres;Password=secret_test_password_do_not_leak;Timeout=1";
        await using var factory = CreateHostFactory(UnreachableConnectionString, environment: "Production");
        using var client = factory.CreateClient();

        using var liveResponse = await client.GetAsync(new Uri("/health/live", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);

        var validRequest = new CreateOrderRequest(
        [
            new CreateOrderItemRequest(Guid.CreateVersion7(), 1, 10.00m, "GBP"),
        ]);

        using var postResponse = await client.PostAsJsonAsync("/api/orders", validRequest);
        var postBody = await postResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, postResponse.StatusCode);
        Assert.DoesNotContain("secret_test_password_do_not_leak", postBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("NpgsqlException", postBody, StringComparison.OrdinalIgnoreCase);
    }

    private static WebApplicationFactory<Program> CreateHostFactory(
        string connectionString,
        string environment = "Development") =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environment);
                builder.ConfigureAppConfiguration((_, configBuilder) =>
                    configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:OrdersDatabase"] = connectionString,
                    }));
            });
}
