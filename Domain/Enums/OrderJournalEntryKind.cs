namespace Domain.Enums
{
    public enum OrderJournalEntryKind
    {
        MerchantRentalAccrued = 1,
        MerchantPaidByDeliveryCashOnReceive = 2,
        DeliveryCashAdvanceToMerchant = 3,
        DeliveryFeeAccrued = 4,
        CashCollectedFromCustomer = 5,
        CompanyServiceFeeAccrued = 6,
        DeliveryRemittanceToCompany = 7,
        MerchantPaidByCompany = 8,
        DeliveryPaidByCompany = 9,
        FaultClawback = 10,
        CustomerRejectedReceiptNote = 11,
        /// <summary>Delivery received cash float from company (عليه) — OrderId null.</summary>
        DeliveryCashFloatReceived = 12,
        /// <summary>Delivery returned unused float to company (ليه/تصفير) — OrderId null.</summary>
        DeliveryCashFloatReturned = 13,
        /// <summary>Company debit for full order total (cash first customer delivery, or PayPal capture).</summary>
        OrderTotalDebitedToCompany = 14,
        /// <summary>Fault party debit after non-delivery (vehicle or whole order).</summary>
        NonDeliveryFaultDebit = 15,
        /// <summary>Company credit for the vehicle delivery fee minus the delivery-leg rider commission (on customer delivery).</summary>
        CompanyDeliveryFeeRemainderAccrued = 16,
        /// <summary>Company debit for the return-leg rider commission (posted with that rider's credit).</summary>
        CompanyReturnLegCommissionCharged = 17,
        /// <summary>Merchant debit for the company's percentage of the vehicle rental (on customer delivery).</summary>
        MerchantCompanyCommissionCharged = 18,
        /// <summary>Company credit for its percentage of the merchant's vehicle rental (on customer delivery).</summary>
        CompanyMerchantCommissionAccrued = 19,
        /// <summary>Company debit: cash received from a delivery (pairs with <see cref="DeliveryRemittanceToCompany"/> or <see cref="DeliveryCashFloatReturned"/>).</summary>
        CompanyCashReceivedFromDelivery = 20,
        /// <summary>Company credit: money paid to a merchant (pairs with <see cref="MerchantPaidByCompany"/>).</summary>
        CompanyPaidMerchant = 21,
        /// <summary>Company credit: commission paid to a delivery (pairs with <see cref="DeliveryPaidByCompany"/>).</summary>
        CompanyPaidDelivery = 22
    }
}
