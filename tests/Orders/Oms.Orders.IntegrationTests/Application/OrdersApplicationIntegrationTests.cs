using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Oms.Orders.Api;
using Oms.Orders.Application.Errors;
using Oms.Orders.Application.Orders.CreateOrder;
using Oms.Orders.Application.Orders.GetOrderById;
using Oms.Orders.Domain;
using Oms.Orders.Domain.Errors;
using Oms.Orders.IntegrationTests.Persistence;
using Xunit;

namespace Oms.Orders.IntegrationTests.Application;

public sealed class OrdersApplicationIntegrationTests : IClassFixture<OrdersPostgreSqlFixture>
{
    private const int UuidVersion7 = 7;

    private readonly OrdersPostgreSqlFixture _fixture;

    public OrdersApplicationIntegrationTests(OrdersPostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CreateOrderCommandWhenDispatchedThroughMediatRShouldPersistOrderAndItemsInPostgreSql()
    {
        await using var hostFactory = CreateHostFactory(_fixture.ConnectionString);
        using var scope = hostFactory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var firstProductId = Guid.CreateVersion7();
        var secondProductId = Guid.CreateVersion7();

        var command = new CreateOrderCommand(
        [
            new CreateOrderItemInput(firstProductId, 3, 10.125m, "gbp"),
            new CreateOrderItemInput(secondProductId, 2, 0.625m, "GBP"),
        ]);

        var createResult = await sender.Send(command);

        Assert.True(createResult.IsSuccess);
        var orderId = createResult.Value;
        Assert.NotEqual(Guid.Empty, orderId);
        Assert.Equal(UuidVersion7, orderId.Version);

        await using var verifyContext = _fixture.CreateDbContext();
        var persistedOrder = await verifyContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .SingleAsync(order => order.Id == orderId);

        Assert.Equal(OrderStatus.Pending, persistedOrder.Status);
        Assert.Equal(2, persistedOrder.Items.Count);
        Assert.Equal(Money.Create(31.625m, "GBP").Value, persistedOrder.Total);

        var firstItem = persistedOrder.Items.Single(item => item.ProductId == firstProductId);
        Assert.Equal(UuidVersion7, firstItem.Id.Version);
        Assert.Equal(3, firstItem.Quantity);
        Assert.Equal(Money.Create(10.125m, "GBP").Value, firstItem.UnitPrice);

        var secondItem = persistedOrder.Items.Single(item => item.ProductId == secondProductId);
        Assert.Equal(UuidVersion7, secondItem.Id.Version);
        Assert.Equal(2, secondItem.Quantity);
        Assert.Equal(Money.Create(0.625m, "GBP").Value, secondItem.UnitPrice);
    }

    [Fact]
    public async Task CreateOrderCommandWhenInputsAreInvalidShouldReturnExpectedErrorsAndPersistNothing()
    {
        await using var hostFactory = CreateHostFactory(_fixture.ConnectionString);
        using var scope = hostFactory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await using var countBeforeContext = _fixture.CreateDbContext();
        var initialOrderCount = await countBeforeContext.Orders.CountAsync();

        var duplicateProductId = Guid.CreateVersion7();

        var invalidCases = new (CreateOrderCommand Command, string ExpectedErrorCode)[]
        {
            (new CreateOrderCommand(null!), OrderErrors.EmptyItems.Code),
            (new CreateOrderCommand([]), OrderErrors.EmptyItems.Code),
            (new CreateOrderCommand([new CreateOrderItemInput(Guid.Empty, 1, 10.00m, "GBP")]), OrderErrors.InvalidProductId.Code),
            (new CreateOrderCommand([new CreateOrderItemInput(Guid.CreateVersion7(), 0, 10.00m, "GBP")]), OrderErrors.InvalidQuantity.Code),
            (new CreateOrderCommand([new CreateOrderItemInput(Guid.CreateVersion7(), 1, 10.00m, "INVALID")]), MoneyErrors.InvalidCurrency.Code),
            (new CreateOrderCommand([new CreateOrderItemInput(Guid.CreateVersion7(), 1, -5.00m, "GBP")]), MoneyErrors.NegativeAmount.Code),
            (new CreateOrderCommand([new CreateOrderItemInput(Guid.CreateVersion7(), 1, 0.00m, "GBP")]), OrderErrors.InvalidUnitPrice.Code),
            (
                new CreateOrderCommand(
                [
                    new CreateOrderItemInput(duplicateProductId, 1, 10.00m, "GBP"),
                    new CreateOrderItemInput(duplicateProductId, 2, 10.00m, "GBP"),
                ]),
                "Orders.DuplicateProduct"),
            (
                new CreateOrderCommand(
                [
                    new CreateOrderItemInput(Guid.CreateVersion7(), 1, 10.00m, "GBP"),
                    new CreateOrderItemInput(Guid.CreateVersion7(), 1, 10.00m, "EUR"),
                ]),
                "Orders.CurrencyMismatch"),
        };

        foreach (var (command, expectedErrorCode) in invalidCases)
        {
            var result = await sender.Send(command);

            Assert.True(result.IsFailure);
            Assert.Equal(expectedErrorCode, result.Error.Code);
        }

        await using var countAfterContext = _fixture.CreateDbContext();
        var finalOrderCount = await countAfterContext.Orders.CountAsync();

        Assert.Equal(initialOrderCount, finalOrderCount);
    }

    [Fact]
    public async Task GetOrderByIdQueryWhenOrderExistsShouldReturnImmutableOrderDetailsReadModel()
    {
        await using var hostFactory = CreateHostFactory(_fixture.ConnectionString);

        var firstProductId = Guid.CreateVersion7();
        var secondProductId = Guid.CreateVersion7();
        Guid createdOrderId;

        using (var createScope = hostFactory.Services.CreateScope())
        {
            var sender = createScope.ServiceProvider.GetRequiredService<ISender>();
            var createResult = await sender.Send(new CreateOrderCommand(
            [
                new CreateOrderItemInput(firstProductId, 2, 15.25m, "eur"),
                new CreateOrderItemInput(secondProductId, 4, 2.50m, "EUR"),
            ]));

            Assert.True(createResult.IsSuccess);
            createdOrderId = createResult.Value;
        }

        using var queryScope = hostFactory.Services.CreateScope();
        var querySender = queryScope.ServiceProvider.GetRequiredService<ISender>();

        var queryResult = await querySender.Send(new GetOrderByIdQuery(createdOrderId));

        Assert.True(queryResult.IsSuccess);
        var details = queryResult.Value;

        Assert.Equal(createdOrderId, details.Id);
        Assert.Equal(OrderStatus.Pending, details.Status);
        Assert.Equal(40.50m, details.TotalAmount);
        Assert.Equal("EUR", details.Currency);
        Assert.Equal(2, details.Items.Count);

        var firstItem = details.Items.Single(item => item.ProductId == firstProductId);
        Assert.NotEqual(Guid.Empty, firstItem.Id);
        Assert.Equal(UuidVersion7, firstItem.Id.Version);
        Assert.Equal(2, firstItem.Quantity);
        Assert.Equal(15.25m, firstItem.UnitPriceAmount);
        Assert.Equal("EUR", firstItem.Currency);
        Assert.Equal(30.50m, firstItem.LineTotalAmount);

        var secondItem = details.Items.Single(item => item.ProductId == secondProductId);
        Assert.NotEqual(Guid.Empty, secondItem.Id);
        Assert.Equal(UuidVersion7, secondItem.Id.Version);
        Assert.Equal(4, secondItem.Quantity);
        Assert.Equal(2.50m, secondItem.UnitPriceAmount);
        Assert.Equal("EUR", secondItem.Currency);
        Assert.Equal(10.00m, secondItem.LineTotalAmount);

        var mutableItems = Assert.IsAssignableFrom<ICollection<OrderItemDetails>>(details.Items);
        Assert.True(mutableItems.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutableItems.Clear());
    }

    [Fact]
    public async Task GetOrderByIdQueryWhenOrderDoesNotExistShouldReturnNotFoundResultWithoutThrowing()
    {
        await using var hostFactory = CreateHostFactory(_fixture.ConnectionString);
        using var scope = hostFactory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var nonExistentOrderId = Guid.CreateVersion7();

        var result = await sender.Send(new GetOrderByIdQuery(nonExistentOrderId));

        Assert.True(result.IsFailure);
        Assert.Equal("Orders.NotFound", result.Error.Code);
        Assert.Contains(nonExistentOrderId.ToString(), result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetOrderByIdQueryWhenOrderIdIsEmptyShouldReturnInvalidOrderIdError()
    {
        await using var hostFactory = CreateHostFactory(_fixture.ConnectionString);
        using var scope = hostFactory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var result = await sender.Send(new GetOrderByIdQuery(Guid.Empty));

        Assert.True(result.IsFailure);
        Assert.Equal(OrderApplicationErrors.InvalidOrderId.Code, result.Error.Code);
    }

    [Fact]
    public async Task PersistenceFailureWhenDatabaseIsUnreachableShouldPropagateExceptionAndNeverReportSuccess()
    {
        const string UnreachableConnectionString = "Host=127.0.0.1;Port=1;Database=orders_db;Username=postgres;Password=postgres;Timeout=1";
        await using var hostFactory = CreateHostFactory(UnreachableConnectionString);
        using var scope = hostFactory.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var command = new CreateOrderCommand(
        [
            new CreateOrderItemInput(Guid.CreateVersion7(), 1, 10.00m, "GBP"),
        ]);

        var createException = await Assert.ThrowsAsync<InvalidOperationException>(() => sender.Send(command));
        Assert.IsAssignableFrom<NpgsqlException>(createException.InnerException);

        var queryException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.Send(new GetOrderByIdQuery(Guid.CreateVersion7())));
        Assert.IsAssignableFrom<NpgsqlException>(queryException.InnerException);

        using var cancelledCts = new CancellationTokenSource();
        await cancelledCts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sender.Send(command, cancelledCts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sender.Send(new GetOrderByIdQuery(Guid.CreateVersion7()), cancelledCts.Token));
    }

    private static WebApplicationFactory<Program> CreateHostFactory(string connectionString) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureAppConfiguration((_, configBuilder) =>
                    configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:OrdersDatabase"] = connectionString,
                    })));
}
