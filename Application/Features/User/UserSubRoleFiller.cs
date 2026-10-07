using Application.Features.User.DTOs;
using Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.User
{
    /// <summary>Adds full name and sub-role (admin employee or merchant user) to user DTOs.</summary>
    internal static class UserSubRoleFiller
    {
        public static async Task FillAsync(DatabaseContext context, IReadOnlyCollection<UserDto> users, CancellationToken cancellationToken)
        {
            if (users.Count == 0)
                return;

            var ids = users.Select(u => u.Id).ToList();

            var employees = await context.Employees
                .Where(e => ids.Contains(e.UserId))
                .Select(e => new { e.UserId, e.FullName, e.SubRoleId, SubRoleName = (string?)e.SubRole!.Name, SubRoleNameAr = e.SubRole!.NameAr })
                .ToDictionaryAsync(e => e.UserId, cancellationToken);

            var merchantUsers = await context.MerchantUsers
                .Where(mu => ids.Contains(mu.UserId))
                .Select(mu => new { mu.UserId, mu.FullName, SubRoleId = (int?)mu.SubRoleId, SubRoleName = (string?)mu.SubRole.Name, SubRoleNameAr = mu.SubRole.NameAr })
                .ToDictionaryAsync(mu => mu.UserId, cancellationToken);

            foreach (var user in users)
            {
                if (employees.TryGetValue(user.Id, out var employee))
                {
                    user.FullName = employee.FullName;
                    user.SubRoleId = employee.SubRoleId;
                    user.SubRoleName = employee.SubRoleName;
                    user.SubRoleNameAr = employee.SubRoleNameAr;
                }
                else if (merchantUsers.TryGetValue(user.Id, out var merchantUser))
                {
                    user.FullName = merchantUser.FullName;
                    user.SubRoleId = merchantUser.SubRoleId;
                    user.SubRoleName = merchantUser.SubRoleName;
                    user.SubRoleNameAr = merchantUser.SubRoleNameAr;
                }
            }
        }
    }
}
