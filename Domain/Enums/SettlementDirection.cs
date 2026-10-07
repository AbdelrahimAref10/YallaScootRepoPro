namespace Domain.Enums
{
    /// <summary>Which way cash moved in a settlement voucher.</summary>
    public enum SettlementDirection
    {
        /// <summary>The company received cash from the party (receipt).</summary>
        CollectFromParty = 1,
        /// <summary>The company paid cash to the party (payment).</summary>
        PayToParty = 2
    }

    /// <summary>What a voucher allocation settled.</summary>
    public enum SettlementAllocationKind
    {
        /// <summary>Customer cash the delivery collected on an order.</summary>
        DeliveryCash = 1,
        /// <summary>Delivery commission on an order.</summary>
        DeliveryCommission = 2,
        /// <summary>Merchant earnings on an order.</summary>
        MerchantEarnings = 3,
        /// <summary>Old cash float the delivery still held (no order).</summary>
        DeliveryLegacyFloat = 4
    }
}
