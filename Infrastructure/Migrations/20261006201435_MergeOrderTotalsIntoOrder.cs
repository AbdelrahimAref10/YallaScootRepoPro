using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// Folds the 1:1 <c>OrderTotals</c> table into <c>VO_Order</c>.
    /// SubTotal / TotalAfterAllFees already lived on the order (OrderSubTotal / OrderTotal),
    /// so only the four fee columns move. Existing rows are copied before the table is dropped,
    /// and <c>usp_GetAdminOrderDetail</c> keeps returning the same totals result set (same column names).
    /// </summary>
    public partial class MergeOrderTotalsIntoOrder : Migration
    {
        private const string OldTotalsSection =
@"    SELECT TOP (1) t.Id, t.OrderId, t.SubTotal, t.ServiceFees, t.DeliveryFees, t.UrgentFees, t.TieredDiscount, t.TotalAfterAllFees
    FROM dbo.OrderTotals t
    WHERE t.OrderId = @OrderId
    ORDER BY t.Id;";

        private const string NewTotalsSection =
@"    SELECT o.OrderId AS Id, o.OrderId, o.OrderSubTotal AS SubTotal, o.OrderServiceFees AS ServiceFees,
           o.OrderDeliveryFees AS DeliveryFees, o.OrderUrgentFees AS UrgentFees,
           o.OrderTieredDiscount AS TieredDiscount, o.OrderTotal AS TotalAfterAllFees
    FROM dbo.VO_Order o
    WHERE o.OrderId = @OrderId;";

        private static string ProcedureSql()
        {
            var sql = AdminOrderDetailProcedure.ProcedureSql.Replace("\r\n", "\n");
            var oldSection = OldTotalsSection.Replace("\r\n", "\n");
            if (!sql.Contains(oldSection))
                throw new System.InvalidOperationException("usp_GetAdminOrderDetail totals section not found.");
            return sql.Replace(oldSection, NewTotalsSection.Replace("\r\n", "\n"));
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "OrderDeliveryFees",
                table: "VO_Order",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OrderServiceFees",
                table: "VO_Order",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OrderTieredDiscount",
                table: "VO_Order",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "OrderUrgentFees",
                table: "VO_Order",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(@"
UPDATE o
SET o.OrderServiceFees = t.ServiceFees,
    o.OrderDeliveryFees = t.DeliveryFees,
    o.OrderUrgentFees = t.UrgentFees,
    o.OrderTieredDiscount = t.TieredDiscount
FROM dbo.VO_Order o
INNER JOIN dbo.OrderTotals t ON t.OrderId = o.OrderId;");

            migrationBuilder.Sql(ProcedureSql());

            migrationBuilder.DropTable(
                name: "OrderTotals");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderTotals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    DeliveryFees = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ServiceFees = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SubTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TieredDiscount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAfterAllFees = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UrgentFees = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderTotals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrderTotals_VO_Order_OrderId",
                        column: x => x.OrderId,
                        principalTable: "VO_Order",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderTotals_OrderId",
                table: "OrderTotals",
                column: "OrderId",
                unique: true);

            migrationBuilder.Sql(@"
INSERT INTO dbo.OrderTotals (OrderId, SubTotal, ServiceFees, DeliveryFees, UrgentFees, TieredDiscount, TotalAfterAllFees)
SELECT o.OrderId, o.OrderSubTotal, o.OrderServiceFees, o.OrderDeliveryFees, o.OrderUrgentFees, o.OrderTieredDiscount, o.OrderTotal
FROM dbo.VO_Order o;");

            migrationBuilder.Sql(AdminOrderDetailProcedure.ProcedureSql);

            migrationBuilder.DropColumn(
                name: "OrderDeliveryFees",
                table: "VO_Order");

            migrationBuilder.DropColumn(
                name: "OrderServiceFees",
                table: "VO_Order");

            migrationBuilder.DropColumn(
                name: "OrderTieredDiscount",
                table: "VO_Order");

            migrationBuilder.DropColumn(
                name: "OrderUrgentFees",
                table: "VO_Order");
        }
    }
}
