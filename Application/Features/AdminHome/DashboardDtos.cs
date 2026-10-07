using Domain.Enums;

namespace Application.Features.AdminHome
{
    public enum DashboardBucket
    {
        Day = 1,
        Week = 2,
        Month = 3
    }

    /// <summary>Everything the admin home page shows, for one period and city selection.</summary>
    public class DashboardDto
    {
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public DateTime PreviousFrom { get; set; }
        public DateTime PreviousTo { get; set; }
        public DashboardBucket Bucket { get; set; }
        public DateTime GeneratedAt { get; set; }

        public DashboardProfitDto Profit { get; set; } = new();
        public DashboardPositionDto Position { get; set; } = new();
        public List<DashboardTrendPointDto> Trend { get; set; } = new();
        public DashboardOrdersDto Orders { get; set; } = new();
        public DashboardPaymentsDto Payments { get; set; } = new();
        public DashboardFleetDto Fleet { get; set; } = new();
        public DashboardPeopleDto People { get; set; } = new();
        public List<DashboardRankDto> TopMerchants { get; set; } = new();
        public List<DashboardRankDto> TopDeliveries { get; set; } = new();
        public List<DashboardRankDto> TopCities { get; set; } = new();
        public List<DashboardRecentOrderDto> RecentOrders { get; set; } = new();
    }

    public class DashboardAmountDto
    {
        /// <summary>Stable key; the client translates it.</summary>
        public string Key { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Count { get; set; }
    }

    /// <summary>The company's own earnings: what it keeps after merchants and riders are credited.</summary>
    public class DashboardProfitDto
    {
        public decimal NetProfit { get; set; }
        public decimal PreviousNetProfit { get; set; }
        public decimal NetProfitChangePercent { get; set; }
        /// <summary>Gross bookings: total of non-cancelled orders created in the period.</summary>
        public decimal GrossBookings { get; set; }
        public decimal PreviousGrossBookings { get; set; }
        public decimal GrossBookingsChangePercent { get; set; }
        /// <summary>Net profit as a share of gross bookings.</summary>
        public decimal MarginPercent { get; set; }
        public List<DashboardAmountDto> Income { get; set; } = new();
        public List<DashboardAmountDto> Costs { get; set; } = new();
    }

    /// <summary>Balances at the end of the period: what others owe the company and what it owes them.</summary>
    public class DashboardPositionDto
    {
        public decimal OwedToCompany { get; set; }
        public decimal CompanyOwes { get; set; }
        public decimal NetPosition { get; set; }
        public decimal TreasuryBalance { get; set; }
        public List<DashboardAmountDto> OwedToCompanyItems { get; set; } = new();
        public List<DashboardAmountDto> CompanyOwesItems { get; set; } = new();
        public List<DashboardPartyDto> TopDebtors { get; set; } = new();
        public List<DashboardPartyDto> TopCreditors { get; set; } = new();
    }

    public class DashboardPartyDto
    {
        public LedgerPartyType PartyType { get; set; }
        public int PartyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class DashboardTrendPointDto
    {
        public DateTime Date { get; set; }
        public decimal NetProfit { get; set; }
        public decimal GrossBookings { get; set; }
        public int Orders { get; set; }
        public int Completed { get; set; }
        public int Cancelled { get; set; }
    }

    public class DashboardOrdersDto
    {
        public int Total { get; set; }
        public int PreviousTotal { get; set; }
        public decimal TotalChangePercent { get; set; }
        public int Completed { get; set; }
        public int Cancelled { get; set; }
        public decimal CompletionRatePercent { get; set; }
        public decimal CancellationRatePercent { get; set; }
        public int Urgent { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal AverageProfitPerOrder { get; set; }
        /// <summary>Open orders right now (not limited to the period), by state.</summary>
        public List<DashboardStateCountDto> Pipeline { get; set; } = new();
        public int OpenNow { get; set; }
        public int WithCustomersNow { get; set; }
    }

    public class DashboardStateCountDto
    {
        public OrderState State { get; set; }
        public int Count { get; set; }
    }

    public class DashboardPaymentsDto
    {
        public decimal CashPaid { get; set; }
        public decimal PayPalPaid { get; set; }
        public decimal Pending { get; set; }
        public decimal Failed { get; set; }
        public decimal Refunded { get; set; }
        public int PaidCount { get; set; }
    }

    public class DashboardFleetDto
    {
        public int Total { get; set; }
        public int Available { get; set; }
        public int Rented { get; set; }
        public int UnderMaintenance { get; set; }
        public decimal UtilizationPercent { get; set; }
    }

    public class DashboardPeopleDto
    {
        public int Customers { get; set; }
        public int NewCustomers { get; set; }
        public decimal NewCustomersChangePercent { get; set; }
        public int ActiveMerchants { get; set; }
        public int ActiveDeliveries { get; set; }
        public int OnlineDeliveries { get; set; }
    }

    public class DashboardRankDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Count { get; set; }
    }

    public class DashboardRecentOrderDto
    {
        public int OrderId { get; set; }
        public string OrderCode { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public OrderState State { get; set; }
        public decimal Total { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
