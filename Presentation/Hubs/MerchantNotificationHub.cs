using Domain.Enums;
using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Presentation.Hubs
{
    /// <summary>
    /// Each connection joins its merchant's group, so a notification reaches only
    /// that merchant's owner and staff.
    /// </summary>
    [Authorize(Roles = AppRoleNames.Merchant)]
    public class MerchantNotificationHub : Hub
    {
        private readonly DatabaseContext _context;

        public MerchantNotificationHub(DatabaseContext context)
        {
            _context = context;
        }

        public static string GroupName(int merchantId) => $"merchant-{merchantId}";

        public override async Task OnConnectedAsync()
        {
            var merchantId = int.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                ? await _context.MerchantUsers
                    .Where(mu => mu.UserId == userId && mu.IsActive && !mu.IsDeleted)
                    .Select(mu => (int?)mu.MerchantId)
                    .FirstOrDefaultAsync()
                : null;

            if (merchantId == null)
            {
                Context.Abort();
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(merchantId.Value));
            await base.OnConnectedAsync();
        }
    }
}
