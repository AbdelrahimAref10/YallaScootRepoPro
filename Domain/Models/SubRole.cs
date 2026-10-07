using Domain.Common;
using Domain.Enums;

namespace Domain.Models
{
    /// <summary>
    /// A permission set inside a main role (Super Admin or Merchant), e.g. "Admin" or "Operation".
    /// </summary>
    public class SubRole : IAuditable
    {
        public int SubRoleId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public string? NameAr { get; private set; }
        public AppRole Scope { get; private set; }
        /// <summary>Seeded role; cannot be edited or deleted.</summary>
        public bool IsSystem { get; private set; }
        /// <summary>Has every active permission in its scope, including modules added later.</summary>
        public bool IsFullAccess { get; private set; }
        public bool IsActive { get; private set; } = true;
        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }

        public ICollection<SubRolePermission> SubRolePermissions { get; private set; } = new List<SubRolePermission>();

        private SubRole() { }

        public static SubRole Create(
            string name,
            string? nameAr,
            AppRole scope,
            bool isSystem = false,
            bool isFullAccess = false,
            string? createdBy = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Sub-role name cannot be empty", nameof(name));

            if (scope is not (AppRole.SuperAdmin or AppRole.Merchant))
                throw new ArgumentException("Sub-roles are only supported for Super Admin and Merchant", nameof(scope));

            return new SubRole
            {
                Name = name.Trim(),
                NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim(),
                Scope = scope,
                IsSystem = isSystem,
                IsFullAccess = isFullAccess,
                IsActive = true,
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };
        }

        public void Update(string name, string? nameAr, bool isActive, string? modifiedBy = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Sub-role name cannot be empty", nameof(name));

            Name = name.Trim();
            NameAr = string.IsNullOrWhiteSpace(nameAr) ? null : nameAr.Trim();
            IsActive = isActive;
            LastModifiedBy = modifiedBy;
            LastModifiedDate = DateTime.UtcNow;
        }

        /// <summary>Replaces the permission set with <paramref name="permissionIds"/>.</summary>
        public void SetPermissions(IEnumerable<int> permissionIds, string? modifiedBy = null)
        {
            var wanted = permissionIds.Distinct().ToHashSet();

            foreach (var existing in SubRolePermissions.Where(p => !wanted.Contains(p.PermissionId)).ToList())
                SubRolePermissions.Remove(existing);

            var current = SubRolePermissions.Select(p => p.PermissionId).ToHashSet();
            foreach (var id in wanted.Where(id => !current.Contains(id)))
                SubRolePermissions.Add(SubRolePermission.Create(id, modifiedBy));

            LastModifiedBy = modifiedBy;
            LastModifiedDate = DateTime.UtcNow;
        }
    }
}
