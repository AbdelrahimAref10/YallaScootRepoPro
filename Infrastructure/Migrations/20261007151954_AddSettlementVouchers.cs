using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSettlementVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SettlementVoucherId",
                table: "VO_OrderJournal",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VO_SettlementVoucher",
                columns: table => new
                {
                    SettlementVoucherId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VoucherNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PartyType = table.Column<int>(type: "int", nullable: false),
                    PartyId = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VO_SettlementVoucher", x => x.SettlementVoucherId);
                });

            migrationBuilder.CreateTable(
                name: "VO_SettlementAllocation",
                columns: table => new
                {
                    SettlementAllocationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SettlementVoucherId = table.Column<int>(type: "int", nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VO_SettlementAllocation", x => x.SettlementAllocationId);
                    table.ForeignKey(
                        name: "FK_VO_SettlementAllocation_VO_Order_OrderId",
                        column: x => x.OrderId,
                        principalTable: "VO_Order",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VO_SettlementAllocation_VO_SettlementVoucher_SettlementVoucherId",
                        column: x => x.SettlementVoucherId,
                        principalTable: "VO_SettlementVoucher",
                        principalColumn: "SettlementVoucherId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VO_OrderJournal_SettlementVoucherId",
                table: "VO_OrderJournal",
                column: "SettlementVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementAllocation_OrderId",
                table: "VO_SettlementAllocation",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_VO_SettlementAllocation_SettlementVoucherId",
                table: "VO_SettlementAllocation",
                column: "SettlementVoucherId");

            migrationBuilder.CreateIndex(
                name: "IX_SettlementVoucher_Party",
                table: "VO_SettlementVoucher",
                columns: new[] { "PartyType", "PartyId", "CreatedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SettlementVoucher_RequestId",
                table: "VO_SettlementVoucher",
                column: "RequestId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_VO_OrderJournal_VO_SettlementVoucher_SettlementVoucherId",
                table: "VO_OrderJournal",
                column: "SettlementVoucherId",
                principalTable: "VO_SettlementVoucher",
                principalColumn: "SettlementVoucherId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VO_OrderJournal_VO_SettlementVoucher_SettlementVoucherId",
                table: "VO_OrderJournal");

            migrationBuilder.DropTable(
                name: "VO_SettlementAllocation");

            migrationBuilder.DropTable(
                name: "VO_SettlementVoucher");

            migrationBuilder.DropIndex(
                name: "IX_VO_OrderJournal_SettlementVoucherId",
                table: "VO_OrderJournal");

            migrationBuilder.DropColumn(
                name: "SettlementVoucherId",
                table: "VO_OrderJournal");
        }
    }
}
