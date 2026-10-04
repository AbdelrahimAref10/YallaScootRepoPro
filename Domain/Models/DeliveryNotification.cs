using Domain.Common;
using Domain.Enums;

namespace Domain.Models
{
    /// <summary>In-app notification for a rider; mirrors <see cref="MerchantNotification"/>.</summary>
    public class DeliveryNotification : IAuditable
    {
        public int DeliveryNotificationId { get; private set; }
        public int DeliveryId { get; private set; }
        public string Title { get; private set; } = string.Empty;
        public string Message { get; private set; } = string.Empty;
        public int? OrderId { get; private set; }
        public NotificationType NotificationType { get; private set; }
        public bool IsRead { get; private set; }
        public DateTime? ReadAt { get; private set; }

        public Delivery Delivery { get; private set; } = null!;
        public Order? Order { get; private set; }

        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }

        private DeliveryNotification() { }

        public static DeliveryNotification Create(
            int deliveryId,
            string title,
            string message,
            NotificationType notificationType,
            int? orderId = null,
            string? createdBy = null)
        {
            if (deliveryId <= 0)
                throw new ArgumentException("Delivery ID must be greater than zero", nameof(deliveryId));
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title cannot be empty", nameof(title));
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Message cannot be empty", nameof(message));

            return new DeliveryNotification
            {
                DeliveryId = deliveryId,
                Title = title.Trim(),
                Message = message.Trim(),
                NotificationType = notificationType,
                OrderId = orderId,
                IsRead = false,
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };
        }

        public void MarkAsRead()
        {
            if (IsRead)
                return;

            IsRead = true;
            ReadAt = DateTime.UtcNow;
            LastModifiedDate = DateTime.UtcNow;
        }
    }
}
