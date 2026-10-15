using Microsoft.EntityFrameworkCore.Migrations;

namespace Oms.Orders.Infrastructure.Persistence.Migrations;

internal sealed partial class InitialOrdersPersistence : Migration
{
    private static readonly string[] _orderItemUniqueColumns = ["order_id", "product_id"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.CreateTable(
            name: "orders",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_orders", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "order_items",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                quantity = table.Column<int>(type: "integer", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                unit_price_amount = table.Column<decimal>(type: "numeric", nullable: false),
                unit_price_currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_items", x => x.id);
                table.CheckConstraint("CK_order_items_quantity_positive", "quantity > 0");
                table.CheckConstraint("CK_order_items_unit_price_amount_positive", "unit_price_amount > 0");
                table.CheckConstraint("CK_order_items_unit_price_currency_length", "char_length(unit_price_currency) = 3");
                table.ForeignKey(
                    name: "FK_order_items_orders_order_id",
                    column: x => x.order_id,
                    principalTable: "orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "UX_order_items_order_id_product_id",
            table: "order_items",
            columns: _orderItemUniqueColumns,
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        migrationBuilder.DropTable(
            name: "order_items");

        migrationBuilder.DropTable(
            name: "orders");
    }
}
