using Application.Common.DomainEvents;
using Domain.Enums;
using Domain.Events;
using Infrastructure;
using Infrastructure.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Features.Customer.Common
{
    /// <summary>Push to the customer who owns an order. Never throws.</summary>
    public interface ICustomerNotifier
    {
        Task NotifyOrderAsync(
            int orderId,
            string title,
            string body,
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
            string title,
            string body,
            NotificationType type,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var target = await _context.Orders
                    .AsNoTracking()
                    .Where(o => o.OrderId == orderId)
                    .Select(o => new { o.OrderCode, o.Customer.AndriodDevice, o.Customer.IosDevice })
                    .FirstOrDefaultAsync(cancellationToken);

                if (target == null)
                    return;

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
                    Body = body.Replace("{code}", target.OrderCode),
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
                _logger.LogError(ex, "Failed to push order {OrderId} to its customer", orderId);
            }
        }
    }

    /// <summary>Tells the customer about every order state change, whoever made it (admin, merchant, rider).</summary>
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
                : _notifier.NotifyOrderAsync(e.OrderId, message.Value.Title, message.Value.Body, message.Value.Type, cancellationToken);
        }

        /// <summary>
        /// DeliveryAssigned is skipped: the assign command pushes "rider assigned" with the rider's name.
        /// <c>{code}</c> is replaced with the order code.
        /// </summary>
        private static (string Title, string Body, NotificationType Type)? MessageFor(OrderState state) => state switch
        {
            OrderState.MerchantPending => ("طلبك قيد المراجعة", "بنأكد توفر المركبات لطلبك #{code} مع التاجر.", NotificationType.OrderMerchantPending),
            OrderState.MerchantConfirmed => ("المركبات متاحة", "التاجر أكد توفر المركبات لطلبك #{code}.", NotificationType.OrderUpdated),
            OrderState.Confirmed => ("تم تأكيد طلبك", "طلبك #{code} اتأكد، وهنبلغك أول ما نعيّن مندوب التوصيل.", NotificationType.OrderConfirmed),
            OrderState.OnWay => ("طلبك في الطريق", "المندوب في طريقه ليك بطلبك #{code}.", NotificationType.OrderOnWay),
            OrderState.CustomerReceived => ("تم استلام طلبك", "استلمت طلبك #{code}. رحلة سعيدة!", NotificationType.OrderCustomerReceived),
            OrderState.CustomerRejectedReceipt => ("تم تسجيل عدم الاستلام", "اتسجّل إن طلبك #{code} ما اتسلمش، وفريقنا هيتواصل معاك.", NotificationType.OrderUpdated),
            OrderState.Completed => ("تم إنهاء طلبك", "طلبك #{code} خلص. شكراً لاستخدامك يلا سكوت!", NotificationType.OrderCompleted),
            OrderState.Cancelled => ("تم إلغاء طلبك", "طلبك #{code} اتلغى.", NotificationType.OrderCancelled),
            _ => null
        };
    }
}
