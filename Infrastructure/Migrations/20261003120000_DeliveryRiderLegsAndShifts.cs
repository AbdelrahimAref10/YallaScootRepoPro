using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <summary>
    /// Rider app: one rider per leg (delivery / return), per-city leg commission,
    /// shifts with online/offline, rider device tokens and cash debt limit, customer app language, 4 handover photos, rider notifications, customer notifications with read state.
    /// Existing assignments become the delivery leg and get a return leg with the same rider
    /// at 0% commission, so riders already paid 100% on delivery are not paid twice.
    /// </summary>
    public partial class DeliveryRiderLegsAndShifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── City: leg commission percents ──
            migrationBuilder.AddColumn<decimal>(
                name: "DeliveryLegCommissionPercent",
                table: "VO_City",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 20m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReturnLegCommissionPercent",
                table: "VO_City",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 20m);

            // ── Delivery: online + device tokens ──
            migrationBuilder.AddColumn<bool>(
                name: "IsOnline",
                table: "VO_Delivery",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "OnlineStatusChangedAt",
                table: "VO_Delivery",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AndriodDevice",
                table: "VO_Delivery",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IosDevice",
                table: "VO_Delivery",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CashDebtLimit",
                table: "VO_Delivery",
                type: "decimal(18,2)",
                nullable: true);

            // ── Customer: app language for push text ──
            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "VO_Customer",
                type: "nvarchar(5)",
                maxLength: 5,
                nullable: true);

            // ── DeliveryMenOrder: leg ──
            migrationBuilder.DropIndex(
                name: "IX_VO_DeliveryMenOrder_Order_Vehicle",
                table: "VO_DeliveryMenOrder");

            migrationBuilder.AddColumn<int>(
                name: "Leg",
                table: "VO_DeliveryMenOrder",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_VO_DeliveryMenOrder_Order_Vehicle_Leg",
                table: "VO_DeliveryMenOrder",
                columns: new[] { "OrderId", "VehicleId", "Leg" },
                unique: true);

            // ── DeliveryOrderPaymentDetail: leg + commission percent ──
            migrationBuilder.DropIndex(
                name: "IX_VO_DeliveryOrderPaymentDetail_Order_Vehicle",
                table: "VO_DeliveryOrderPaymentDetail");

            migrationBuilder.AddColumn<int>(
                name: "Leg",
                table: "VO_DeliveryOrderPaymentDetail",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "CommissionPercent",
                table: "VO_DeliveryOrderPaymentDetail",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 100m);

            migrationBuilder.CreateIndex(
                name: "IX_VO_DeliveryOrderPaymentDetail_Order_Vehicle_Leg",
                table: "VO_DeliveryOrderPaymentDetail",
                columns: new[] { "OrderId", "VehicleId", "Leg" },
                unique: true);

            // Existing assignments: the same rider also holds the return leg, with no extra commission.
            migrationBuilder.Sql(@"
INSERT INTO VO_DeliveryMenOrder (OrderId, VehicleId, DeliveryId, Leg, DeliveryReceivedFromMerchant, ReceivedFromMerchantAt, CreatedBy, CreatedDate, LastModifiedBy, LastModifiedDate)
SELECT d.OrderId, d.VehicleId, d.DeliveryId, 2, 0, NULL, 'migration', SYSUTCDATETIME(), NULL, SYSUTCDATETIME()
FROM VO_DeliveryMenOrder d
WHERE d.Leg = 1
  AND NOT EXISTS (SELECT 1 FROM VO_DeliveryMenOrder r WHERE r.OrderId = d.OrderId AND r.VehicleId = d.VehicleId AND r.Leg = 2);

INSERT INTO VO_DeliveryOrderPaymentDetail (OrderId, DeliveryId, VehicleId, DeliveryFeeShare, Leg, CommissionPercent, CreatedBy, CreatedDate, LastModifiedBy, LastModifiedDate)
SELECT p.OrderId, p.DeliveryId, p.VehicleId, 0, 2, 0, 'migration', SYSUTCDATETIME(), NULL, SYSUTCDATETIME()
FROM VO_DeliveryOrderPaymentDetail p
WHERE p.Leg = 1
  AND NOT EXISTS (SELECT 1 FROM VO_DeliveryOrderPaymentDetail r WHERE r.OrderId = p.OrderId AND r.VehicleId = p.VehicleId AND r.Leg = 2);
");

            // ── Shifts ──
            migrationBuilder.CreateTable(
                name: "VO_Shift",
                columns: table => new
                {
                    ShiftId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CityId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    EndTime = table.Column<TimeSpan>(type: "time", nullable: false),
                    DaysOfWeekMask = table.Column<int>(type: "int", nullable: false, defaultValue: 127),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VO_Shift", x => x.ShiftId);
                    table.ForeignKey(
                        name: "FK_VO_Shift_VO_City_CityId",
                        column: x => x.CityId,
                        principalTable: "VO_City",
                        principalColumn: "CityId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VO_Shift_CityId",
                table: "VO_Shift",
                column: "CityId");

            migrationBuilder.CreateTable(
                name: "VO_DeliveryShift",
                columns: table => new
                {
                    DeliveryShiftId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeliveryId = table.Column<int>(type: "int", nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VO_DeliveryShift", x => x.DeliveryShiftId);
                    table.ForeignKey(
                        name: "FK_VO_DeliveryShift_VO_Delivery_DeliveryId",
                        column: x => x.DeliveryId,
                        principalTable: "VO_Delivery",
                        principalColumn: "DeliveryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VO_DeliveryShift_VO_Shift_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "VO_Shift",
                        principalColumn: "ShiftId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VO_DeliveryShift_Delivery_Shift",
                table: "VO_DeliveryShift",
                columns: new[] { "DeliveryId", "ShiftId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VO_DeliveryShift_ShiftId",
                table: "VO_DeliveryShift",
                column: "ShiftId");

            // ── Handover photos ──
            migrationBuilder.CreateTable(
                name: "VO_OrderVehicleHandoverImage",
                columns: table => new
                {
                    OrderVehicleHandoverImageId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    Step = table.Column<int>(type: "int", nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    ImageUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DeliveryId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VO_OrderVehicleHandoverImage", x => x.OrderVehicleHandoverImageId);
                    table.ForeignKey(
                        name: "FK_VO_OrderVehicleHandoverImage_VO_Order_OrderId",
                        column: x => x.OrderId,
                        principalTable: "VO_Order",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VO_OrderVehicleHandoverImage_Order_Vehicle_Step_Position",
                table: "VO_OrderVehicleHandoverImage",
                columns: new[] { "OrderId", "VehicleId", "Step", "Position" },
                unique: true);

            // ── Rider notifications ──
            migrationBuilder.CreateTable(
                name: "VO_DeliveryNotification",
                columns: table => new
                {
                    DeliveryNotificationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeliveryId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    NotificationType = table.Column<int>(type: "int", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VO_DeliveryNotification", x => x.DeliveryNotificationId);
                    table.ForeignKey(
                        name: "FK_VO_DeliveryNotification_VO_Delivery_DeliveryId",
                        column: x => x.DeliveryId,
                        principalTable: "VO_Delivery",
                        principalColumn: "DeliveryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VO_DeliveryNotification_VO_Order_OrderId",
                        column: x => x.OrderId,
                        principalTable: "VO_Order",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VO_DeliveryNotification_CreatedDate",
                table: "VO_DeliveryNotification",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_VO_DeliveryNotification_DeliveryId",
                table: "VO_DeliveryNotification",
                column: "DeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_VO_DeliveryNotification_IsRead",
                table: "VO_DeliveryNotification",
                column: "IsRead");

            migrationBuilder.CreateIndex(
                name: "IX_VO_DeliveryNotification_OrderId",
                table: "VO_DeliveryNotification",
                column: "OrderId");

            // ── Customer notifications ──
            migrationBuilder.CreateTable(
                name: "VO_CustomerNotification",
                columns: table => new
                {
                    CustomerNotificationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    OrderId = table.Column<int>(type: "int", nullable: true),
                    NotificationType = table.Column<int>(type: "int", nullable: false),
                    IsRead = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VO_CustomerNotification", x => x.CustomerNotificationId);
                    table.ForeignKey(
                        name: "FK_VO_CustomerNotification_VO_Customer_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "VO_Customer",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VO_CustomerNotification_VO_Order_OrderId",
                        column: x => x.OrderId,
                        principalTable: "VO_Order",
                        principalColumn: "OrderId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VO_CustomerNotification_CreatedDate",
                table: "VO_CustomerNotification",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_VO_CustomerNotification_CustomerId",
                table: "VO_CustomerNotification",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_VO_CustomerNotification_IsRead",
                table: "VO_CustomerNotification",
                column: "IsRead");

            migrationBuilder.CreateIndex(
                name: "IX_VO_CustomerNotification_OrderId",
                table: "VO_CustomerNotification",
                column: "OrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "VO_CustomerNotification");
            migrationBuilder.DropTable(name: "VO_DeliveryNotification");
            migrationBuilder.DropTable(name: "VO_OrderVehicleHandoverImage");
            migrationBuilder.DropTable(name: "VO_DeliveryShift");
            migrationBuilder.DropTable(name: "VO_Shift");

            migrationBuilder.Sql("DELETE FROM VO_DeliveryOrderPaymentDetail WHERE Leg = 2;");
            migrationBuilder.Sql("DELETE FROM VO_DeliveryMenOrder WHERE Leg = 2;");

            migrationBuilder.DropIndex(
                name: "IX_VO_DeliveryOrderPaymentDetail_Order_Vehicle_Leg",
                table: "VO_DeliveryOrderPaymentDetail");

            migrationBuilder.DropColumn(name: "Leg", table: "VO_DeliveryOrderPaymentDetail");
            migrationBuilder.DropColumn(name: "CommissionPercent", table: "VO_DeliveryOrderPaymentDetail");

            migrationBuilder.CreateIndex(
                name: "IX_VO_DeliveryOrderPaymentDetail_Order_Vehicle",
                table: "VO_DeliveryOrderPaymentDetail",
                columns: new[] { "OrderId", "VehicleId" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_VO_DeliveryMenOrder_Order_Vehicle_Leg",
                table: "VO_DeliveryMenOrder");

            migrationBuilder.DropColumn(name: "Leg", table: "VO_DeliveryMenOrder");

            migrationBuilder.CreateIndex(
                name: "IX_VO_DeliveryMenOrder_Order_Vehicle",
                table: "VO_DeliveryMenOrder",
                columns: new[] { "OrderId", "VehicleId" },
                unique: true);

            migrationBuilder.DropColumn(name: "IsOnline", table: "VO_Delivery");
            migrationBuilder.DropColumn(name: "OnlineStatusChangedAt", table: "VO_Delivery");
            migrationBuilder.DropColumn(name: "AndriodDevice", table: "VO_Delivery");
            migrationBuilder.DropColumn(name: "IosDevice", table: "VO_Delivery");
            migrationBuilder.DropColumn(name: "CashDebtLimit", table: "VO_Delivery");
            migrationBuilder.DropColumn(name: "PreferredLanguage", table: "VO_Customer");

            migrationBuilder.DropColumn(name: "DeliveryLegCommissionPercent", table: "VO_City");
            migrationBuilder.DropColumn(name: "ReturnLegCommissionPercent", table: "VO_City");
        }
    }
}
