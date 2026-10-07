using Domain.Authorization;
using Domain.Enums;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Presentation.Authorization
{
    /// <summary>
    /// Checks permissions against the database (through the cached <see cref="IPermissionService"/>),
    /// not the token claims, so sub-role changes apply on the next request.
    /// </summary>
    public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IPermissionService _permissionService;

        public PermissionAuthorizationHandler(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            if (!int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return;

            foreach (var scopeGroup in requirement.Permissions.GroupBy(ScopeOf))
            {
                if (scopeGroup.Key is not { } scope || !context.User.IsInRole(AppRoleNames.ToRoleName(scope)))
                    continue;

                var access = await _permissionService.GetUserAccessAsync(userId, scope);
                if (scopeGroup.Any(access.Has))
                {
                    context.Succeed(requirement);
                    return;
                }
            }
        }

        private static AppRole? ScopeOf(string permission) => PermissionCatalog.Find(permission)?.Scope;
    }
}
