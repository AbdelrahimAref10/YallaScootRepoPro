using Domain.Common;
using Domain.Enums;

namespace Domain.Models
{
    /// <summary>What was pushed to a customer, in the language it was sent in, with read state.</summary>
    public class CustomerNotification : IAuditable
    {
        public int CustomerNotificationId { get; private set; }
        public int CustomerId { get; private set; }
        public string Title { get; private set; } = string.Empty;
        public string Message { get; private set; } = string.Empty;
        public int? OrderId { get; private set; }
        public NotificationType NotificationType { get; private set; }
        public bool IsRead { get; private set; }
        public DateTime? ReadAt { get; private set; }

        public Customer Customer { get; private set; } = null!;
        public Order? Order { get; private set; }

        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }

        private CustomerNotification() { }

        public static CustomerNotification Create(
            int customerId,
            string title,
            string message,
            NotificationType notificationType,
            int? orderId = null,
            string? createdBy = null)
        {
            if (customerId <= 0)
                throw new ArgumentException("Customer ID must be greater than zero", nameof(customerId));
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title cannot be empty", nameof(title));
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Message cannot be empty", nameof(message));

            return new CustomerNotification
            {
                CustomerId = customerId,
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
