using Domain.Authorization;
using Domain.Enums;
using Domain.Models;
using Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Data
{
    public static class SeedData
    {
        private const string AdminUserName = "admin";
        private const string AdminPassword = "Admin@123";

        public const string AdminSubRoleName = "Admin";
        public const string MerchantOwnerSubRoleName = "Merchant Owner";
        private const string OperationSubRoleName = "Operation";

        public static async Task SeedAdminUserAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var services = scope.ServiceProvider;

            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
            var loggerFactory = services.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("SeedData");
            var dbContext = services.GetRequiredService<DatabaseContext>();

            try
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    if (!await dbContext.Database.CanConnectAsync(cts.Token))
                    {
                        logger.LogWarning("Database is not available. Skipping seed data.");
                        return;
                    }
                }
                catch (Exception dbEx)
                {
                    logger.LogWarning(dbEx, "Cannot connect to database. Skipping seed data.");
                    return;
                }

                foreach (var roleName in AppRoleNames.All)
                {
                    bool roleExists;
                    try
                    {
                        roleExists = await roleManager.RoleExistsAsync(roleName);
                    }
                    catch (Microsoft.Data.SqlClient.SqlException sqlEx) when (sqlEx.Number == 208)
                    {
                        logger.LogWarning("Role table does not exist. Database migrations may not have been applied. Skipping seed data.");
                        return;
                    }

                    if (!roleExists)
                    {
                        var role = new ApplicationRole
                        {
                            Name = roleName,
                            NormalizedName = roleName.ToUpperInvariant(),
                            CreatedDate = DateTime.UtcNow,
                            LastModifiedDate = DateTime.UtcNow
                        };
                        await roleManager.CreateAsync(role);
                        logger.LogInformation("{RoleName} role created", roleName);
                    }
                }

                await SeedPermissionsAsync(dbContext, logger);

                var adminUser = await userManager.FindByNameAsync(AdminUserName);
                if (adminUser == null)
                {
                    adminUser = new ApplicationUser
                    {
                        UserName = AdminUserName,
                        NormalizedUserName = "ADMIN",
                        Email = "admin@ecommerce.com",
                        NormalizedEmail = "ADMIN@ECOMMERCE.COM",
                        EmailConfirmed = true,
                        PhoneNumberConfirmed = true,
                        Active = true,
                        CreatedDate = DateTime.UtcNow,
                        LastModifiedDate = DateTime.UtcNow
                    };

                    var result = await userManager.CreateAsync(adminUser, AdminPassword);
                    if (!result.Succeeded)
                    {
                        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                        logger.LogError("Failed to create admin user: {Errors}", errors);
                        return;
                    }

                    logger.LogInformation("Super Admin user created with username: {UserName}, password: {Password}", AdminUserName, AdminPassword);
                }
                else
                {
                    // Keep seed admin usable after identity/role migrations.
                    var needsUpdate = false;
                    if (!adminUser.Active)
                    {
                        adminUser.Active = true;
                        needsUpdate = true;
                    }
                    if (!adminUser.EmailConfirmed)
                    {
                        adminUser.EmailConfirmed = true;
                        needsUpdate = true;
                    }
                    if (needsUpdate)
                        await userManager.UpdateAsync(adminUser);

                    // If seed password no longer works (e.g. after migrations), restore it.
                    if (!await userManager.CheckPasswordAsync(adminUser, AdminPassword))
                    {
                        if (await userManager.HasPasswordAsync(adminUser))
                            await userManager.RemovePasswordAsync(adminUser);

                        var passwordResult = await userManager.AddPasswordAsync(adminUser, AdminPassword);
                        if (!passwordResult.Succeeded)
                        {
                            var errors = string.Join(", ", passwordResult.Errors.Select(e => e.Description));
                            logger.LogError("Failed to reset admin password: {Errors}", errors);
                        }
                        else
                        {
                            logger.LogInformation("Admin password restored to seed default");
                        }
                    }
                    else
                    {
                        logger.LogInformation("Admin user already exists");
                    }
                }

                if (!await userManager.IsInRoleAsync(adminUser, AppRoleNames.SuperAdmin))
                    await userManager.AddToRoleAsync(adminUser, AppRoleNames.SuperAdmin);

                // Migrate legacy "Admin" role assignment if present.
                if (await userManager.IsInRoleAsync(adminUser, "Admin"))
                    await userManager.RemoveFromRoleAsync(adminUser, "Admin");

                var adminSubRoleId = await dbContext.SubRoles
                    .Where(r => r.Scope == AppRole.SuperAdmin && r.IsSystem && r.Name == AdminSubRoleName)
                    .Select(r => r.SubRoleId)
                    .FirstAsync();

                if (!dbContext.Employees.Any(e => e.UserId == adminUser.Id))
                {
                    dbContext.Employees.Add(Employee.Create(adminUser.Id, "System Admin", "System", adminSubRoleId));
                    await dbContext.SaveChangesAsync();
                }

                await BackfillSubRolesAsync(dbContext, adminSubRoleId, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while seeding admin user");
            }
        }

        /// <summary>
        /// Syncs VO_Permission with <see cref="PermissionCatalog"/> and makes sure the system sub-roles exist.
        /// </summary>
        private static async Task SeedPermissionsAsync(DatabaseContext dbContext, ILogger logger)
        {
            var existing = await dbContext.Permissions.AsTracking().ToListAsync();
            var byName = existing.ToDictionary(p => p.PermissionName);

            foreach (var definition in PermissionCatalog.All)
            {
                if (byName.TryGetValue(definition.Name, out var permission))
                {
                    if (!permission.IsActive)
                        permission.Activate("System");
                    continue;
                }

                dbContext.Permissions.Add(Permission.Create(
                    definition.Name,
                    $"{definition.Action} {definition.Module}",
                    definition.Module,
                    definition.Scope,
                    definition.Action,
                    "System"));
                logger.LogInformation("Permission {Permission} created", definition.Name);
            }

            foreach (var stale in existing.Where(p => p.IsActive && !PermissionCatalog.Exists(p.PermissionName)))
                stale.Deactivate("System");

            await EnsureSubRoleAsync(dbContext, AdminSubRoleName, "مدير النظام", AppRole.SuperAdmin, isSystem: true);
            await EnsureSubRoleAsync(dbContext, MerchantOwnerSubRoleName, "صاحب المتجر", AppRole.Merchant, isSystem: true);
            await dbContext.SaveChangesAsync();

            if (!await dbContext.SubRoles.AnyAsync(r => r.Scope == AppRole.SuperAdmin && r.Name == OperationSubRoleName))
            {
                var operation = SubRole.Create(OperationSubRoleName, "العمليات", AppRole.SuperAdmin, createdBy: "System");
                string[] operationPermissions =
                [
                    Permissions.Admin.Dashboard.View,
                    Permissions.Admin.Orders.View, Permissions.Admin.Orders.Edit,
                    Permissions.Admin.Shifts.View, Permissions.Admin.Shifts.Edit,
                    Permissions.Admin.Deliveries.View, Permissions.Admin.Deliveries.Edit,
                    Permissions.Admin.Merchants.View, Permissions.Admin.Merchants.Edit,
                    Permissions.Admin.Customers.View, Permissions.Admin.Customers.Edit
                ];
                var ids = await dbContext.Permissions
                    .Where(p => operationPermissions.Contains(p.PermissionName))
                    .Select(p => p.PermissionId)
                    .ToListAsync();
                operation.SetPermissions(ids, "System");
                dbContext.SubRoles.Add(operation);
                await dbContext.SaveChangesAsync();
                logger.LogInformation("Operation sub-role created");
            }
        }

        private static async Task EnsureSubRoleAsync(DatabaseContext dbContext, string name, string nameAr, AppRole scope, bool isSystem)
        {
            if (await dbContext.SubRoles.AnyAsync(r => r.Scope == scope && r.Name == name))
                return;

            dbContext.SubRoles.Add(SubRole.Create(name, nameAr, scope, isSystem, isFullAccess: true, createdBy: "System"));
        }

        /// <summary>Gives every admin employee and every merchant owner a sub-role.</summary>
        private static async Task BackfillSubRolesAsync(DatabaseContext dbContext, int adminSubRoleId, ILogger logger)
        {
            var employees = await dbContext.Employees.AsTracking().Where(e => e.SubRoleId == null).ToListAsync();
            foreach (var employee in employees)
                employee.SetSubRole(adminSubRoleId, "System");

            var ownerSubRoleId = await dbContext.SubRoles
                .Where(r => r.Scope == AppRole.Merchant && r.IsSystem && r.Name == MerchantOwnerSubRoleName)
                .Select(r => r.SubRoleId)
                .FirstAsync();

            var merchantsWithoutOwner = await dbContext.Merchants
                .Where(m => !dbContext.MerchantUsers.Any(mu => mu.UserId == m.UserId))
                .Select(m => new { m.MerchantId, m.UserId, m.FullName })
                .ToListAsync();
            foreach (var merchant in merchantsWithoutOwner)
                dbContext.MerchantUsers.Add(MerchantUser.CreateOwner(merchant.MerchantId, merchant.UserId, ownerSubRoleId, merchant.FullName, "System"));

            if (employees.Count > 0 || merchantsWithoutOwner.Count > 0)
            {
                await dbContext.SaveChangesAsync();
                logger.LogInformation("Backfilled sub-roles for {Employees} employees and {Merchants} merchants", employees.Count, merchantsWithoutOwner.Count);
            }
        }
    }
}
