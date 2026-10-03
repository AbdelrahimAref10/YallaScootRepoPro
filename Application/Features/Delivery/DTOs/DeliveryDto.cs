namespace Application.Features.Delivery.DTOs
{
    public class DeliveryDto
    {
        public int DeliveryId { get; set; }
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public int CityId { get; set; }
        public int ZoneId { get; set; }
        public string? CityName { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? PersonalImage { get; set; }
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedDate { get; set; }

        /// <summary>Cash collected from customers and not remitted yet.</summary>
        public decimal CashDebt { get; set; }

        /// <summary>Null = no limit. At or above it the rider cannot take cash orders.</summary>
        public decimal? CashDebtLimit { get; set; }
    }
}
