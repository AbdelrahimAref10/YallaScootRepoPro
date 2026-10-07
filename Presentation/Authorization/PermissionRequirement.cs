using Microsoft.AspNetCore.Authorization;

namespace Presentation.Authorization
{
    public sealed class PermissionRequirement : IAuthorizationRequirement
    {
        public PermissionRequirement(IReadOnlyList<string> permissions)
        {
            Permissions = permissions;
        }

        /// <summary>Any one of these grants access.</summary>
        public IReadOnlyList<string> Permissions { get; }
    }
}
