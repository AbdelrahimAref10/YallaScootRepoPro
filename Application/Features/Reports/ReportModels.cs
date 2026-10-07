using Domain.Enums;

namespace Application.Features.Reports
{
    public enum ReportColumnType
    {
        Text = 1,
        Number = 2,
        Money = 3,
        Date = 4,
        DateTime = 5,
        /// <summary>Enum-like value shown as a chip; the client translates "{ValueKeyPrefix}{value}".</summary>
        Badge = 6
    }

    public enum ReportScope
    {
        Admin = 1,
        Merchant = 2
    }

    public class ReportColumnDto
    {
        public string Key { get; set; } = string.Empty;
        /// <summary>English label (used in exports and as the UI fallback).</summary>
        public string Label { get; set; } = string.Empty;
        /// <summary>i18n key for the UI label.</summary>
        public string LabelKey { get; set; } = string.Empty;
        public ReportColumnType Type { get; set; }
        /// <summary>Sum this column into <see cref="ReportResultDto.Totals"/>.</summary>
        public bool Total { get; set; }
        /// <summary>For badges: i18n prefix of the cell value.</summary>
        public string? ValueKeyPrefix { get; set; }
    }

    public class ReportKpiDto
    {
        public string Label { get; set; } = string.Empty;
        public string LabelKey { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public ReportColumnType Type { get; set; } = ReportColumnType.Money;
    }

    /// <summary>A generic table: every report returns this shape, so one page and one exporter serve all.</summary>
    public class ReportResultDto
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public List<ReportColumnDto> Columns { get; set; } = new();
        public List<Dictionary<string, object?>> Rows { get; set; } = new();
        public Dictionary<string, decimal> Totals { get; set; } = new();
        public List<ReportKpiDto> Kpis { get; set; } = new();
    }

    /// <summary>All report filters. Each report reads the ones it supports; lists are multi-select.</summary>
    public class ReportFilter
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public List<int>? CityIds { get; set; }
        public List<int>? MerchantIds { get; set; }
        public List<int>? DeliveryIds { get; set; }
        public List<int>? CustomerIds { get; set; }
        public List<int>? VehicleIds { get; set; }
        public List<OrderState>? OrderStates { get; set; }
        public List<PaymentMethod>? PaymentMethods { get; set; }
        public List<PaymentState>? PaymentStates { get; set; }
        public List<RefundState>? RefundStates { get; set; }
        public List<SettlementDirection>? Directions { get; set; }
        public List<LedgerPartyType>? PartyTypes { get; set; }

        /// <summary>Inclusive start of the From day.</summary>
        internal DateTime? From => FromDate?.Date;
        /// <summary>Exclusive end: the day after To.</summary>
        internal DateTime? ToExclusive => ToDate?.Date.AddDays(1);

        public static bool Has<T>(List<T>? values) => values is { Count: > 0 };
    }

    /// <summary>Who is running the report (merchant reports are limited to the merchant).</summary>
    public sealed record ReportContext(ReportScope Scope, int? MerchantId);

    public interface IReport
    {
        string Key { get; }
        ReportScope Scope { get; }
        Task<ReportResultDto> RunAsync(ReportFilter filter, ReportContext context, CancellationToken cancellationToken);
    }
}
