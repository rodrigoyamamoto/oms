using Oms.Orders.Domain.Common;

namespace Oms.Orders.Application.Errors;

internal static class OrderApplicationErrors
{
    internal static readonly Error InvalidRequestPayload = new(
        "Orders.InvalidRequestPayload",
        "The request body does not contain valid JSON for an order request.");

    internal static readonly Error InvalidOrderId = new(
        "Orders.InvalidOrderId",
        "Order identifier cannot be empty.");

    internal static Error NotFound(Guid orderId) => new(
        "Orders.NotFound",
        $"Order '{orderId}' was not found.");
}
