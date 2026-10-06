using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MerchantCompanyCommission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CompanyCommissionAmount",
                table: "VO_MerchantOrderPaymentDetail",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CompanyCommissionPercent",
                table: "VO_MerchantOrderPaymentDetail",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "CompanyCommissionPercent",
                table: "VO_Merchant",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompanyCommissionAmount",
                table: "VO_MerchantOrderPaymentDetail");

            migrationBuilder.DropColumn(
                name: "CompanyCommissionPercent",
                table: "VO_MerchantOrderPaymentDetail");

            migrationBuilder.DropColumn(
                name: "CompanyCommissionPercent",
                table: "VO_Merchant");
        }
    }
}
