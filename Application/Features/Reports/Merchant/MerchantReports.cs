using Application.Features.Reports.Admin;
using Domain.Enums;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using K = Domain.Enums.OrderJournalEntryKind;
using D = Domain.Enums.JournalDirection;

namespace Application.Features.Reports.Merchant
{
    /// <summary>The merchant's orders: what he earns on each, the company commission, what was paid and what is left.</summary>
    public class MerchantOrdersReport : IReport
    {
        private readonly DatabaseContext _context;
        public MerchantOrdersReport(DatabaseContext context) => _context = context;

        public string Key => "orders";
        public ReportScope Scope => ReportScope.Merchant;

        public async Task<ReportResultDto> RunAsync(ReportFilter f, ReportContext context, CancellationToken ct)
        {
            var merchantId = context.MerchantId!.Value;
            // Orders with this merchant's vehicles, or with money on this merchant's account.
            var query = _context.Orders.AsNoTracking().Where(o =>
                o.OrderVehicles.Any(v => v.Vehicle.MerchantId == merchantId)
                || _context.OrderJournals.Any(j => j.OrderId == o.OrderId && j.PartyType == LedgerPartyType.Merchant && j.PartyId == merchantId));
            if (f.From.HasValue) query = query.Where(o => o.CreatedDate >= f.From.Value);
            if (f.ToExclusive.HasValue) query = query.Where(o => o.CreatedDate < f.ToExclusive.Value);
            if (ReportFilter.Has(f.OrderStates)) query = query.Where(o => f.OrderStates!.Contains(o.OrderState));
            if (ReportFilter.Has(f.VehicleIds)) query = query.Where(o => o.OrderVehicles.Any(v => f.VehicleIds!.Contains(v.VehicleId)));

            var orders = await query.OrderByDescending(o => o.CreatedDate)
                .Select(o => new
                {
                    o.OrderId,
                    o.OrderCode,
                    o.CreatedDate,
                    o.ReservationDateFrom,
                    o.ReservationDateTo,
                    o.OrderState,
                    Vehicles = o.OrderVehicles.Count(v => v.Vehicle.MerchantId == merchantId)
                })
                .ToListAsync(ct);

            var ids = orders.Select(o => o.OrderId).ToList();
            var journal = (await _context.OrderJournals.AsNoTracking()
                .Where(j => j.PartyType == LedgerPartyType.Merchant && j.PartyId == merchantId && j.OrderId != null && ids.Contains(j.OrderId.Value))
                .GroupBy(j => new { OrderId = j.OrderId!.Value, j.EntryKind, j.Direction })
                .Select(g => new { g.Key.OrderId, g.Key.EntryKind, g.Key.Direction, Amount = g.Sum(x => x.Amount) })
                .ToListAsync(ct)).ToLookup(j => j.OrderId);

            var b = new ReportBuilder(Key, "My orders")
                .Text("orderCode", "Order").DateTime("date", "Date").Date("from", "Rental from").Date("to", "Rental to")
                .Badge("orderState", "Status").Number("vehicles", "My vehicles")
                .Money("rental", "Rental").Money("commission", "Company commission").Money("net", "My net")
                .Money("paid", "Paid to me").Money("remaining", "Remaining");

            foreach (var o in orders)
            {
                var lines = journal[o.OrderId].ToList();
                decimal Sum(K kind, D dir) => lines.Where(l => l.EntryKind == kind && l.Direction == dir).Sum(l => l.Amount);
                var rental = Sum(K.MerchantRentalAccrued, D.Credit);
                var commission = Sum(K.MerchantCompanyCommissionCharged, D.Debit);
                var remaining = lines.Sum(l => l.Direction == D.Credit ? l.Amount : -l.Amount);
                b.Row(new()
                {
                    ["orderCode"] = o.OrderCode,
                    ["date"] = o.CreatedDate,
                    ["from"] = o.ReservationDateFrom,
                    ["to"] = o.ReservationDateTo,
                    ["orderState"] = o.OrderState.ToString(),
                    ["vehicles"] = o.Vehicles,
                    ["rental"] = rental,
                    ["commission"] = commission,
                    ["net"] = rental - commission,
                    ["paid"] = Sum(K.MerchantPaidByCompany, D.Debit),
                    ["remaining"] = remaining
                });
            }

            return b.Kpi("orders", "Orders", orders.Count, ReportColumnType.Number)
                .Kpi("net", "My net", b.Sum("net"))
                .Kpi("paidToMe", "Paid to me", b.Sum("paid"))
                .Kpi("remaining", "Remaining", b.Sum("remaining"))
                .Build();
        }
    }

    /// <summary>
    /// One row per vehicle on each of the merchant's orders: its daily price, the company's percent and cut,
    /// and the merchant's net. Prices come from <c>OrderVehicle.DailyPrice</c> and, once the order is Confirmed,
    /// amounts and percent from the payout snapshot, so later price or percent changes don't alter old orders.
    /// </summary>
    public class MerchantOrderVehiclesReport : IReport
    {
        private readonly DatabaseContext _context;
        public MerchantOrderVehiclesReport(DatabaseContext context) => _context = context;

        public string Key => "order-vehicles";
        public ReportScope Scope => ReportScope.Merchant;

        public async Task<ReportResultDto> RunAsync(ReportFilter f, ReportContext context, CancellationToken ct)
        {
            var merchantId = context.MerchantId!.Value;
            var query = _context.OrderVehicles.AsNoTracking().Where(ov =>
                ov.Vehicle.MerchantId == merchantId && ov.MerchantResponseStatus != MerchantVehicleResponseStatus.Declined);
            if (f.From.HasValue) query = query.Where(ov => ov.Order.CreatedDate >= f.From.Value);
            if (f.ToExclusive.HasValue) query = query.Where(ov => ov.Order.CreatedDate < f.ToExclusive.Value);
            if (ReportFilter.Has(f.OrderStates)) query = query.Where(ov => f.OrderStates!.Contains(ov.Order.OrderState));
            if (ReportFilter.Has(f.VehicleIds)) query = query.Where(ov => f.VehicleIds!.Contains(ov.VehicleId));

            var lines = await query.OrderByDescending(ov => ov.Order.CreatedDate).ThenBy(ov => ov.Vehicle.Name)
                .Select(ov => new
                {
                    ov.OrderId,
                    ov.Order.OrderCode,
                    ov.Order.CreatedDate,
                    ov.Order.ReservationDateFrom,
                    ov.Order.ReservationDateTo,
                    ov.Order.OrderState,
                    ov.VehicleId,
                    ov.Vehicle.VehicleCode,
                    Vehicle = ov.Vehicle.Name,
                    ov.DailyPrice
                })
                .ToListAsync(ct);

            var orderIds = lines.Select(l => l.OrderId).Distinct().ToList();
            var snapshots = await _context.MerchantOrderPaymentDetails.AsNoTracking()
                .Where(p => p.MerchantId == merchantId && orderIds.Contains(p.OrderId))
                .ToDictionaryAsync(p => (p.OrderId, p.VehicleId), ct);
            // Until the order is Confirmed the percent is not fixed yet: use the merchant's current one.
            var currentPercent = await _context.Merchants.AsNoTracking()
                .Where(m => m.MerchantId == merchantId)
                .Select(m => m.CompanyCommissionPercent)
                .FirstAsync(ct);

            var b = new ReportBuilder(Key, "My orders by vehicle")
                .Text("orderCode", "Order").DateTime("date", "Date").Badge("orderState", "Status")
                .Text("vehicleCode", "Code").Text("vehicle", "Vehicle")
                .Money("dailyPrice", "Daily price", total: false).Number("days", "Days", total: false).Money("rental", "Rental")
                .Text("companyPercent", "Yalla Scoot %").Money("companyProfit", "Yalla Scoot profit").Money("net", "My net");

            foreach (var l in lines)
            {
                var days = Domain.Models.Order.InclusiveReservationDays(l.ReservationDateFrom, l.ReservationDateTo);
                decimal rental, percent, commission;
                if (snapshots.TryGetValue((l.OrderId, l.VehicleId), out var s))
                {
                    rental = s.VehicleRental;
                    percent = s.CompanyCommissionPercent;
                    commission = s.CompanyCommissionAmount;
                }
                else
                {
                    rental = l.DailyPrice * days;
                    percent = currentPercent;
                    commission = Domain.Models.MerchantOrderPaymentDetail.ComputeCompanyCommission(rental, percent);
                }

                b.Row(new()
                {
                    ["orderCode"] = l.OrderCode,
                    ["date"] = l.CreatedDate,
                    ["orderState"] = l.OrderState.ToString(),
                    ["vehicleCode"] = l.VehicleCode,
                    ["vehicle"] = l.Vehicle,
                    ["dailyPrice"] = l.DailyPrice,
                    ["days"] = days,
                    ["rental"] = rental,
                    ["companyPercent"] = $"{percent:0.##}%",
                    ["companyProfit"] = commission,
                    ["net"] = rental - commission
                });
            }

            return b.Kpi("orders", "Orders", orderIds.Count, ReportColumnType.Number)
                .Kpi("vehicles", "Vehicles", lines.Count, ReportColumnType.Number)
                .Kpi("companyProfit", "Yalla Scoot profit", b.Sum("companyProfit"))
                .Kpi("net", "My net", b.Sum("net"))
                .Build();
        }
    }

    /// <summary>The merchant's account statement with a running balance.</summary>
    public class MerchantStatementReport : IReport
    {
        private readonly DatabaseContext _context;
        public MerchantStatementReport(DatabaseContext context) => _context = context;

        public string Key => "statement";
        public ReportScope Scope => ReportScope.Merchant;

        public async Task<ReportResultDto> RunAsync(ReportFilter f, ReportContext context, CancellationToken ct)
        {
            var merchant = await _context.Merchants.AsNoTracking()
                .Where(m => m.MerchantId == context.MerchantId)
                .Select(m => new StatementReportBuilder.Party(LedgerPartyType.Merchant, m.MerchantId, m.FullName))
                .FirstAsync(ct);
            var result = await StatementReportBuilder.BuildAsync(_context, Key, new[] { merchant }, f, ct);
            result.Title = "My account statement";

            // Single party: drop the party column and word the totals from the merchant's side.
            result.Columns.RemoveAll(c => c.Key == "party");
            foreach (var row in result.Rows)
                row.Remove("party");
            foreach (var kpi in result.Kpis)
            {
                var key = kpi.LabelKey switch { "reports.kpi.credit" => "myCredit", "reports.kpi.debit" => "myDebit", _ => null };
                if (key == null) continue;
                kpi.LabelKey = $"reports.kpi.{key}";
                kpi.Label = key == "myCredit" ? "Credit (owed to me)" : "Debit (owed by me)";
            }
            return result;
        }
    }

    /// <summary>Payments the company made to the merchant (settlement vouchers).</summary>
    public class MerchantPaymentsReport : IReport
    {
        private readonly VouchersReport _vouchers;
        public MerchantPaymentsReport(DatabaseContext context) => _vouchers = new VouchersReport(context);

        public string Key => "payments";
        public ReportScope Scope => ReportScope.Merchant;

        public async Task<ReportResultDto> RunAsync(ReportFilter f, ReportContext context, CancellationToken ct)
        {
            var result = await _vouchers.RunAsync(new ReportFilter { FromDate = f.FromDate, ToDate = f.ToDate }, context, ct);
            result.Key = Key;
            result.Title = "Payments received";
            // The party columns are always this merchant.
            // Every voucher is a payment to this merchant, so party / received columns add nothing.
            string[] hidden = { "partyType", "party", "direction", "received", "createdBy" };
            result.Columns.RemoveAll(c => hidden.Contains(c.Key));
            foreach (var row in result.Rows)
                foreach (var key in hidden)
                    row.Remove(key);
            result.Totals.Remove("received");
            result.Kpis.RemoveAll(k => k.LabelKey is "reports.kpi.received" or "reports.kpi.net");
            return result;
        }
    }

    /// <summary>Per vehicle: rentals, revenue, company commission and net in the period.</summary>
    public class MerchantVehiclesReport : IReport
    {
        private readonly DatabaseContext _context;
        public MerchantVehiclesReport(DatabaseContext context) => _context = context;

        public string Key => "vehicles";
        public ReportScope Scope => ReportScope.Merchant;

        public async Task<ReportResultDto> RunAsync(ReportFilter f, ReportContext context, CancellationToken ct)
        {
            var merchantId = context.MerchantId!.Value;
            var vehicles = _context.Vehicles.AsNoTracking().Where(v => v.MerchantId == merchantId);
            if (ReportFilter.Has(f.VehicleIds)) vehicles = vehicles.Where(v => f.VehicleIds!.Contains(v.VehicleId));
            var list = await vehicles.OrderBy(v => v.Name)
                .Select(v => new { v.VehicleId, v.VehicleCode, v.Name, SubCategory = v.SubCategory.Name, v.Status })
                .ToListAsync(ct);
            var ids = list.Select(v => v.VehicleId).ToList();

            var journal = _context.OrderJournals.AsNoTracking()
                .Where(j => j.PartyType == LedgerPartyType.Merchant && j.PartyId == merchantId && j.VehicleId != null && ids.Contains(j.VehicleId.Value));
            if (f.From.HasValue) journal = journal.Where(j => j.CreatedDate >= f.From.Value);
            if (f.ToExclusive.HasValue) journal = journal.Where(j => j.CreatedDate < f.ToExclusive.Value);
            var sums = (await journal
                .GroupBy(j => new { VehicleId = j.VehicleId!.Value, j.EntryKind, j.Direction })
                .Select(g => new { g.Key.VehicleId, g.Key.EntryKind, g.Key.Direction, Amount = g.Sum(x => x.Amount), Orders = g.Select(x => x.OrderId).Distinct().Count() })
                .ToListAsync(ct)).ToLookup(s => s.VehicleId);

            var b = new ReportBuilder(Key, "My vehicles")
                .Text("vehicleCode", "Code").Text("vehicle", "Vehicle").Text("subCategory", "Sub category").Badge("status", "Status")
                .Number("rentals", "Rentals").Money("rental", "Rental").Money("commission", "Company commission").Money("net", "My net");

            foreach (var v in list)
            {
                var s = sums[v.VehicleId].ToList();
                var rentalLines = s.Where(x => x.EntryKind == K.MerchantRentalAccrued && x.Direction == D.Credit).ToList();
                var rental = rentalLines.Sum(x => x.Amount);
                var commission = s.Where(x => x.EntryKind == K.MerchantCompanyCommissionCharged && x.Direction == D.Debit).Sum(x => x.Amount);
                b.Row(new()
                {
                    ["vehicleCode"] = v.VehicleCode,
                    ["vehicle"] = v.Name,
                    ["subCategory"] = v.SubCategory,
                    ["status"] = v.Status.ToString(),
                    ["rentals"] = rentalLines.Sum(x => x.Orders),
                    ["rental"] = rental,
                    ["commission"] = commission,
                    ["net"] = rental - commission
                });
            }

            return b.Kpi("vehicles", "Vehicles", list.Count, ReportColumnType.Number)
                .Kpi("rentals", "Rentals", b.Sum("rentals"), ReportColumnType.Number)
                .Kpi("net", "My net", b.Sum("net"))
                .Kpi("companyCommission", "Company commission", b.Sum("commission"))
                .Build();
        }
    }
}
