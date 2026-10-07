using Domain.Common;

namespace Domain.Models
{
    /// <summary>
    /// A login that works for a merchant: the owner (Merchant.UserId) or a staff member.
    /// </summary>
    public class MerchantUser : IAuditable
    {
        public int MerchantUserId { get; private set; }
        public int MerchantId { get; private set; }
        public int UserId { get; private set; }
        public int SubRoleId { get; private set; }
        public bool IsOwner { get; private set; }
        public string FullName { get; private set; } = string.Empty;
        public bool IsActive { get; private set; } = true;
        public bool IsDeleted { get; private set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }

        public Merchant Merchant { get; private set; } = null!;
        public ApplicationUser User { get; private set; } = null!;
        public SubRole SubRole { get; private set; } = null!;

        private MerchantUser() { }

        public static MerchantUser CreateOwner(int merchantId, int userId, int subRoleId, string fullName, string? createdBy = null) =>
            Create(merchantId, userId, subRoleId, fullName, isOwner: true, createdBy);

        /// <summary>Owner of a merchant that is being added in the same SaveChanges.</summary>
        public static MerchantUser CreateOwner(Merchant merchant, int subRoleId, string? createdBy = null)
        {
            var owner = Create(merchant.MerchantId, merchant.UserId, subRoleId, merchant.FullName, isOwner: true, createdBy, requireMerchantId: false);
            owner.Merchant = merchant;
            return owner;
        }

        public static MerchantUser CreateStaff(int merchantId, int userId, int subRoleId, string fullName, string? createdBy = null) =>
            Create(merchantId, userId, subRoleId, fullName, isOwner: false, createdBy);

        private static MerchantUser Create(int merchantId, int userId, int subRoleId, string fullName, bool isOwner, string? createdBy, bool requireMerchantId = true)
        {
            if (requireMerchantId && merchantId <= 0)
                throw new ArgumentException("Merchant ID must be greater than zero", nameof(merchantId));
            if (userId <= 0)
                throw new ArgumentException("User ID must be greater than zero", nameof(userId));
            if (subRoleId <= 0)
                throw new ArgumentException("Sub-role ID must be greater than zero", nameof(subRoleId));
            if (string.IsNullOrWhiteSpace(fullName))
                throw new ArgumentException("Full name cannot be empty", nameof(fullName));

            return new MerchantUser
            {
                MerchantId = merchantId,
                UserId = userId,
                SubRoleId = subRoleId,
                IsOwner = isOwner,
                FullName = fullName.Trim(),
                IsActive = true,
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };
        }

        public void Update(string fullName, int subRoleId, string? modifiedBy = null)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                throw new ArgumentException("Full name cannot be empty", nameof(fullName));
            if (subRoleId <= 0)
                throw new ArgumentException("Sub-role ID must be greater than zero", nameof(subRoleId));

            FullName = fullName.Trim();
            SubRoleId = subRoleId;
            LastModifiedBy = modifiedBy;
            LastModifiedDate = DateTime.UtcNow;
        }

        public void SetSubRole(int subRoleId, string? modifiedBy = null)
        {
            if (subRoleId <= 0)
                throw new ArgumentException("Sub-role ID must be greater than zero", nameof(subRoleId));

            SubRoleId = subRoleId;
            LastModifiedBy = modifiedBy;
            LastModifiedDate = DateTime.UtcNow;
        }

        public void SetActive(bool isActive, string? modifiedBy = null)
        {
            IsActive = isActive;
            LastModifiedBy = modifiedBy;
            LastModifiedDate = DateTime.UtcNow;
        }

        public void SoftDelete(string? modifiedBy = null)
        {
            IsDeleted = true;
            IsActive = false;
            LastModifiedBy = modifiedBy;
            LastModifiedDate = DateTime.UtcNow;
        }
    }
}
