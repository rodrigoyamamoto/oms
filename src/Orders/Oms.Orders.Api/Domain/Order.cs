using Oms.Orders.Domain.Common;
using Oms.Orders.Domain.Errors;

namespace Oms.Orders.Domain;

internal sealed class Order
{
    private readonly List<OrderItem> _items;

    private Order()
    {
        _items = [];
    }

    private Order(Guid id, List<OrderItem> items)
    {
        Id = id;
        _items = items;
        Status = OrderStatus.Pending;
    }

    public Guid Id { get; }

    public OrderStatus Status { get; private set; }

    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    public Money Total
    {
        get
        {
            var runningTotal = Money.Zero(_items[0].UnitPrice.Currency);
            foreach (var item in _items)
            {
                runningTotal = runningTotal.Add(item.LineTotal);
            }

            return runningTotal;
        }
    }

    public static Result<Order> Create(IEnumerable<OrderItemDraft> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var createdItems = new List<OrderItem>();
        var seenProductIds = new HashSet<Guid>();
        string? orderCurrency = null;

        foreach (var draft in items)
        {
            var itemResult = OrderItem.Create(draft.ProductId, draft.Quantity, draft.UnitPrice);
            if (itemResult.IsFailure)
            {
                return Result.Failure<Order>(itemResult.Error);
            }

            var orderItem = itemResult.Value;

            if (!seenProductIds.Add(orderItem.ProductId))
            {
                return Result.Failure<Order>(OrderErrors.DuplicateProduct(orderItem.ProductId));
            }

            if (orderCurrency is null)
            {
                orderCurrency = orderItem.UnitPrice.Currency;
            }
            else if (!string.Equals(orderCurrency, orderItem.UnitPrice.Currency, StringComparison.Ordinal))
            {
                return Result.Failure<Order>(OrderErrors.CurrencyMismatch(orderCurrency, orderItem.UnitPrice.Currency));
            }

            createdItems.Add(orderItem);
        }

        if (createdItems.Count == 0)
        {
            return Result.Failure<Order>(OrderErrors.EmptyItems);
        }

        return Result.Success(new Order(Guid.CreateVersion7(), createdItems));
    }

    public Result MarkStockReserved()
    {
        if (Status != OrderStatus.Pending)
        {
            return Result.Failure(OrderErrors.InvalidTransition(Status, OrderStatus.AwaitingPayment));
        }

        Status = OrderStatus.AwaitingPayment;
        return Result.Success();
    }

    public Result ConfirmPayment()
    {
        if (Status != OrderStatus.AwaitingPayment)
        {
            return Result.Failure(OrderErrors.InvalidTransition(Status, OrderStatus.Confirmed));
        }

        Status = OrderStatus.Confirmed;
        return Result.Success();
    }

    public Result Cancel()
    {
        if (Status is not (OrderStatus.Pending or OrderStatus.AwaitingPayment))
        {
            return Result.Failure(OrderErrors.InvalidTransition(Status, OrderStatus.Cancelled));
        }

        Status = OrderStatus.Cancelled;
        return Result.Success();
    }
}
