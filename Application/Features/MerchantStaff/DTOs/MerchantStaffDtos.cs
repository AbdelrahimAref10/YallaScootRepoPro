namespace Application.Features.MerchantStaff.DTOs
{
    public class MerchantStaffDto
    {
        public int MerchantUserId { get; set; }
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public int SubRoleId { get; set; }
        public string SubRoleName { get; set; } = string.Empty;
        public string? SubRoleNameAr { get; set; }
        public bool IsOwner { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class SubRoleLookupDto
    {
        public int SubRoleId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? NameAr { get; set; }
    }
}
