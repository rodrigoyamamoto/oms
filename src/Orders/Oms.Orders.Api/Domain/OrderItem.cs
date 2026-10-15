using Oms.Orders.Domain.Common;
using Oms.Orders.Domain.Errors;

namespace Oms.Orders.Domain;

internal sealed class OrderItem
{
    private OrderItem()
    {
    }

    private OrderItem(Guid id, Guid productId, int quantity, Money unitPrice)
    {
        Id = id;
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public Guid Id { get; }

    public Guid ProductId { get; }

    public int Quantity { get; }

    public Money UnitPrice { get; }

    public Money LineTotal => UnitPrice.Multiply(Quantity);

    internal static Result<OrderItem> Create(Guid productId, int quantity, Money unitPrice)
    {
        if (productId == Guid.Empty)
        {
            return Result.Failure<OrderItem>(OrderErrors.InvalidProductId);
        }

        if (quantity <= 0)
        {
            return Result.Failure<OrderItem>(OrderErrors.InvalidQuantity);
        }

        if (!unitPrice.IsInitialised || unitPrice.Amount <= 0m)
        {
            return Result.Failure<OrderItem>(OrderErrors.InvalidUnitPrice);
        }

        return Result.Success(new OrderItem(Guid.CreateVersion7(), productId, quantity, unitPrice));
    }
}
