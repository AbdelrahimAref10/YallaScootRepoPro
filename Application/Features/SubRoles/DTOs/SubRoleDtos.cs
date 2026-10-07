namespace Application.Features.SubRoles.DTOs
{
    public class SubRoleDto
    {
        public int SubRoleId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
        /// <summary>AppRole enum int: SuperAdmin=2, Merchant=3.</summary>
        public int Scope { get; set; }
        public bool IsSystem { get; set; }
        public bool IsFullAccess { get; set; }
        public bool IsActive { get; set; }
        public int UsersCount { get; set; }
        public int PermissionsCount { get; set; }
    }

    public class SubRoleDetailDto : SubRoleDto
    {
        /// <summary>Permission names, e.g. "Admin.Orders.View". Every permission of the scope for full-access roles.</summary>
        public List<string> Permissions { get; set; } = new List<string>();
    }

    public class PermissionModuleDto
    {
        public string Module { get; set; } = string.Empty;
        public List<PermissionItemDto> Permissions { get; set; } = new List<PermissionItemDto>();
    }

    public class PermissionItemDto
    {
        public string Name { get; set; } = string.Empty;
        /// <summary>View, Create, Edit or Delete.</summary>
        public string Action { get; set; } = string.Empty;
    }
}
