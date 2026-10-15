using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oms.Orders.Domain;

namespace Oms.Orders.Infrastructure.Persistence.Configurations;

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    private const int IsoCurrencyCodeLength = 3;

    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "order_items",
            tableBuilder =>
            {
                tableBuilder.HasCheckConstraint(
                    "CK_order_items_quantity_positive",
                    "quantity > 0");
                tableBuilder.HasCheckConstraint(
                    "CK_order_items_unit_price_amount_positive",
                    "unit_price_amount > 0");
                tableBuilder.HasCheckConstraint(
                    "CK_order_items_unit_price_currency_length",
                    "char_length(unit_price_currency) = 3");
            });

        builder.HasKey(item => item.Id);

        builder.Property(item => item.Id)
            .HasColumnName("id")
            .HasColumnType("uuid")
            .ValueGeneratedNever();

        builder.Property<Guid>("OrderId")
            .HasColumnName("order_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(item => item.ProductId)
            .HasColumnName("product_id")
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(item => item.Quantity)
            .HasColumnName("quantity")
            .IsRequired();

        builder.ComplexProperty(
            item => item.UnitPrice,
            priceBuilder =>
            {
                priceBuilder.Property(money => money.Amount)
                    .HasColumnName("unit_price_amount")
                    .HasColumnType("numeric")
                    .IsRequired();

                priceBuilder.Property(money => money.Currency)
                    .HasColumnName("unit_price_currency")
                    .HasMaxLength(IsoCurrencyCodeLength)
                    .IsFixedLength()
                    .IsRequired();

                priceBuilder.Ignore(money => money.IsInitialised);
            });

        builder.Ignore(item => item.LineTotal);

        builder.HasIndex("OrderId", nameof(OrderItem.ProductId))
            .IsUnique()
            .HasDatabaseName("UX_order_items_order_id_product_id");
    }
}
