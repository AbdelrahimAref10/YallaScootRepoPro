namespace Application.Features.Customer.DTOs
{
    public class DeliveryDeviceTokensDto
    {
        public string? AndriodDevice { get; set; }
        public string? IosDevice { get; set; }
        /// <summary>App language, "ar" or "en". Falls back to the Accept-Language header.</summary>
        public string? Language { get; set; }
    }
}


