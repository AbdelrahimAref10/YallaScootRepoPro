using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubRolesAndMerchantUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VO_RolePermission");

            migrationBuilder.AddColumn<string>(
                name: "Action",
                table: "VO_Permission",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Scope",
                table: "VO_Permission",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SubRoleId",
                table: "VO_Employee",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VO_SubRole",
                columns: table => new
                {
                    SubRoleId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    IsFullAccess = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VO_SubRole", x => x.SubRoleId);
                });

            migrationBuilder.CreateTable(
                name: "VO_MerchantUser",
                columns: table => new
                {
                    MerchantUserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MerchantId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SubRoleId = table.Column<int>(type: "int", nullable: false),
                    IsOwner = table.Column<bool>(type: "bit", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VO_MerchantUser", x => x.MerchantUserId);
                    table.ForeignKey(
                        name: "FK_VO_MerchantUser_VO_Merchant_MerchantId",
                        column: x => x.MerchantId,
                        principalTable: "VO_Merchant",
                        principalColumn: "MerchantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VO_MerchantUser_VO_SubRole_SubRoleId",
                        column: x => x.SubRoleId,
                        principalTable: "VO_SubRole",
                        principalColumn: "SubRoleId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VO_MerchantUser_VO_User_UserId",
                        column: x => x.UserId,
                        principalTable: "VO_User",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VO_SubRolePermission",
                columns: table => new
                {
                    SubRoleId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VO_SubRolePermission", x => new { x.SubRoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_VO_SubRolePermission_VO_Permission_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "VO_Permission",
                        principalColumn: "PermissionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VO_SubRolePermission_VO_SubRole_SubRoleId",
                        column: x => x.SubRoleId,
                        principalTable: "VO_SubRole",
                        principalColumn: "SubRoleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VO_Employee_SubRoleId",
                table: "VO_Employee",
                column: "SubRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_MerchantUser_MerchantId",
                table: "VO_MerchantUser",
                column: "MerchantId");

            migrationBuilder.CreateIndex(
                name: "IX_MerchantUser_UserId",
                table: "VO_MerchantUser",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VO_MerchantUser_SubRoleId",
                table: "VO_MerchantUser",
                column: "SubRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_SubRole_Scope_Name",
                table: "VO_SubRole",
                columns: new[] { "Scope", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubRolePermission_PermissionId",
                table: "VO_SubRolePermission",
                column: "PermissionId");

            migrationBuilder.AddForeignKey(
                name: "FK_VO_Employee_VO_SubRole_SubRoleId",
                table: "VO_Employee",
                column: "SubRoleId",
                principalTable: "VO_SubRole",
                principalColumn: "SubRoleId",
                onDelete: ReferentialAction.Restrict);

            // System sub-roles (Scope: 2 = Super Admin, 3 = Merchant). Permissions are synced by SeedData.
            migrationBuilder.Sql(@"
INSERT INTO VO_SubRole (Name, NameAr, Scope, IsSystem, IsFullAccess, IsActive, CreatedBy, CreatedDate, LastModifiedDate)
VALUES (N'Admin', N'مدير النظام', 2, 1, 1, 1, N'System', GETUTCDATE(), GETUTCDATE()),
       (N'Merchant Owner', N'صاحب المتجر', 3, 1, 1, 1, N'System', GETUTCDATE(), GETUTCDATE());

UPDATE VO_Employee
SET SubRoleId = (SELECT SubRoleId FROM VO_SubRole WHERE Scope = 2 AND Name = N'Admin')
WHERE SubRoleId IS NULL;

INSERT INTO VO_MerchantUser (MerchantId, UserId, SubRoleId, IsOwner, FullName, IsActive, IsDeleted, CreatedBy, CreatedDate, LastModifiedDate)
SELECT m.MerchantId, m.UserId,
       (SELECT SubRoleId FROM VO_SubRole WHERE Scope = 3 AND Name = N'Merchant Owner'),
       1, m.FullName, 1, m.IsDeleted, N'System', GETUTCDATE(), GETUTCDATE()
FROM VO_Merchant m;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VO_Employee_VO_SubRole_SubRoleId",
                table: "VO_Employee");

            migrationBuilder.DropTable(
                name: "VO_MerchantUser");

            migrationBuilder.DropTable(
                name: "VO_SubRolePermission");

            migrationBuilder.DropTable(
                name: "VO_SubRole");

            migrationBuilder.DropIndex(
                name: "IX_VO_Employee_SubRoleId",
                table: "VO_Employee");

            migrationBuilder.DropColumn(
                name: "Action",
                table: "VO_Permission");

            migrationBuilder.DropColumn(
                name: "Scope",
                table: "VO_Permission");

            migrationBuilder.DropColumn(
                name: "SubRoleId",
                table: "VO_Employee");

            migrationBuilder.CreateTable(
                name: "VO_RolePermission",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VO_RolePermission", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_VO_RolePermission_VO_Permission_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "VO_Permission",
                        principalColumn: "PermissionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VO_RolePermission_VO_Role_RoleId",
                        column: x => x.RoleId,
                        principalTable: "VO_Role",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "VO_RolePermission",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId",
                table: "VO_RolePermission",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_PermissionId",
                table: "VO_RolePermission",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);
        }
    }
}
