using Microsoft.EntityFrameworkCore;
using Npgsql;
using Oms.Orders.Domain;
using Xunit;

namespace Oms.Orders.IntegrationTests.Persistence;

public sealed class OrdersPersistenceIntegrationTests : IClassFixture<OrdersPostgreSqlFixture>
{
    private const int UuidVersion7 = 7;

    private readonly OrdersPostgreSqlFixture _fixture;

    public OrdersPersistenceIntegrationTests(OrdersPostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MigrationWhenAppliedToCleanDatabaseShouldRecordInitialMigrationAndNoPendingModelChanges()
    {
        await using var context = _fixture.CreateDbContext();

        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync();
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();

        Assert.Contains(
            appliedMigrations,
            migration => migration.EndsWith("InitialOrdersPersistence", StringComparison.Ordinal));
        Assert.Empty(pendingMigrations);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task PersistAndReloadOrderAcrossSeparateDbContextsShouldPreserveUuidV7IdsItemsMoneyPrecisionAndCalculatedTotal()
    {
        var firstProductId = Guid.CreateVersion7();
        var secondProductId = Guid.CreateVersion7();

        var createResult = Order.Create(
        [
            new OrderItemDraft(firstProductId, 3, Money.Create(10.125m, "gbp").Value),
            new OrderItemDraft(secondProductId, 2, Money.Create(0.625m, "GBP").Value),
        ]);

        Assert.True(createResult.IsSuccess);
        var originalOrder = createResult.Value;
        var expectedOrderId = originalOrder.Id;
        var expectedFirstItemId = originalOrder.Items[0].Id;
        var expectedSecondItemId = originalOrder.Items[1].Id;

        await using (var writeContext = _fixture.CreateDbContext())
        {
            writeContext.Orders.Add(originalOrder);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = _fixture.CreateDbContext();
        var reloadedOrder = await readContext.Orders
            .Include(order => order.Items)
            .SingleAsync(order => order.Id == expectedOrderId);

        Assert.NotSame(originalOrder, reloadedOrder);
        Assert.Equal(expectedOrderId, reloadedOrder.Id);
        Assert.Equal(UuidVersion7, reloadedOrder.Id.Version);
        Assert.Equal(OrderStatus.Pending, reloadedOrder.Status);
        Assert.Equal(2, reloadedOrder.Items.Count);

        var reloadedFirstItem = reloadedOrder.Items.Single(item => item.ProductId == firstProductId);
        var reloadedSecondItem = reloadedOrder.Items.Single(item => item.ProductId == secondProductId);

        Assert.Equal(expectedFirstItemId, reloadedFirstItem.Id);
        Assert.Equal(UuidVersion7, reloadedFirstItem.Id.Version);
        Assert.Equal(3, reloadedFirstItem.Quantity);
        Assert.True(reloadedFirstItem.UnitPrice.IsInitialised);
        Assert.Equal(Money.Create(10.125m, "GBP").Value, reloadedFirstItem.UnitPrice);
        Assert.Equal(Money.Create(30.375m, "GBP").Value, reloadedFirstItem.LineTotal);

        Assert.Equal(expectedSecondItemId, reloadedSecondItem.Id);
        Assert.Equal(UuidVersion7, reloadedSecondItem.Id.Version);
        Assert.Equal(2, reloadedSecondItem.Quantity);
        Assert.True(reloadedSecondItem.UnitPrice.IsInitialised);
        Assert.Equal(Money.Create(0.625m, "GBP").Value, reloadedSecondItem.UnitPrice);
        Assert.Equal(Money.Create(1.250m, "GBP").Value, reloadedSecondItem.LineTotal);

        Assert.Equal(Money.Create(31.625m, "GBP").Value, reloadedOrder.Total);

        var mutableInterface = Assert.IsAssignableFrom<ICollection<OrderItem>>(reloadedOrder.Items);
        Assert.True(mutableInterface.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutableInterface.Clear());
    }

    [Fact]
    public async Task LifecycleTransitionsWhenPersistedAndReloadedShouldPersistAllOrderStatusValues()
    {
        var order = Order.Create(
        [
            new OrderItemDraft(Guid.CreateVersion7(), 1, Money.Create(25.00m, "EUR").Value),
        ]).Value;

        await using (var initialContext = _fixture.CreateDbContext())
        {
            initialContext.Orders.Add(order);
            await initialContext.SaveChangesAsync();
        }

        await using (var reserveContext = _fixture.CreateDbContext())
        {
            var pendingOrder = await reserveContext.Orders
                .Include(o => o.Items)
                .SingleAsync(o => o.Id == order.Id);

            var reserveResult = pendingOrder.MarkStockReserved();
            Assert.True(reserveResult.IsSuccess);
            await reserveContext.SaveChangesAsync();
        }

        await using (var confirmContext = _fixture.CreateDbContext())
        {
            var awaitingPaymentOrder = await confirmContext.Orders
                .Include(o => o.Items)
                .SingleAsync(o => o.Id == order.Id);

            Assert.Equal(OrderStatus.AwaitingPayment, awaitingPaymentOrder.Status);

            var confirmResult = awaitingPaymentOrder.ConfirmPayment();
            Assert.True(confirmResult.IsSuccess);
            await confirmContext.SaveChangesAsync();
        }

        await using (var verifyContext = _fixture.CreateDbContext())
        {
            var confirmedOrder = await verifyContext.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .SingleAsync(o => o.Id == order.Id);

            Assert.Equal(OrderStatus.Confirmed, confirmedOrder.Status);
        }

        var cancelledOrder = Order.Create(
        [
            new OrderItemDraft(Guid.CreateVersion7(), 1, Money.Create(12.00m, "EUR").Value),
        ]).Value;

        Assert.True(cancelledOrder.Cancel().IsSuccess);

        await using (var cancelWriteContext = _fixture.CreateDbContext())
        {
            cancelWriteContext.Orders.Add(cancelledOrder);
            await cancelWriteContext.SaveChangesAsync();
        }

        await using (var cancelVerifyContext = _fixture.CreateDbContext())
        {
            var reloadedCancelledOrder = await cancelVerifyContext.Orders
                .AsNoTracking()
                .SingleAsync(o => o.Id == cancelledOrder.Id);

            Assert.Equal(OrderStatus.Cancelled, reloadedCancelledOrder.Status);
        }
    }

    [Fact]
    public async Task OptimisticConcurrencyWhenTwoContextsUpdateSameOrderConcurrentlyShouldThrowDbUpdateConcurrencyExceptionAndPreserveFirstWrite()
    {
        var order = Order.Create(
        [
            new OrderItemDraft(Guid.CreateVersion7(), 2, Money.Create(15.00m, "GBP").Value),
        ]).Value;

        await using (var setupContext = _fixture.CreateDbContext())
        {
            setupContext.Orders.Add(order);
            await setupContext.SaveChangesAsync();
        }

        await using var contextA = _fixture.CreateDbContext();
        await using var contextB = _fixture.CreateDbContext();

        var orderInContextA = await contextA.Orders.SingleAsync(o => o.Id == order.Id);
        var orderInContextB = await contextB.Orders.SingleAsync(o => o.Id == order.Id);

        Assert.True(orderInContextA.MarkStockReserved().IsSuccess);
        Assert.True(orderInContextB.Cancel().IsSuccess);

        await contextA.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextB.SaveChangesAsync());

        await using var verificationContext = _fixture.CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .AsNoTracking()
            .SingleAsync(o => o.Id == order.Id);

        Assert.Equal(OrderStatus.AwaitingPayment, persistedOrder.Status);
    }

    [Fact]
    public async Task DatabaseConstraintsWhenInvalidOrderItemRowsAreInsertedDirectlyShouldRejectViolations()
    {
        var order = Order.Create(
        [
            new OrderItemDraft(Guid.CreateVersion7(), 1, Money.Create(10.00m, "GBP").Value),
        ]).Value;

        var existingProductId = order.Items[0].ProductId;

        await using var context = _fixture.CreateDbContext();
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var duplicateProductException = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO order_items (id, order_id, product_id, quantity, unit_price_amount, unit_price_currency)
                VALUES ({Guid.CreateVersion7()}, {order.Id}, {existingProductId}, 1, 10.00, 'GBP');
                """));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicateProductException.SqlState);

        var nonPositiveQuantityException = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO order_items (id, order_id, product_id, quantity, unit_price_amount, unit_price_currency)
                VALUES ({Guid.CreateVersion7()}, {order.Id}, {Guid.CreateVersion7()}, 0, 10.00, 'GBP');
                """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, nonPositiveQuantityException.SqlState);

        var nonPositivePriceException = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO order_items (id, order_id, product_id, quantity, unit_price_amount, unit_price_currency)
                VALUES ({Guid.CreateVersion7()}, {order.Id}, {Guid.CreateVersion7()}, 1, 0.00, 'GBP');
                """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, nonPositivePriceException.SqlState);

        var invalidCurrencyLengthException = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO order_items (id, order_id, product_id, quantity, unit_price_amount, unit_price_currency)
                VALUES ({Guid.CreateVersion7()}, {order.Id}, {Guid.CreateVersion7()}, 1, 10.00, 'GB');
                """));
        Assert.Equal(PostgresErrorCodes.CheckViolation, invalidCurrencyLengthException.SqlState);
    }

    [Fact]
    public async Task TransactionalAtomicityWhenOrderSaveFailsOnConstraintShouldNotLeavePartialOrderPersisted()
    {
        var existingOrder = Order.Create(
        [
            new OrderItemDraft(Guid.CreateVersion7(), 1, Money.Create(10.00m, "GBP").Value),
        ]).Value;

        await using (var setupContext = _fixture.CreateDbContext())
        {
            setupContext.Orders.Add(existingOrder);
            await setupContext.SaveChangesAsync();
        }

        var collidingOrder = Order.Create(
        [
            new OrderItemDraft(Guid.CreateVersion7(), 2, Money.Create(20.00m, "GBP").Value),
        ]).Value;
        var collidingItemId = collidingOrder.Items[0].Id;

        await using (var failingContext = _fixture.CreateDbContext())
        {
            failingContext.Orders.Add(collidingOrder);
            failingContext.Entry(collidingOrder.Items[0]).Property("Id").CurrentValue = existingOrder.Items[0].Id;

            await Assert.ThrowsAsync<DbUpdateException>(() => failingContext.SaveChangesAsync());
        }

        await using var verifyContext = _fixture.CreateDbContext();
        var persistedOrderExists = await verifyContext.Orders.AnyAsync(o => o.Id == collidingOrder.Id);
        var persistedItemExists = await verifyContext.OrderItems.AnyAsync(i => i.Id == collidingItemId);

        Assert.False(persistedOrderExists);
        Assert.False(persistedItemExists);
    }
}
