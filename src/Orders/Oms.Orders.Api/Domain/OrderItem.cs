namespace Oms.Orders.Domain;

internal sealed class OrderItem
{
    internal OrderItem(Guid productId, int quantity, Money unitPrice)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product identifier cannot be empty.", nameof(productId));
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Order item quantity must be greater than zero.");
        }

        if (string.IsNullOrEmpty(unitPrice.Currency))
        {
            throw new ArgumentException("Order item unit price must specify a valid currency.", nameof(unitPrice));
        }

        if (unitPrice.Amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), unitPrice.Amount, "Order item unit price must be strictly greater than zero.");
        }

        Id = Guid.CreateVersion7();
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public Guid Id { get; }

    public Guid ProductId { get; }

    public int Quantity { get; }

    public Money UnitPrice { get; }

    public Money LineTotal => UnitPrice.Multiply(Quantity);
}
