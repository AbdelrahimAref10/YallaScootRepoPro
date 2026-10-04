using Application.Common.DomainEvents;
using Domain.Enums;
using Domain.Events;
using Infrastructure;
using Domain.Models;
using Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Customer.Common
{
    /// <summary>
    /// Push text in both app languages. <c>{code}</c> is replaced with the order code.
    /// </summary>
    public sealed record CustomerPushText(string TitleAr, string BodyAr, string TitleEn, string BodyEn);

    /// <summary>Push to the customer who owns an order, in the customer's app language. Never throws.</summary>
    public interface ICustomerNotifier
    {
        Task NotifyOrderAsync(
            int orderId,
            CustomerPushText text,
            NotificationType type,
            CancellationToken cancellationToken = default);
    }

    public class CustomerNotifier : ICustomerNotifier
    {
        private readonly DatabaseContext _context;
        private readonly INotificationService _push;
        private readonly ILogger<CustomerNotifier> _logger;

        public CustomerNotifier(DatabaseContext context, INotificationService push, ILogger<CustomerNotifier> logger)
        {
            _context = context;
            _push = push;
            _logger = logger;
        }

        public async Task NotifyOrderAsync(
            int orderId,
            CustomerPushText text,
            NotificationType type,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var target = await _context.Orders
                    .AsNoTracking()
                    .Where(o => o.OrderId == orderId)
                    .Select(o => new
                    {
                        o.OrderCode,
                        o.CustomerId,
                        o.Customer.AndriodDevice,
                        o.Customer.IosDevice,
                        o.Customer.PreferredLanguage
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                if (target == null)
                    return;

                // Customers who have not reported a language yet get Arabic.
                var english = target.PreferredLanguage == "en";
                var title = (english ? text.TitleEn : text.TitleAr).Replace("{code}", target.OrderCode);
                var body = (english ? text.BodyEn : text.BodyAr).Replace("{code}", target.OrderCode);

                // Kept for the in-app list (with read state) even when the customer has no device token.
                _context.CustomerNotifications.Add(
                    CustomerNotification.Create(target.CustomerId, title, body, type, orderId, "System"));
                await _context.SaveChangesAsync(cancellationToken);

                var tokens = new[] { target.AndriodDevice, target.IosDevice }
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t!)
                    .Distinct()
                    .ToList();

                if (tokens.Count == 0)
                    return;

                await _push.SendNotificationAsyncToMultipleDevices(new NotificationBodyForMultipleDevices
                {
                    Title = title,
                    Body = body,
                    FireBaseTokens = tokens,
                    PayLoad = new Dictionary<string, string>
                    {
                        ["orderId"] = orderId.ToString(),
                        ["orderCode"] = target.OrderCode,
                        ["type"] = ((int)type).ToString(),
                        ["action"] = "open_order_detail"
                    }
                });
            }
            catch (Exception ex)
            {
                // Must never fail the action that triggered it, nor be retried by a later save on this context.
                _logger.LogError(ex, "Failed to push order {OrderId} to its customer", orderId);
                foreach (var entry in _context.ChangeTracker.Entries<CustomerNotification>()
                             .Where(e => e.State == EntityState.Added)
                             .ToList())
                    entry.State = EntityState.Detached;
            }
        }
    }

    /// <summary>Tells the customer about order state changes, whoever made them (admin, merchant, rider).</summary>
    public class OrderStateChangedCustomerPushHandler : INotificationHandler<DomainEventNotification>
    {
        private readonly ICustomerNotifier _notifier;

        public OrderStateChangedCustomerPushHandler(ICustomerNotifier notifier)
        {
            _notifier = notifier;
        }

        public Task Handle(DomainEventNotification notification, CancellationToken cancellationToken)
        {
            if (notification.DomainEvent is not OrderStateChangedEvent e)
                return Task.CompletedTask;

            var message = MessageFor(e.ToState);
            return message == null
                ? Task.CompletedTask
                : _notifier.NotifyOrderAsync(e.OrderId, message.Value.Text, message.Value.Type, cancellationToken);
        }

        /// <summary>
        /// Starts at Confirmed: the merchant states before it are internal and can repeat when the admin
        /// swaps vehicles. DeliveryAssigned is skipped because the assign command pushes the rider's name.
        /// </summary>
        private static (CustomerPushText Text, NotificationType Type)? MessageFor(OrderState state) => state switch
        {
            OrderState.Confirmed => (new CustomerPushText(
                "تم تأكيد طلبك", "طلبك #{code} اتأكد، وهنبلغك أول ما نعيّن مندوب التوصيل.",
                "Order confirmed", "Your order #{code} is confirmed. We'll let you know when a rider is assigned."),
                NotificationType.OrderConfirmed),
            OrderState.OnWay => (new CustomerPushText(
                "طلبك في الطريق", "المندوب في طريقه ليك بطلبك #{code}. تقدر تكلمه من صفحة الطلب.",
                "Your order is on the way", "The rider is on the way with order #{code}. You can call them from the order page."),
                NotificationType.OrderOnWay),
            OrderState.CustomerReceived => (new CustomerPushText(
                "تم استلام طلبك", "استلمت طلبك #{code}. رحلة سعيدة!",
                "Order received", "You received order #{code}. Enjoy your ride!"),
                NotificationType.OrderCustomerReceived),
            OrderState.CustomerRejectedReceipt => (new CustomerPushText(
                "تم تسجيل عدم الاستلام", "اتسجّل إن طلبك #{code} ما اتسلمش، وفريقنا هيتواصل معاك.",
                "Not received", "Order #{code} was marked as not received. Our team will contact you."),
                NotificationType.OrderUpdated),
            OrderState.Completed => (new CustomerPushText(
                "تم إنهاء طلبك", "طلبك #{code} خلص. شكراً لاستخدامك يلا سكوت!",
                "Order completed", "Order #{code} is complete. Thanks for riding with YallaScoot!"),
                NotificationType.OrderCompleted),
            OrderState.Cancelled => (new CustomerPushText(
                "تم إلغاء طلبك", "طلبك #{code} اتلغى.",
                "Order cancelled", "Your order #{code} was cancelled."),
                NotificationType.OrderCancelled),
            _ => null
        };
    }
}
