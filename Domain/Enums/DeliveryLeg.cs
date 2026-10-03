namespace Domain.Enums
{
    /// <summary>
    /// A rental vehicle's cycle is two trips, and each can go to a different rider:
    /// Delivery = merchant → customer, Return = customer → merchant.
    /// </summary>
    public enum DeliveryLeg
    {
        Delivery = 1,
        Return = 2
    }
}
