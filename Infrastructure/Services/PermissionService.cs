using Domain.Authorization;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services
{
    /// <summary>What a user may do inside the admin or merchant panel.</summary>
    public sealed class UserAccess
    {
        public static readonly UserAccess Denied = new();

        /// <summary>False when the user, its profile, its merchant or its sub-role is inactive.</summary>
        public bool IsAllowed { get; init; }
        public AppRole? Scope { get; init; }
        public int? SubRoleId { get; init; }
        public string? SubRoleName { get; init; }
        public string? SubRoleNameAr { get; init; }
        public bool IsFullAccess { get; init; }
        public bool IsOwner { get; init; }
        public int? MerchantId { get; init; }
        public IReadOnlySet<string> Permissions { get; init; } = new HashSet<string>();

        public bool Has(string permission) => IsAllowed && Permissions.Contains(permission);
    }

    public interface IPermissionService
    {
        /// <summary>Loads (cached) access of <paramref name="userId"/> in the admin (SuperAdmin) or merchant scope.</summary>
        Task<UserAccess> GetUserAccessAsync(int userId, AppRole scope, CancellationToken cancellationToken = default);

        /// <summary>Drops every cached access so changes to sub-roles / users apply on the next request.</summary>
        void InvalidateCache();
    }

    public class PermissionService : IPermissionService
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

        /// <summary>Part of every cache key; bumping it orphans all cached entries.</summary>
        private static long _version;

        private readonly DatabaseContext _context;
        private readonly IMemoryCache _cache;

        public PermissionService(DatabaseContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        /// <summary>The panel a set of Identity roles logs into: Super Admin wins over Merchant.</summary>
        public static AppRole? ResolvePanelScope(IEnumerable<string> roles)
        {
            var list = roles as ICollection<string> ?? roles.ToList();
            if (list.Contains(AppRoleNames.SuperAdmin))
                return AppRole.SuperAdmin;
            if (list.Contains(AppRoleNames.Merchant))
                return AppRole.Merchant;
            return null;
        }

        public async Task<UserAccess> GetUserAccessAsync(int userId, AppRole scope, CancellationToken cancellationToken = default)
        {
            if (userId <= 0 || !PermissionCatalog.IsSubRoleScope(scope))
                return UserAccess.Denied;

            var key = $"perm:{Interlocked.Read(ref _version)}:{userId}:{(int)scope}";

            if (_cache.TryGetValue(key, out UserAccess? cached) && cached != null)
                return cached;

            var access = await LoadAsync(userId, scope, cancellationToken);
            _cache.Set(key, access, CacheDuration);
            return access;
        }

        public void InvalidateCache() => InvalidateCacheGlobally();

        /// <summary>Called by <see cref="DatabaseContext"/> after saving users, merchants or sub-roles.</summary>
        public static void InvalidateCacheGlobally() => Interlocked.Increment(ref _version);

        private async Task<UserAccess> LoadAsync(int userId, AppRole scope, CancellationToken cancellationToken)
        {
            var userActive = await _context.Users
                .Where(u => u.Id == userId)
                .Select(u => (bool?)u.Active)
                .FirstOrDefaultAsync(cancellationToken);
            if (userActive != true)
                return UserAccess.Denied;

            int? subRoleId;
            int? merchantId = null;
            var isOwner = false;

            if (scope == AppRole.SuperAdmin)
            {
                subRoleId = await _context.Employees
                    .Where(e => e.UserId == userId)
                    .Select(e => e.SubRoleId)
                    .FirstOrDefaultAsync(cancellationToken);
            }
            else
            {
                var merchantUser = await _context.MerchantUsers
                    .Where(mu => mu.UserId == userId && !mu.IsDeleted && mu.IsActive
                                 && mu.Merchant.IsActive && !mu.Merchant.IsDeleted)
                    .Select(mu => new { mu.SubRoleId, mu.MerchantId, mu.IsOwner })
                    .FirstOrDefaultAsync(cancellationToken);
                if (merchantUser == null)
                    return UserAccess.Denied;

                subRoleId = merchantUser.SubRoleId;
                merchantId = merchantUser.MerchantId;
                isOwner = merchantUser.IsOwner;
            }

            if (subRoleId == null)
                return UserAccess.Denied;

            var subRole = await _context.SubRoles
                .Where(r => r.SubRoleId == subRoleId && r.Scope == scope && r.IsActive)
                .Select(r => new { r.SubRoleId, r.Name, r.NameAr, r.IsFullAccess })
                .FirstOrDefaultAsync(cancellationToken);
            if (subRole == null)
                return UserAccess.Denied;

            var permissionsQuery = subRole.IsFullAccess
                ? _context.Permissions.Where(p => p.Scope == scope && p.IsActive)
                : _context.SubRolePermissions
                    .Where(rp => rp.SubRoleId == subRole.SubRoleId && rp.Permission.IsActive && rp.Permission.Scope == scope)
                    .Select(rp => rp.Permission);
            var permissions = await permissionsQuery.Select(p => p.PermissionName).ToListAsync(cancellationToken);

            return new UserAccess
            {
                IsAllowed = true,
                Scope = scope,
                SubRoleId = subRole.SubRoleId,
                SubRoleName = subRole.Name,
                SubRoleNameAr = subRole.NameAr,
                IsFullAccess = subRole.IsFullAccess,
                IsOwner = isOwner,
                MerchantId = merchantId,
                Permissions = permissions.ToHashSet()
            };
        }
    }
}
