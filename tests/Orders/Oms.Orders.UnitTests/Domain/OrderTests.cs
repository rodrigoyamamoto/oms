using Oms.Orders.Domain;
using Xunit;

namespace Oms.Orders.UnitTests.Domain;

public sealed class OrderTests
{
    private const int UuidVersion7 = 7;

    [Fact]
    public void CreateWhenValidItemsAreProvidedShouldInitialisePendingOrderWithUuidV7IdentifiersAndCalculatedTotal()
    {
        var firstProductId = Guid.CreateVersion7();
        var secondProductId = Guid.CreateVersion7();

        var order = Order.Create(
        [
            new OrderItemDraft(firstProductId, 2, new Money(12.50m, "gbp")),
            new OrderItemDraft(secondProductId, 3, new Money(5.25m, "GBP")),
        ]);

        Assert.NotEqual(Guid.Empty, order.Id);
        Assert.Equal(UuidVersion7, order.Id.Version);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(2, order.Items.Count);

        var firstItem = order.Items[0];
        var secondItem = order.Items[1];

        Assert.NotEqual(Guid.Empty, firstItem.Id);
        Assert.NotEqual(Guid.Empty, secondItem.Id);
        Assert.Equal(UuidVersion7, firstItem.Id.Version);
        Assert.Equal(UuidVersion7, secondItem.Id.Version);
        Assert.NotEqual(firstItem.Id, secondItem.Id);
        Assert.NotEqual(order.Id, firstItem.Id);
        Assert.NotEqual(order.Id, secondItem.Id);

        Assert.Equal(firstProductId, firstItem.ProductId);
        Assert.Equal(2, firstItem.Quantity);
        Assert.Equal(new Money(12.50m, "GBP"), firstItem.UnitPrice);
        Assert.Equal(new Money(25.00m, "GBP"), firstItem.LineTotal);

        Assert.Equal(secondProductId, secondItem.ProductId);
        Assert.Equal(3, secondItem.Quantity);
        Assert.Equal(new Money(5.25m, "GBP"), secondItem.UnitPrice);
        Assert.Equal(new Money(15.75m, "GBP"), secondItem.LineTotal);

        Assert.Equal(new Money(40.75m, "GBP"), order.Total);
    }

    [Fact]
    public void CreateWhenItemsArgumentIsNullShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Order.Create(null!));
    }

    [Fact]
    public void CreateWhenItemsCollectionIsEmptyShouldThrowArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(() => Order.Create([]));

        Assert.Equal("items", exception.ParamName);
    }

    [Fact]
    public void CreateWhenItemHasEmptyProductIdShouldThrowArgumentException()
    {
        var drafts = new[]
        {
            new OrderItemDraft(Guid.Empty, 1, new Money(10.00m, "GBP")),
        };

        var exception = Assert.Throws<ArgumentException>(() => Order.Create(drafts));

        Assert.Equal("productId", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void CreateWhenItemHasNonPositiveQuantityShouldThrowArgumentOutOfRangeException(int invalidQuantity)
    {
        var drafts = new[]
        {
            new OrderItemDraft(Guid.CreateVersion7(), invalidQuantity, new Money(10.00m, "GBP")),
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Order.Create(drafts));

        Assert.Equal("quantity", exception.ParamName);
    }

    [Fact]
    public void CreateWhenItemHasZeroUnitPriceShouldThrowArgumentOutOfRangeException()
    {
        var drafts = new[]
        {
            new OrderItemDraft(Guid.CreateVersion7(), 1, Money.Zero("GBP")),
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => Order.Create(drafts));

        Assert.Equal("unitPrice", exception.ParamName);
    }

    [Fact]
    public void CreateWhenItemHasDefaultUninitialisedUnitPriceShouldThrowArgumentException()
    {
        var drafts = new[]
        {
            new OrderItemDraft(Guid.CreateVersion7(), 1, default),
        };

        var exception = Assert.Throws<ArgumentException>(() => Order.Create(drafts));

        Assert.Equal("unitPrice", exception.ParamName);
    }

    [Fact]
    public void CreateWhenOrderContainsDuplicateProductIdsShouldThrowArgumentException()
    {
        var duplicateProductId = Guid.CreateVersion7();
        var drafts = new[]
        {
            new OrderItemDraft(duplicateProductId, 1, new Money(10.00m, "GBP")),
            new OrderItemDraft(duplicateProductId, 2, new Money(10.00m, "GBP")),
        };

        var exception = Assert.Throws<ArgumentException>(() => Order.Create(drafts));

        Assert.Equal("items", exception.ParamName);
    }

    [Fact]
    public void CreateWhenItemsHaveMismatchedCurrenciesShouldThrowArgumentException()
    {
        var drafts = new[]
        {
            new OrderItemDraft(Guid.CreateVersion7(), 1, new Money(10.00m, "GBP")),
            new OrderItemDraft(Guid.CreateVersion7(), 1, new Money(10.00m, "EUR")),
        };

        var exception = Assert.Throws<ArgumentException>(() => Order.Create(drafts));

        Assert.Equal("items", exception.ParamName);
    }

    [Fact]
    public void ItemsWhenCallerMutatesSourceListOrAttemptsCollectionCastMutationShouldRemainProtected()
    {
        var initialProductId = Guid.CreateVersion7();
        var sourceDrafts = new List<OrderItemDraft>
        {
            new(initialProductId, 2, new Money(15.00m, "GBP")),
        };

        var order = Order.Create(sourceDrafts);

        sourceDrafts.Clear();
        sourceDrafts.Add(new OrderItemDraft(Guid.CreateVersion7(), 10, new Money(99.00m, "GBP")));

        Assert.Single(order.Items);
        Assert.Equal(initialProductId, order.Items[0].ProductId);
        Assert.Equal(new Money(30.00m, "GBP"), order.Total);

        var mutableInterface = Assert.IsAssignableFrom<ICollection<OrderItem>>(order.Items);
        Assert.True(mutableInterface.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutableInterface.Clear());
    }

    [Fact]
    public void LifecycleWhenProgressingPendingToAwaitingPaymentToConfirmedShouldSucceed()
    {
        var order = CreateValidOrder();

        order.MarkStockReserved();
        Assert.Equal(OrderStatus.AwaitingPayment, order.Status);

        order.ConfirmPayment();
        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public void CancelWhenOrderIsPendingShouldTransitionToCancelled()
    {
        var order = CreateValidOrder();

        order.Cancel();

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void CancelWhenOrderIsAwaitingPaymentShouldTransitionToCancelled()
    {
        var order = CreateValidOrder();
        order.MarkStockReserved();

        order.Cancel();

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void ConfirmPaymentWhenOrderIsPendingShouldThrowInvalidOperationException()
    {
        var order = CreateValidOrder();

        Assert.Throws<InvalidOperationException>(() => order.ConfirmPayment());
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public void MarkStockReservedWhenOrderIsAlreadyAwaitingPaymentConfirmedOrCancelledShouldThrowInvalidOperationException()
    {
        var awaitingPaymentOrder = CreateValidOrder();
        awaitingPaymentOrder.MarkStockReserved();
        Assert.Throws<InvalidOperationException>(() => awaitingPaymentOrder.MarkStockReserved());

        var confirmedOrder = CreateValidOrder();
        confirmedOrder.MarkStockReserved();
        confirmedOrder.ConfirmPayment();
        Assert.Throws<InvalidOperationException>(() => confirmedOrder.MarkStockReserved());

        var cancelledOrder = CreateValidOrder();
        cancelledOrder.Cancel();
        Assert.Throws<InvalidOperationException>(() => cancelledOrder.MarkStockReserved());
    }

    [Fact]
    public void ConfirmPaymentWhenOrderIsAlreadyConfirmedShouldThrowInvalidOperationException()
    {
        var order = CreateValidOrder();
        order.MarkStockReserved();
        order.ConfirmPayment();

        Assert.Throws<InvalidOperationException>(() => order.ConfirmPayment());
        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public void ConfirmPaymentWhenOrderIsCancelledShouldThrowInvalidOperationException()
    {
        var order = CreateValidOrder();
        order.Cancel();

        Assert.Throws<InvalidOperationException>(() => order.ConfirmPayment());
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void CancelWhenOrderIsConfirmedOrAlreadyCancelledShouldThrowInvalidOperationException()
    {
        var confirmedOrder = CreateValidOrder();
        confirmedOrder.MarkStockReserved();
        confirmedOrder.ConfirmPayment();

        Assert.Throws<InvalidOperationException>(() => confirmedOrder.Cancel());
        Assert.Equal(OrderStatus.Confirmed, confirmedOrder.Status);

        var cancelledOrder = CreateValidOrder();
        cancelledOrder.Cancel();

        Assert.Throws<InvalidOperationException>(() => cancelledOrder.Cancel());
        Assert.Equal(OrderStatus.Cancelled, cancelledOrder.Status);
    }

    private static Order CreateValidOrder() =>
        Order.Create(
        [
            new OrderItemDraft(Guid.CreateVersion7(), 2, new Money(10.00m, "GBP")),
        ]);
}
