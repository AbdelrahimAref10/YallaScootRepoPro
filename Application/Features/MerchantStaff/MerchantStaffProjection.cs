using Application.Features.MerchantStaff.DTOs;
using Domain.Models;
using System.Linq.Expressions;

namespace Application.Features.MerchantStaff
{
    internal static class MerchantStaffProjection
    {
        public static readonly Expression<Func<MerchantUser, MerchantStaffDto>> ToDto = mu => new MerchantStaffDto
        {
            MerchantUserId = mu.MerchantUserId,
            UserId = mu.UserId,
            FullName = mu.FullName,
            UserName = mu.User.UserName ?? string.Empty,
            PhoneNumber = mu.User.PhoneNumber,
            Email = mu.User.Email,
            SubRoleId = mu.SubRoleId,
            SubRoleName = mu.SubRole.Name,
            SubRoleNameAr = mu.SubRole.NameAr,
            IsOwner = mu.IsOwner,
            IsActive = mu.IsActive,
            CreatedDate = mu.CreatedDate
        };
    }
}
