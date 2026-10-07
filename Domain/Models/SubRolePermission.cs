using Domain.Common;

namespace Domain.Models
{
    public class SubRolePermission : IAuditable
    {
        public int SubRoleId { get; private set; }
        public int PermissionId { get; private set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }

        public SubRole SubRole { get; private set; } = null!;
        public Permission Permission { get; private set; } = null!;

        private SubRolePermission() { }

        internal static SubRolePermission Create(int permissionId, string? createdBy = null)
        {
            if (permissionId <= 0)
                throw new ArgumentException("Permission ID must be greater than zero", nameof(permissionId));

            return new SubRolePermission
            {
                PermissionId = permissionId,
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };
        }
    }
}
