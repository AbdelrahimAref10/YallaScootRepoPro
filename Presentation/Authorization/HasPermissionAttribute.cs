using Microsoft.AspNetCore.Authorization;

namespace Presentation.Authorization
{
    /// <summary>
    /// Requires the caller's sub-role to grant at least one of <paramref name="permissions"/>
    /// (constants from <see cref="Domain.Authorization.Permissions"/>).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class HasPermissionAttribute : AuthorizeAttribute
    {
        public const string PolicyPrefix = "perm:";
        public const char Separator = '|';

        public HasPermissionAttribute(params string[] permissions)
            : base(PolicyPrefix + string.Join(Separator, permissions))
        {
            if (permissions.Length == 0)
                throw new ArgumentException("At least one permission is required", nameof(permissions));
        }
    }
}
