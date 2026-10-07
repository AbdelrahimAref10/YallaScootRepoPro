using CSharpFunctionalExtensions;
using Domain.Authorization;
using Domain.Enums;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.SubRoles
{
    internal static class SubRoleRules
    {
        public static Result<AppRole> ParseScope(int scope) =>
            AppRoleNames.TryFromInt(scope, out var role) && PermissionCatalog.IsSubRoleScope(role)
                ? Result.Success(role)
                : Result.Failure<AppRole>("Scope must be Super Admin (2) or Merchant (3)");

        /// <summary>Resolves permission names of <paramref name="scope"/> to ids; unknown names fail.</summary>
        public static async Task<Result<List<int>>> ResolvePermissionIdsAsync(
            DatabaseContext context, AppRole scope, IReadOnlyCollection<string>? names, CancellationToken cancellationToken)
        {
            var wanted = (names ?? Array.Empty<string>()).Distinct().ToList();
            var invalid = wanted.Where(n => PermissionCatalog.Find(n)?.Scope != scope).ToList();
            if (invalid.Count > 0)
                return Result.Failure<List<int>>($"Unknown permissions: {string.Join(", ", invalid)}");

            var ids = await context.Permissions
                .Where(p => wanted.Contains(p.PermissionName) && p.IsActive)
                .Select(p => p.PermissionId)
                .ToListAsync(cancellationToken);
            return Result.Success(ids);
        }

        public static Task<bool> NameTakenAsync(DatabaseContext context, AppRole scope, string name, int? exceptId, CancellationToken cancellationToken)
        {
            var trimmed = name.Trim();
            return context.SubRoles.AnyAsync(r => r.Scope == scope && r.Name == trimmed && r.SubRoleId != exceptId, cancellationToken);
        }
    }
}
