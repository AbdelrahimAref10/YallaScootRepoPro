using Domain.Enums;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Common
{
    public static class SubRoleLookup
    {
        /// <summary>The seeded full-access "Merchant Owner" sub-role.</summary>
        public static Task<int> MerchantOwnerIdAsync(DatabaseContext context, CancellationToken cancellationToken) =>
            context.SubRoles
                .Where(r => r.Scope == AppRole.Merchant && r.IsSystem && r.IsFullAccess)
                .OrderBy(r => r.SubRoleId)
                .Select(r => r.SubRoleId)
                .FirstAsync(cancellationToken);

        /// <summary>True when <paramref name="subRoleId"/> is an active sub-role of <paramref name="scope"/>.</summary>
        public static Task<bool> IsActiveInScopeAsync(DatabaseContext context, int subRoleId, AppRole scope, CancellationToken cancellationToken) =>
            context.SubRoles.AnyAsync(r => r.SubRoleId == subRoleId && r.Scope == scope && r.IsActive, cancellationToken);
    }
}
