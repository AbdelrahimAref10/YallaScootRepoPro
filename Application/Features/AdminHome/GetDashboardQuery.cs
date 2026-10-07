using CSharpFunctionalExtensions;
using Domain.Common;
using Domain.Enums;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using K = Domain.Enums.OrderJournalEntryKind;
using D = Domain.Enums.JournalDirection;

namespace Application.Features.AdminHome
{
    /// <summary>Admin home page. Dates are whole days (To inclusive); empty cities means all.</summary>
    public record GetDashboardQuery : IRequest<Result<DashboardDto>>
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public List<int>? CityIds { get; set; }
    }

    public class GetDashboardQueryHandler : IRequestHandler<GetDashboardQuery, Result<DashboardDto>>
    {
        /// <summary>Short enough to feel live, long enough that reloads and several admins share one computation.</summary>
        private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);
        private const int MaxDays = 3 * 366;
        private const int TopCount = 5;

        // Company journal lines that make up its profit (same formula as the orders report).
        private static readonly K[] IncomeKinds = { K.CompanyServiceFeeAccrued, K.CompanyDeliveryFeeRemainderAccrued, K.CompanyMerchantCommissionAccrued };
        private static readonly K[] ProfitKinds = { K.CompanyServiceFeeAccrued, K.CompanyDeliveryFeeRemainderAccrued, K.CompanyMerchantCommissionAccrued, K.CompanyReturnLegCommissionCharged };

        private static readonly OrderState[] OpenStates =
        {
            OrderState.Pending, OrderState.MerchantPending, OrderState.MerchantConfirmed, OrderState.Confirmed,
            OrderState.DeliveryAssigned, OrderState.OnWay, OrderState.CustomerReceived
        };

        private readonly DatabaseContext _context;
        private readonly IDateTimeProvider _clock;
        private readonly IMemoryCache _cache;

        public GetDashboardQueryHandler(DatabaseContext context, IDateTimeProvider clock, IMemoryCache cache)
        {
            _context = context;
            _clock = clock;
            _cache = cache;
        }

        public async Task<Result<DashboardDto>> Handle(GetDashboardQuery request, CancellationToken ct)
        {
            var today = _clock.Now.Date;
            var from = (request.FromDate ?? today.AddDays(-29)).Date;
            var to = (request.ToDate ?? today).Date;
            if (from > to)
                return Result.Failure<DashboardDto>("From date must be before To date");
            if ((to - from).TotalDays > MaxDays)
                return Result.Failure<DashboardDto>("The period cannot be longer than 3 years");

            var cities = request.CityIds?.Where(id => id > 0).Distinct().OrderBy(id => id).ToList() ?? new List<int>();
            var key = $"admin-dashboard:{from:yyyyMMdd}:{to:yyyyMMdd}:{string.Join(',', cities)}";
            if (_cache.TryGetValue(key, out DashboardDto? cached) && cached != null)
                return Result.Success(cached);

            var dto = await BuildAsync(from, to, cities, ct);
            _cache.Set(key, dto, CacheFor);
            return Result.Success(dto);
        }

        private async Task<DashboardDto> BuildAsync(DateTime from, DateTime to, List<int> cities, CancellationToken ct)
        {
            var end = to.AddDays(1);              // exclusive
            var length = end - from;
            var prevFrom = from - length;          // previous period of the same length
            var byCity = cities.Count > 0;
            var bucket = length.TotalDays <= 31 ? DashboardBucket.Day
                : length.TotalDays <= 184 ? DashboardBucket.Week
                : DashboardBucket.Month;

            // ---------- Orders: per day and state, current + previous period in one pass ----------
            var orders = _context.Orders.AsNoTracking();
            if (byCity) orders = orders.Where(o => cities.Contains(o.CityId));

            var orderDays = await orders
                .Where(o => o.CreatedDate >= prevFrom && o.CreatedDate < end)
                .GroupBy(o => new { Day = o.CreatedDate.Date, o.OrderState })
                .Select(g => new
                {
                    g.Key.Day,
                    g.Key.OrderState,
                    Count = g.Count(),
                    Urgent = g.Count(o => o.IsUrgent),
                    Total = g.Sum(o => o.OrderTotal)
                })
                .ToListAsync(ct);

            var pipeline = await orders
                .Where(o => OpenStates.Contains(o.OrderState))
                .GroupBy(o => o.OrderState)
                .Select(g => new DashboardStateCountDto { State = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var payments = await orders
                .Where(o => o.CreatedDate >= from && o.CreatedDate < end)
                .SelectMany(o => o.OrderPayments)
                .GroupBy(p => new { p.PaymentMethodId, p.State })
                .Select(g => new { g.Key.PaymentMethodId, g.Key.State, Count = g.Count(), Total = g.Sum(p => p.Total) })
                .ToListAsync(ct);

            var recent = await orders
                .OrderByDescending(o => o.CreatedDate)
                .Take(6)
                .Select(o => new DashboardRecentOrderDto
                {
                    OrderId = o.OrderId,
                    OrderCode = o.OrderCode,
                    Customer = o.Customer.FullName,
                    City = o.City.Name,
                    State = o.OrderState,
                    Total = o.OrderTotal,
                    CreatedDate = o.CreatedDate
                })
                .ToListAsync(ct);

            // ---------- Company profit: journal per day and kind ----------
            var companyLines = _context.OrderJournals.AsNoTracking()
                .Where(j => j.PartyType == LedgerPartyType.Company && ProfitKinds.Contains(j.EntryKind));
            if (byCity) companyLines = companyLines.Where(j => j.Order != null && cities.Contains(j.Order.CityId));

            var profitDays = await companyLines
                .Where(j => j.CreatedDate >= prevFrom && j.CreatedDate < end)
                .GroupBy(j => new { Day = j.CreatedDate.Date, j.EntryKind, j.Direction })
                .Select(g => new { g.Key.Day, g.Key.EntryKind, g.Key.Direction, Amount = g.Sum(j => j.Amount) })
                .ToListAsync(ct);

            var profitByCity = await companyLines
                .Where(j => j.CreatedDate >= from && j.CreatedDate < end && j.Order != null)
                .GroupBy(j => j.Order!.CityId)
                .Select(g => new
                {
                    CityId = g.Key,
                    Profit = g.Sum(j => j.Direction == D.Credit ? j.Amount : -j.Amount),
                    Orders = g.Select(j => j.OrderId).Distinct().Count()
                })
                .ToListAsync(ct);

            // Cancellation fees charged to customers are company income too.
            var fees = _context.CustomerWallets.AsNoTracking().Where(w => w.Type == WalletType.OrderCancellationFees);
            if (byCity) fees = fees.Where(w => cities.Contains(w.Customer.CityId));

            var feeDays = await fees
                .Where(w => w.CreatedDate >= prevFrom && w.CreatedDate < end)
                .GroupBy(w => w.CreatedDate.Date)
                .Select(g => new { Day = g.Key, Count = g.Count(), Amount = g.Sum(w => w.Withdraw - w.Deposit) })
                .ToListAsync(ct);

            // ---------- Balances at the end of the period ----------
            var partyBalances = await _context.OrderJournals.AsNoTracking()
                .Where(j => (j.PartyType == LedgerPartyType.Merchant || j.PartyType == LedgerPartyType.Delivery)
                            && j.PartyId != null && j.CreatedDate < end)
                .GroupBy(j => new { j.PartyType, PartyId = j.PartyId!.Value })
                .Select(g => new
                {
                    g.Key.PartyType,
                    g.Key.PartyId,
                    Balance = g.Sum(j => j.Direction == D.Credit ? j.Amount : -j.Amount)
                })
                .Where(x => x.Balance != 0)
                .ToListAsync(ct);

            var merchants = await _context.Merchants.AsNoTracking()
                .Where(m => !m.IsDeleted && (!byCity || cities.Contains(m.CityId)))
                .Select(m => new { m.MerchantId, m.FullName, m.IsActive })
                .ToDictionaryAsync(m => m.MerchantId, ct);

            var deliveries = await _context.Deliveries.AsNoTracking()
                .Where(d => !d.IsDeleted && (!byCity || cities.Contains(d.CityId)))
                .Select(d => new { d.DeliveryId, d.FullName, d.IsActive, d.IsOnline })
                .ToDictionaryAsync(d => d.DeliveryId, ct);

            var unpaidFees = await fees
                .Where(w => w.State != CustomerWalletState.Paid && w.CreatedDate < end)
                .GroupBy(_ => 1)
                .Select(g => new { Count = g.Count(), Amount = g.Sum(w => w.Withdraw - w.Deposit) })
                .FirstOrDefaultAsync(ct);

            var refunds = _context.RefundablePaypalAmounts.AsNoTracking()
                .Where(r => r.State == RefundState.Pending && r.CreatedDate < end);
            if (byCity) refunds = refunds.Where(r => cities.Contains(r.Customer.CityId));
            var pendingRefunds = await refunds
                .GroupBy(_ => 1)
                .Select(g => new { Count = g.Count(), Amount = g.Sum(r => r.RefundableAmount) })
                .FirstOrDefaultAsync(ct);

            // The treasury is company-wide, never split by city.
            var treasury = await _context.CompanyTreasuries.AsNoTracking()
                .Where(t => t.CreatedDate < end)
                .SumAsync(t => t.DebitAmount - t.CreditAmount, ct);

            // ---------- Top merchants / riders in the period ----------
            var earnings = await _context.OrderJournals.AsNoTracking()
                .Where(j => j.CreatedDate >= from && j.CreatedDate < end && j.PartyId != null && j.Direction == D.Credit
                            && ((j.PartyType == LedgerPartyType.Merchant && j.EntryKind == K.MerchantRentalAccrued)
                                || (j.PartyType == LedgerPartyType.Delivery && j.EntryKind == K.DeliveryFeeAccrued)))
                .GroupBy(j => new { j.PartyType, PartyId = j.PartyId!.Value })
                .Select(g => new
                {
                    g.Key.PartyType,
                    g.Key.PartyId,
                    Amount = g.Sum(j => j.Amount),
                    Orders = g.Select(j => j.OrderId).Distinct().Count()
                })
                .ToListAsync(ct);

            // ---------- Fleet and people ----------
            var vehicles = _context.Vehicles.AsNoTracking().Where(v => !v.Merchant.IsDeleted);
            if (byCity) vehicles = vehicles.Where(v => cities.Contains(v.Merchant.CityId));
            var fleet = await vehicles
                .GroupBy(v => v.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var customers = _context.Customers.AsNoTracking().Where(c => !c.IsDeleted);
            if (byCity) customers = customers.Where(c => cities.Contains(c.CityId));
            var customerStats = await customers
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    New = g.Count(c => c.CreatedDate >= from && c.CreatedDate < end),
                    PrevNew = g.Count(c => c.CreatedDate >= prevFrom && c.CreatedDate < from)
                })
                .FirstOrDefaultAsync(ct);

            var cityNames = await _context.Cities.AsNoTracking()
                .Select(c => new { c.CityId, c.Name })
                .ToDictionaryAsync(c => c.CityId, c => c.Name, ct);

            // ================= Shape the result in memory (all inputs above are small aggregates) =================
            var dto = new DashboardDto
            {
                From = from,
                To = to,
                PreviousFrom = prevFrom,
                PreviousTo = from.AddDays(-1),
                Bucket = bucket,
                GeneratedAt = _clock.Now,
                RecentOrders = recent
            };

            // Profit
            decimal Kind(K kind, bool current) => profitDays
                .Where(p => p.EntryKind == kind && (p.Day >= from) == current)
                .Sum(p => p.Direction == D.Credit ? p.Amount : -p.Amount);
            decimal Fees(bool current) => feeDays.Where(f => (f.Day >= from) == current).Sum(f => f.Amount);
            decimal Profit(bool current) => ProfitKinds.Sum(k => Kind(k, current)) + Fees(current);

            bool Live(OrderState s) => s != OrderState.Cancelled;
            var current = orderDays.Where(o => o.Day >= from).ToList();
            var previous = orderDays.Where(o => o.Day < from).ToList();
            var gross = current.Where(o => Live(o.OrderState)).Sum(o => o.Total);
            var prevGross = previous.Where(o => Live(o.OrderState)).Sum(o => o.Total);
            var netProfit = Profit(true);
            var prevProfit = Profit(false);

            dto.Profit = new DashboardProfitDto
            {
                NetProfit = netProfit,
                PreviousNetProfit = prevProfit,
                NetProfitChangePercent = Change(netProfit, prevProfit),
                GrossBookings = gross,
                PreviousGrossBookings = prevGross,
                GrossBookingsChangePercent = Change(gross, prevGross),
                MarginPercent = gross == 0 ? 0 : Math.Round(netProfit / gross * 100m, 1),
                Income = new List<DashboardAmountDto>
                {
                    new() { Key = "serviceFees", Amount = Kind(K.CompanyServiceFeeAccrued, true) },
                    new() { Key = "merchantCommission", Amount = Kind(K.CompanyMerchantCommissionAccrued, true) },
                    new() { Key = "deliveryFeeShare", Amount = Kind(K.CompanyDeliveryFeeRemainderAccrued, true) },
                    new() { Key = "cancellationFees", Amount = Fees(true), Count = feeDays.Where(f => f.Day >= from).Sum(f => f.Count) }
                },
                Costs = new List<DashboardAmountDto>
                {
                    new() { Key = "returnLegCommission", Amount = -Kind(K.CompanyReturnLegCommissionCharged, true) }
                }
            };

            // Position
            var merchantBalances = partyBalances.Where(b => b.PartyType == LedgerPartyType.Merchant && merchants.ContainsKey(b.PartyId)).ToList();
            var deliveryBalances = partyBalances.Where(b => b.PartyType == LedgerPartyType.Delivery && deliveries.ContainsKey(b.PartyId)).ToList();
            var merchantsOwe = merchantBalances.Where(b => b.Balance < 0).ToList();
            var owedToMerchants = merchantBalances.Where(b => b.Balance > 0).ToList();
            var deliveriesOwe = deliveryBalances.Where(b => b.Balance < 0).ToList();
            var owedToDeliveries = deliveryBalances.Where(b => b.Balance > 0).ToList();

            var owedItems = new List<DashboardAmountDto>
            {
                new() { Key = "deliveriesCash", Amount = -deliveriesOwe.Sum(b => b.Balance), Count = deliveriesOwe.Count },
                new() { Key = "merchants", Amount = -merchantsOwe.Sum(b => b.Balance), Count = merchantsOwe.Count },
                new() { Key = "customerFees", Amount = unpaidFees?.Amount ?? 0, Count = unpaidFees?.Count ?? 0 }
            };
            var oweItems = new List<DashboardAmountDto>
            {
                new() { Key = "merchants", Amount = owedToMerchants.Sum(b => b.Balance), Count = owedToMerchants.Count },
                new() { Key = "deliveries", Amount = owedToDeliveries.Sum(b => b.Balance), Count = owedToDeliveries.Count },
                new() { Key = "customerRefunds", Amount = pendingRefunds?.Amount ?? 0, Count = pendingRefunds?.Count ?? 0 }
            };

            string PartyName(LedgerPartyType type, int id) => type == LedgerPartyType.Merchant
                ? merchants.TryGetValue(id, out var m) ? m.FullName : $"#{id}"
                : deliveries.TryGetValue(id, out var d) ? d.FullName : $"#{id}";
            List<DashboardPartyDto> Parties(IEnumerable<(LedgerPartyType Type, int Id, decimal Amount)> rows) => rows
                .OrderByDescending(r => r.Amount).Take(TopCount)
                .Select(r => new DashboardPartyDto { PartyType = r.Type, PartyId = r.Id, Name = PartyName(r.Type, r.Id), Amount = r.Amount })
                .ToList();

            var relevant = merchantBalances.Concat(deliveryBalances).ToList();
            dto.Position = new DashboardPositionDto
            {
                OwedToCompany = owedItems.Sum(i => i.Amount),
                CompanyOwes = oweItems.Sum(i => i.Amount),
                TreasuryBalance = treasury,
                OwedToCompanyItems = owedItems,
                CompanyOwesItems = oweItems,
                TopDebtors = Parties(relevant.Where(b => b.Balance < 0).Select(b => (b.PartyType, b.PartyId, -b.Balance))),
                TopCreditors = Parties(relevant.Where(b => b.Balance > 0).Select(b => (b.PartyType, b.PartyId, b.Balance)))
            };
            dto.Position.NetPosition = dto.Position.OwedToCompany - dto.Position.CompanyOwes;

            // Trend: every bucket in the period, empty ones included so the chart has a steady axis.
            var points = new SortedDictionary<DateTime, DashboardTrendPointDto>();
            for (var day = from; day < end; day = day.AddDays(1))
            {
                var start = BucketStart(day, bucket);
                if (!points.ContainsKey(start)) points[start] = new DashboardTrendPointDto { Date = start };
            }
            foreach (var o in current)
            {
                var p = points[BucketStart(o.Day, bucket)];
                p.Orders += o.Count;
                if (o.OrderState == OrderState.Completed) p.Completed += o.Count;
                if (o.OrderState == OrderState.Cancelled) p.Cancelled += o.Count;
                if (Live(o.OrderState)) p.GrossBookings += o.Total;
            }
            foreach (var line in profitDays.Where(p => p.Day >= from))
                points[BucketStart(line.Day, bucket)].NetProfit += line.Direction == D.Credit ? line.Amount : -line.Amount;
            foreach (var f in feeDays.Where(f => f.Day >= from))
                points[BucketStart(f.Day, bucket)].NetProfit += f.Amount;
            dto.Trend = points.Values.ToList();

            // Orders
            var total = current.Sum(o => o.Count);
            var prevTotal = previous.Sum(o => o.Count);
            var completed = current.Where(o => o.OrderState == OrderState.Completed).Sum(o => o.Count);
            var cancelled = current.Where(o => o.OrderState == OrderState.Cancelled).Sum(o => o.Count);
            var liveCount = total - cancelled;
            dto.Orders = new DashboardOrdersDto
            {
                Total = total,
                PreviousTotal = prevTotal,
                TotalChangePercent = Change(total, prevTotal),
                Completed = completed,
                Cancelled = cancelled,
                CompletionRatePercent = Percent(completed, total),
                CancellationRatePercent = Percent(cancelled, total),
                Urgent = current.Sum(o => o.Urgent),
                AverageOrderValue = liveCount == 0 ? 0 : Math.Round(gross / liveCount, 2),
                AverageProfitPerOrder = completed == 0 ? 0 : Math.Round(netProfit / completed, 2),
                Pipeline = OpenStates
                    .Select(s => new DashboardStateCountDto { State = s, Count = pipeline.FirstOrDefault(p => p.State == s)?.Count ?? 0 })
                    .ToList(),
                OpenNow = pipeline.Sum(p => p.Count),
                WithCustomersNow = pipeline.FirstOrDefault(p => p.State == OrderState.CustomerReceived)?.Count ?? 0
            };

            // Payments
            decimal Paid(PaymentMethod method) => payments
                .Where(p => p.PaymentMethodId == (int)method && p.State == PaymentState.Paid).Sum(p => p.Total);
            dto.Payments = new DashboardPaymentsDto
            {
                CashPaid = Paid(PaymentMethod.Cash),
                PayPalPaid = Paid(PaymentMethod.PayPal),
                Pending = payments.Where(p => p.State == PaymentState.Pending).Sum(p => p.Total),
                Failed = payments.Where(p => p.State == PaymentState.Failed).Sum(p => p.Total),
                Refunded = payments.Where(p => p.State == PaymentState.Refunded).Sum(p => p.Total),
                PaidCount = payments.Where(p => p.State == PaymentState.Paid).Sum(p => p.Count)
            };

            // Fleet and people
            int Status(VehicleStatus s) => fleet.FirstOrDefault(f => f.Status == s)?.Count ?? 0;
            var fleetTotal = fleet.Sum(f => f.Count);
            dto.Fleet = new DashboardFleetDto
            {
                Total = fleetTotal,
                Available = Status(VehicleStatus.Available),
                Rented = Status(VehicleStatus.Rented),
                UnderMaintenance = Status(VehicleStatus.UnderMaintenance),
                UtilizationPercent = Percent(Status(VehicleStatus.Rented), fleetTotal)
            };
            dto.People = new DashboardPeopleDto
            {
                Customers = customerStats?.Total ?? 0,
                NewCustomers = customerStats?.New ?? 0,
                NewCustomersChangePercent = Change(customerStats?.New ?? 0, customerStats?.PrevNew ?? 0),
                ActiveMerchants = merchants.Values.Count(m => m.IsActive),
                ActiveDeliveries = deliveries.Values.Count(d => d.IsActive),
                OnlineDeliveries = deliveries.Values.Count(d => d.IsActive && d.IsOnline)
            };

            // Leaders
            List<DashboardRankDto> Top(LedgerPartyType type) => earnings
                .Where(e => e.PartyType == type && (type == LedgerPartyType.Merchant ? merchants.ContainsKey(e.PartyId) : deliveries.ContainsKey(e.PartyId)))
                .OrderByDescending(e => e.Amount).Take(TopCount)
                .Select(e => new DashboardRankDto { Id = e.PartyId, Name = PartyName(type, e.PartyId), Amount = e.Amount, Count = e.Orders })
                .ToList();
            dto.TopMerchants = Top(LedgerPartyType.Merchant);
            dto.TopDeliveries = Top(LedgerPartyType.Delivery);
            dto.TopCities = profitByCity
                .OrderByDescending(c => c.Profit).Take(TopCount)
                .Select(c => new DashboardRankDto { Id = c.CityId, Name = cityNames.GetValueOrDefault(c.CityId, $"#{c.CityId}"), Amount = c.Profit, Count = c.Orders })
                .ToList();

            return dto;
        }

        private static DateTime BucketStart(DateTime day, DashboardBucket bucket) => bucket switch
        {
            DashboardBucket.Week => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)),  // Monday
            DashboardBucket.Month => new DateTime(day.Year, day.Month, 1),
            _ => day
        };

        private static decimal Change(decimal current, decimal previous)
        {
            if (previous == 0) return current == 0 ? 0 : 100;
            return Math.Round((current - previous) / Math.Abs(previous) * 100m, 1);
        }

        private static decimal Percent(int part, int whole) => whole == 0 ? 0 : Math.Round(part * 100m / whole, 1);
    }
}
