namespace Oms.Orders.Api.Contracts;

internal sealed record CreateOrderItemRequest(
    Guid ProductId,
    int Quantity,
    decimal UnitPriceAmount,
    string? Currency);

internal sealed record CreateOrderRequest(IReadOnlyList<CreateOrderItemRequest>? Items);
