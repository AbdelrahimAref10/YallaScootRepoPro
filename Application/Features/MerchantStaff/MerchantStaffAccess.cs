using CSharpFunctionalExtensions;
using Domain.Enums;
using Domain.Models;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.MerchantStaff
{
    /// <summary>Shared checks for the merchant staff screens.</summary>
    internal static class MerchantStaffAccess
    {
        /// <summary>Merchant id of the signed-in owner / staff member.</summary>
        public static async Task<Result<int>> CurrentMerchantIdAsync(DatabaseContext context, int userId, CancellationToken cancellationToken)
        {
            var merchantId = await context.MerchantUsers
                .Where(mu => mu.UserId == userId && mu.IsActive && !mu.IsDeleted)
                .Select(mu => (int?)mu.MerchantId)
                .FirstOrDefaultAsync(cancellationToken);

            return merchantId.HasValue
                ? Result.Success(merchantId.Value)
                : Result.Failure<int>("Merchant profile not found for current user");
        }

        /// <summary>A staff member of the current merchant that the caller may change (not the owner, not themself).</summary>
        public static async Task<Result<MerchantUser>> EditableStaffAsync(
            DatabaseContext context, int currentUserId, int merchantUserId, CancellationToken cancellationToken)
        {
            var merchantId = await CurrentMerchantIdAsync(context, currentUserId, cancellationToken);
            if (merchantId.IsFailure)
                return Result.Failure<MerchantUser>(merchantId.Error);

            var staff = await context.MerchantUsers
                .AsTracking()
                .Include(mu => mu.User)
                .FirstOrDefaultAsync(mu => mu.MerchantUserId == merchantUserId
                                           && mu.MerchantId == merchantId.Value
                                           && !mu.IsDeleted, cancellationToken);
            if (staff == null)
                return Result.Failure<MerchantUser>("Staff member not found");
            if (staff.IsOwner)
                return Result.Failure<MerchantUser>("The merchant owner cannot be changed from here");
            if (staff.UserId == currentUserId)
                return Result.Failure<MerchantUser>("You cannot change your own account");

            return Result.Success(staff);
        }

        /// <summary>Staff can only get a non-system, active merchant sub-role.</summary>
        public static Task<bool> IsAssignableSubRoleAsync(DatabaseContext context, int subRoleId, CancellationToken cancellationToken) =>
            context.SubRoles.AnyAsync(r => r.SubRoleId == subRoleId && r.Scope == AppRole.Merchant && r.IsActive && !r.IsSystem, cancellationToken);
    }
}
