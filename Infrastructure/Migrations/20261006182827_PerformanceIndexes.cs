using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_VO_OrderPayment_OrderId_State",
                table: "VO_OrderPayment",
                columns: new[] { "OrderId", "State" })
                .Annotation("SqlServer:Include", new[] { "Total" });

            migrationBuilder.CreateIndex(
                name: "IX_VO_Order_OrderState_CreatedDate",
                table: "VO_Order",
                columns: new[] { "OrderState", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_VO_CustomerWallet_Type_CreatedDate",
                table: "VO_CustomerWallet",
                columns: new[] { "Type", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Customer_CreatedDate",
                table: "VO_Customer",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_VO_CompanyTreasury_CreatedDate",
                table: "VO_CompanyTreasury",
                column: "CreatedDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VO_OrderPayment_OrderId_State",
                table: "VO_OrderPayment");

            migrationBuilder.DropIndex(
                name: "IX_VO_Order_OrderState_CreatedDate",
                table: "VO_Order");

            migrationBuilder.DropIndex(
                name: "IX_VO_CustomerWallet_Type_CreatedDate",
                table: "VO_CustomerWallet");

            migrationBuilder.DropIndex(
                name: "IX_Customer_CreatedDate",
                table: "VO_Customer");

            migrationBuilder.DropIndex(
                name: "IX_VO_CompanyTreasury_CreatedDate",
                table: "VO_CompanyTreasury");
        }
    }
}
