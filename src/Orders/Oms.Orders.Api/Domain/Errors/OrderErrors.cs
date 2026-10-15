using Oms.Orders.Domain.Common;

namespace Oms.Orders.Domain.Errors;

internal static class OrderErrors
{
    internal static readonly Error EmptyItems = new(
        "Orders.EmptyItems",
        "An order must contain at least one item.");

    internal static readonly Error InvalidProductId = new(
        "Orders.InvalidProductId",
        "Order item product identifier cannot be empty.");

    internal static readonly Error InvalidQuantity = new(
        "Orders.InvalidQuantity",
        "Order item quantity must be greater than zero.");

    internal static readonly Error InvalidUnitPrice = new(
        "Orders.InvalidUnitPrice",
        "Order item unit price must be initialised with a strictly positive amount.");

    internal static Error DuplicateProduct(Guid productId) => new(
        "Orders.DuplicateProduct",
        $"Duplicate product '{productId}' is not permitted within the same order.");

    internal static Error CurrencyMismatch(string orderCurrency, string itemCurrency) => new(
        "Orders.CurrencyMismatch",
        $"All order items must share the same currency ('{orderCurrency}' vs '{itemCurrency}').");

    internal static Error InvalidTransition(OrderStatus current, OrderStatus target) => new(
        "Orders.InvalidTransition",
        $"Cannot transition order from '{current}' to '{target}'.");
}
