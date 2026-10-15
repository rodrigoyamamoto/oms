using Oms.Orders.Domain;

namespace Oms.Orders.Application.Orders.GetOrderById;

internal sealed record OrderItemDetails(
    Guid Id,
    Guid ProductId,
    int Quantity,
    decimal UnitPriceAmount,
    string Currency,
    decimal LineTotalAmount);

internal sealed record OrderDetails(
    Guid Id,
    OrderStatus Status,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<OrderItemDetails> Items);
