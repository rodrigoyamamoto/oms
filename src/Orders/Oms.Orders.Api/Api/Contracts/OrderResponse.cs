namespace Oms.Orders.Api.Contracts;

internal sealed record OrderItemResponse(
    Guid Id,
    Guid ProductId,
    int Quantity,
    decimal UnitPriceAmount,
    string Currency,
    decimal LineTotalAmount);

internal sealed record OrderResponse(
    Guid Id,
    string Status,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<OrderItemResponse> Items);
