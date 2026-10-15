using System.Collections.ObjectModel;

namespace Oms.Orders.Domain;

internal sealed class Order
{
    private readonly List<OrderItem> _items;
    private readonly ReadOnlyCollection<OrderItem> _readOnlyItems;

    private Order(Guid id, List<OrderItem> items)
    {
        Id = id;
        _items = items;
        _readOnlyItems = _items.AsReadOnly();
        Status = OrderStatus.Pending;
    }

    public Guid Id { get; }

    public OrderStatus Status { get; private set; }

    public IReadOnlyList<OrderItem> Items => _readOnlyItems;

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

    public static Order Create(IEnumerable<OrderItemDraft> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var createdItems = new List<OrderItem>();
        var seenProductIds = new HashSet<Guid>();
        string? orderCurrency = null;

        foreach (var draft in items)
        {
            var orderItem = new OrderItem(draft.ProductId, draft.Quantity, draft.UnitPrice);

            if (!seenProductIds.Add(orderItem.ProductId))
            {
                throw new ArgumentException(
                    $"Duplicate product '{orderItem.ProductId}' is not permitted within the same order.",
                    nameof(items));
            }

            if (orderCurrency is null)
            {
                orderCurrency = orderItem.UnitPrice.Currency;
            }
            else if (!string.Equals(orderCurrency, orderItem.UnitPrice.Currency, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"All order items must share the same currency ('{orderCurrency}' vs '{orderItem.UnitPrice.Currency}').",
                    nameof(items));
            }

            createdItems.Add(orderItem);
        }

        if (createdItems.Count == 0)
        {
            throw new ArgumentException("An order must contain at least one item.", nameof(items));
        }

        return new Order(Guid.CreateVersion7(), createdItems);
    }

    public void MarkStockReserved()
    {
        if (Status != OrderStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Cannot mark stock as reserved when the order is in the '{Status}' state.");
        }

        Status = OrderStatus.AwaitingPayment;
    }

    public void ConfirmPayment()
    {
        if (Status != OrderStatus.AwaitingPayment)
        {
            throw new InvalidOperationException(
                $"Cannot confirm payment when the order is in the '{Status}' state.");
        }

        Status = OrderStatus.Confirmed;
    }

    public void Cancel()
    {
        if (Status is not (OrderStatus.Pending or OrderStatus.AwaitingPayment))
        {
            throw new InvalidOperationException(
                $"Cannot cancel an order when it is in the '{Status}' state.");
        }

        Status = OrderStatus.Cancelled;
    }
}
