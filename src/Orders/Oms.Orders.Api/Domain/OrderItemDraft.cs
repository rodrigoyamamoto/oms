namespace Oms.Orders.Domain;

internal readonly record struct OrderItemDraft(Guid ProductId, int Quantity, Money UnitPrice);
