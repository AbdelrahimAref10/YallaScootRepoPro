namespace Domain.Enums
{
    /// <summary>The four vehicle handovers, in the order they happen.</summary>
    public enum HandoverStep
    {
        ReceivedFromOwner = 1,
        DeliveredToCustomer = 2,
        ReceivedFromCustomer = 3,
        DeliveredToOwner = 4
    }

    /// <summary>The four mandatory photo angles taken at every handover.</summary>
    public enum HandoverImagePosition
    {
        Front = 1,
        Back = 2,
        Left = 3,
        Right = 4
    }

    public static class HandoverStepExtensions
    {
        public static DeliveryLeg Leg(this HandoverStep step) =>
            step is HandoverStep.ReceivedFromOwner or HandoverStep.DeliveredToCustomer
                ? DeliveryLeg.Delivery
                : DeliveryLeg.Return;
    }
}
