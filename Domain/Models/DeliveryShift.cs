using Domain.Common;

namespace Domain.Models
{
    /// <summary>A rider belongs to a shift. A rider can be in several shifts.</summary>
    public class DeliveryShift : IAuditable
    {
        public int DeliveryShiftId { get; private set; }
        public int DeliveryId { get; private set; }
        public int ShiftId { get; private set; }

        public Delivery Delivery { get; private set; } = null!;
        public Shift Shift { get; private set; } = null!;

        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }

        private DeliveryShift() { }

        public static DeliveryShift Create(int deliveryId, int shiftId, string? createdBy = null)
        {
            if (deliveryId <= 0)
                throw new ArgumentException("Delivery ID must be greater than zero", nameof(deliveryId));
            if (shiftId <= 0)
                throw new ArgumentException("Shift ID must be greater than zero", nameof(shiftId));

            return new DeliveryShift
            {
                DeliveryId = deliveryId,
                ShiftId = shiftId,
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };
        }
    }
}
