using Domain.Enums;

namespace Application.Features.Customer.DTOs
{
    /// <summary>A push sent to the customer (VO_CustomerNotification), in the language it was sent in.</summary>
    public class CustomerNotificationDto
    {
        public int CustomerNotificationId { get; set; }
        public int CustomerId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int? OrderId { get; set; }
        public string? OrderCode { get; set; }
        public NotificationType NotificationType { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
