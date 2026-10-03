using Domain.Enums;
using Domain.Models;
using Infrastructure;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Delivery.Common
{
    /// <summary>Rider-facing push + in-app notification.</summary>
    public interface IRiderNotifier
    {
        /// <summary>Stores a <see cref="DeliveryNotification"/> and pushes it to the rider's devices.</summary>
        Task NotifyAsync(
            int deliveryId,
            string title,
            string message,
            string type,
            int? orderId = null,
            string? orderCode = null,
            CancellationToken cancellationToken = default);
    }

    public class RiderNotifier : IRiderNotifier
    {
        /// <summary>Android raw resource / iOS bundle sound shipped in the rider app.</summary>
        public const string Sound = "rider_alert";

        /// <summary>Android channel created by the rider app with <see cref="Sound"/>.</summary>
        public const string ChannelId = "rider_orders";

        private readonly DatabaseContext _context;
        private readonly INotificationService _push;
        private readonly ILogger<RiderNotifier> _logger;

        public RiderNotifier(DatabaseContext context, INotificationService push, ILogger<RiderNotifier> logger)
        {
            _context = context;
            _push = push;
            _logger = logger;
        }

        public async Task NotifyAsync(
            int deliveryId,
            string title,
            string message,
            string type,
            int? orderId = null,
            string? orderCode = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _context.DeliveryNotifications.Add(DeliveryNotification.Create(
                    deliveryId, title, message, NotificationType.OrderUpdated, orderId, "System"));
                await _context.SaveChangesAsync(cancellationToken);

                var tokens = await _context.Deliveries
                    .AsNoTracking()
                    .Where(d => d.DeliveryId == deliveryId)
                    .Select(d => new { d.AndriodDevice, d.IosDevice })
                    .FirstOrDefaultAsync(cancellationToken);

                if (tokens == null)
                    return;

                var payload = new Dictionary<string, string>
                {
                    ["type"] = type,
                    ["action"] = "open_order_detail"
                };
                if (orderId.HasValue)
                    payload["orderId"] = orderId.Value.ToString();
                if (!string.IsNullOrWhiteSpace(orderCode))
                    payload["orderCode"] = orderCode;

                foreach (var token in new[] { tokens.AndriodDevice, tokens.IosDevice }
                             .Where(t => !string.IsNullOrWhiteSpace(t))
                             .Distinct())
                {
                    await _push.SendNotificationForSingleDevice(new NotificationBody
                    {
                        Title = title,
                        Body = message,
                        FireBaseToken = token!,
                        PayLoad = payload,
                        Sound = Sound,
                        AndroidChannelId = ChannelId
                    });
                }
            }
            catch (Exception ex)
            {
                // A failed notification must never fail the admin action that triggered it,
                // nor be retried by every later save on this request's shared context.
                _logger.LogError(ex, "Failed to notify rider {DeliveryId}", deliveryId);
                foreach (var entry in _context.ChangeTracker.Entries<DeliveryNotification>()
                             .Where(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Added)
                             .ToList())
                    entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
            }
        }
    }
}
