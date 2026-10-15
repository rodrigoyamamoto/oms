using Oms.Orders.Domain;
using Oms.Orders.Domain.Errors;
using Xunit;

namespace Oms.Orders.UnitTests.Domain;

public sealed class OrderTests
{
    private const int UuidVersion7 = 7;

    [Fact]
    public void CreateWhenValidItemsAreProvidedShouldSucceedAndInitialisePendingOrderWithUuidV7IdentifiersAndCalculatedTotal()
    {
        var firstProductId = Guid.CreateVersion7();
        var secondProductId = Guid.CreateVersion7();

        var result = Order.Create(
        [
            new OrderItemDraft(firstProductId, 2, Money.Create(12.50m, "gbp").Value),
            new OrderItemDraft(secondProductId, 3, Money.Create(5.25m, "GBP").Value),
        ]);

        Assert.True(result.IsSuccess);
        var order = result.Value;

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
        Assert.Equal(Money.Create(12.50m, "GBP").Value, firstItem.UnitPrice);
        Assert.Equal(Money.Create(25.00m, "GBP").Value, firstItem.LineTotal);

        Assert.Equal(secondProductId, secondItem.ProductId);
        Assert.Equal(3, secondItem.Quantity);
        Assert.Equal(Money.Create(5.25m, "GBP").Value, secondItem.UnitPrice);
        Assert.Equal(Money.Create(15.75m, "GBP").Value, secondItem.LineTotal);

        Assert.Equal(Money.Create(40.75m, "GBP").Value, order.Total);
    }

    [Fact]
    public void CreateWhenItemsArgumentIsNullShouldThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Order.Create(null!));
    }

    [Fact]
    public void CreateWhenItemsCollectionIsEmptyShouldReturnEmptyItemsError()
    {
        var result = Order.Create([]);

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.EmptyItems.Code, result.Error.Code);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void CreateWhenItemHasEmptyProductIdShouldReturnInvalidProductIdError()
    {
        var result = Order.Create(
        [
            new OrderItemDraft(Guid.Empty, 1, Money.Create(10.00m, "GBP").Value),
        ]);

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidProductId.Code, result.Error.Code);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void CreateWhenItemHasNonPositiveQuantityShouldReturnInvalidQuantityError(int invalidQuantity)
    {
        var result = Order.Create(
        [
            new OrderItemDraft(Guid.CreateVersion7(), invalidQuantity, Money.Create(10.00m, "GBP").Value),
        ]);

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidQuantity.Code, result.Error.Code);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void CreateWhenItemHasZeroUnitPriceShouldReturnInvalidUnitPriceError()
    {
        var result = Order.Create(
        [
            new OrderItemDraft(Guid.CreateVersion7(), 1, Money.Zero("GBP")),
        ]);

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidUnitPrice.Code, result.Error.Code);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void CreateWhenItemHasDefaultUninitialisedUnitPriceShouldReturnInvalidUnitPriceError()
    {
        var result = Order.Create(
        [
            new OrderItemDraft(Guid.CreateVersion7(), 1, default),
        ]);

        Assert.True(result.IsFailure);
        Assert.Equal(OrderErrors.InvalidUnitPrice.Code, result.Error.Code);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void CreateWhenOrderContainsDuplicateProductIdsShouldReturnDuplicateProductError()
    {
        var duplicateProductId = Guid.CreateVersion7();

        var result = Order.Create(
        [
            new OrderItemDraft(duplicateProductId, 1, Money.Create(10.00m, "GBP").Value),
            new OrderItemDraft(duplicateProductId, 2, Money.Create(10.00m, "GBP").Value),
        ]);

        Assert.True(result.IsFailure);
        Assert.Equal("Orders.DuplicateProduct", result.Error.Code);
        Assert.Contains(duplicateProductId.ToString(), result.Error.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void CreateWhenItemsHaveMismatchedCurrenciesShouldReturnCurrencyMismatchError()
    {
        var result = Order.Create(
        [
            new OrderItemDraft(Guid.CreateVersion7(), 1, Money.Create(10.00m, "GBP").Value),
            new OrderItemDraft(Guid.CreateVersion7(), 1, Money.Create(10.00m, "EUR").Value),
        ]);

        Assert.True(result.IsFailure);
        Assert.Equal("Orders.CurrencyMismatch", result.Error.Code);
        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void ItemsWhenCallerMutatesSourceListOrAttemptsCollectionCastMutationShouldRemainProtected()
    {
        var initialProductId = Guid.CreateVersion7();
        var sourceDrafts = new List<OrderItemDraft>
        {
            new(initialProductId, 2, Money.Create(15.00m, "GBP").Value),
        };

        var order = Order.Create(sourceDrafts).Value;

        sourceDrafts.Clear();
        sourceDrafts.Add(new OrderItemDraft(Guid.CreateVersion7(), 10, Money.Create(99.00m, "GBP").Value));

        Assert.Single(order.Items);
        Assert.Equal(initialProductId, order.Items[0].ProductId);
        Assert.Equal(Money.Create(30.00m, "GBP").Value, order.Total);

        var mutableInterface = Assert.IsAssignableFrom<ICollection<OrderItem>>(order.Items);
        Assert.True(mutableInterface.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutableInterface.Clear());
    }

    [Fact]
    public void LifecycleWhenProgressingPendingToAwaitingPaymentToConfirmedShouldSucceed()
    {
        var order = CreateValidOrder();

        var stockReservedResult = order.MarkStockReserved();
        Assert.True(stockReservedResult.IsSuccess);
        Assert.Equal(OrderStatus.AwaitingPayment, order.Status);

        var paymentConfirmedResult = order.ConfirmPayment();
        Assert.True(paymentConfirmedResult.IsSuccess);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public void CancelWhenOrderIsPendingShouldSucceedAndTransitionToCancelled()
    {
        var order = CreateValidOrder();

        var result = order.Cancel();

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void CancelWhenOrderIsAwaitingPaymentShouldSucceedAndTransitionToCancelled()
    {
        var order = CreateValidOrder();
        order.MarkStockReserved();

        var result = order.Cancel();

        Assert.True(result.IsSuccess);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void ConfirmPaymentWhenOrderIsPendingShouldReturnInvalidTransitionAndLeaveStateUnchanged()
    {
        var order = CreateValidOrder();

        var result = order.ConfirmPayment();

        Assert.True(result.IsFailure);
        Assert.Equal("Orders.InvalidTransition", result.Error.Code);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public void MarkStockReservedWhenOrderIsNotPendingShouldReturnInvalidTransitionAndLeaveStateUnchanged()
    {
        var awaitingPaymentOrder = CreateValidOrder();
        awaitingPaymentOrder.MarkStockReserved();

        var fromAwaitingResult = awaitingPaymentOrder.MarkStockReserved();
        Assert.True(fromAwaitingResult.IsFailure);
        Assert.Equal("Orders.InvalidTransition", fromAwaitingResult.Error.Code);
        Assert.Equal(OrderStatus.AwaitingPayment, awaitingPaymentOrder.Status);

        var confirmedOrder = CreateValidOrder();
        confirmedOrder.MarkStockReserved();
        confirmedOrder.ConfirmPayment();

        var fromConfirmedResult = confirmedOrder.MarkStockReserved();
        Assert.True(fromConfirmedResult.IsFailure);
        Assert.Equal("Orders.InvalidTransition", fromConfirmedResult.Error.Code);
        Assert.Equal(OrderStatus.Confirmed, confirmedOrder.Status);

        var cancelledOrder = CreateValidOrder();
        cancelledOrder.Cancel();

        var fromCancelledResult = cancelledOrder.MarkStockReserved();
        Assert.True(fromCancelledResult.IsFailure);
        Assert.Equal("Orders.InvalidTransition", fromCancelledResult.Error.Code);
        Assert.Equal(OrderStatus.Cancelled, cancelledOrder.Status);
    }

    [Fact]
    public void ConfirmPaymentWhenOrderIsAlreadyConfirmedShouldReturnInvalidTransitionAndLeaveStateUnchanged()
    {
        var order = CreateValidOrder();
        order.MarkStockReserved();
        order.ConfirmPayment();

        var result = order.ConfirmPayment();

        Assert.True(result.IsFailure);
        Assert.Equal("Orders.InvalidTransition", result.Error.Code);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public void ConfirmPaymentWhenOrderIsCancelledShouldReturnInvalidTransitionAndLeaveStateUnchanged()
    {
        var order = CreateValidOrder();
        order.Cancel();

        var result = order.ConfirmPayment();

        Assert.True(result.IsFailure);
        Assert.Equal("Orders.InvalidTransition", result.Error.Code);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void CancelWhenOrderIsConfirmedOrAlreadyCancelledShouldReturnInvalidTransitionAndLeaveStateUnchanged()
    {
        var confirmedOrder = CreateValidOrder();
        confirmedOrder.MarkStockReserved();
        confirmedOrder.ConfirmPayment();

        var fromConfirmedResult = confirmedOrder.Cancel();
        Assert.True(fromConfirmedResult.IsFailure);
        Assert.Equal("Orders.InvalidTransition", fromConfirmedResult.Error.Code);
        Assert.Equal(OrderStatus.Confirmed, confirmedOrder.Status);

        var cancelledOrder = CreateValidOrder();
        cancelledOrder.Cancel();

        var fromCancelledResult = cancelledOrder.Cancel();
        Assert.True(fromCancelledResult.IsFailure);
        Assert.Equal("Orders.InvalidTransition", fromCancelledResult.Error.Code);
        Assert.Equal(OrderStatus.Cancelled, cancelledOrder.Status);
    }

    private static Order CreateValidOrder() =>
        Order.Create(
        [
            new OrderItemDraft(Guid.CreateVersion7(), 2, Money.Create(10.00m, "GBP").Value),
        ]).Value;
}
