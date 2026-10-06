using Application.Features.AdminHome.Common;
using Application.Features.AdminHome.DTOs;
using CSharpFunctionalExtensions;
using Domain.Common;
using Domain.Enums;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.AdminHome.Query.GetHomeSummaryQuery
{
    public record GetHomeSummaryQuery : IRequest<Result<HomeSummaryDto>>
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int? CityId { get; set; }
    }

    public class GetHomeSummaryQueryHandler : IRequestHandler<GetHomeSummaryQuery, Result<HomeSummaryDto>>
    {
        private readonly DatabaseContext _context;
        private readonly IDateTimeProvider _dateTimeProvider;

        public GetHomeSummaryQueryHandler(DatabaseContext context, IDateTimeProvider dateTimeProvider)
        {
            _context = context;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task<Result<HomeSummaryDto>> Handle(GetHomeSummaryQuery request, CancellationToken cancellationToken)
        {
            var now = _dateTimeProvider.Now;
            var (from, to) = AdminHomeDateRange.Resolve(request.From, request.To, now);
            var (prevFrom, prevTo) = AdminHomeDateRange.PreviousPeriod(from, to);

            var orders = _context.Orders.AsNoTracking().AsQueryable();
            if (request.CityId.HasValue)
            {
                orders = orders.Where(o => o.CityId == request.CityId.Value);
            }

            // One pass over orders: current/previous counts and live states (was 4 round trips).
            var orderStats = await orders
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    OrdersCount = g.Count(o => o.CreatedDate >= from && o.CreatedDate <= to),
                    PrevOrdersCount = g.Count(o => o.CreatedDate >= prevFrom && o.CreatedDate <= prevTo),
                    ActiveRentals = g.Count(o =>
                        o.OrderState == OrderState.Confirmed ||
                        o.OrderState == OrderState.OnWay ||
                        o.OrderState == OrderState.CustomerReceived),
                    PendingOrders = g.Count(o => o.OrderState == OrderState.Pending)
                })
                .FirstOrDefaultAsync(cancellationToken);

            // Paid revenue for both periods in one query (was 2 round trips).
            var revenueStats = await orders
                .Where(o => o.OrderState == OrderState.Completed && o.CreatedDate >= prevFrom && o.CreatedDate <= to)
                .SelectMany(o => o.OrderPayments
                    .Where(p => p.State == PaymentState.Paid)
                    .Select(p => new { o.CreatedDate, p.Total }))
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Revenue = g.Sum(x => x.CreatedDate >= from && x.CreatedDate <= to ? x.Total : 0m),
                    PrevRevenue = g.Sum(x => x.CreatedDate >= prevFrom && x.CreatedDate <= prevTo ? x.Total : 0m)
                })
                .FirstOrDefaultAsync(cancellationToken);

            // Fleet in one query (was 3 round trips).
            var fleet = await _context.Vehicles.AsNoTracking()
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Available = g.Count(v => v.Status == VehicleStatus.Available),
                    Rented = g.Count(v => v.Status == VehicleStatus.Rented)
                })
                .FirstOrDefaultAsync(cancellationToken);

            var customers = _context.Customers.AsNoTracking().AsQueryable();
            if (request.CityId.HasValue)
            {
                customers = customers.Where(c => c.CityId == request.CityId.Value);
            }

            // New customers for both periods (was 2 round trips); only the date window is read.
            var customerStats = await customers
                .Where(c => c.CreatedDate >= prevFrom && c.CreatedDate <= to)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    NewCustomers = g.Count(c => c.CreatedDate >= from && c.CreatedDate <= to),
                    PrevNewCustomers = g.Count(c => c.CreatedDate >= prevFrom && c.CreatedDate <= prevTo)
                })
                .FirstOrDefaultAsync(cancellationToken);

            // Treasury totals in one query (was 2 round trips).
            var treasury = await _context.CompanyTreasuries.AsNoTracking()
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Debit = g.Sum(t => t.DebitAmount),
                    Credit = g.Sum(t => t.CreditAmount)
                })
                .FirstOrDefaultAsync(cancellationToken);

            var unpaidCancellationFeesCount = await _context.CustomerWallets.AsNoTracking()
                .CountAsync(cw =>
                    cw.Type == WalletType.OrderCancellationFees &&
                    cw.State == CustomerWalletState.Pending &&
                    cw.CreatedDate >= from &&
                    cw.CreatedDate <= to, cancellationToken);

            var revenue = revenueStats?.Revenue ?? 0;
            var prevRevenue = revenueStats?.PrevRevenue ?? 0;
            var ordersCount = orderStats?.OrdersCount ?? 0;
            var totalVehicles = fleet?.Total ?? 0;
            var utilization = totalVehicles == 0 ? 0 : Math.Round((decimal)(fleet?.Rented ?? 0) / totalVehicles * 100m, 2);
            var newCustomers = customerStats?.NewCustomers ?? 0;

            var dto = new HomeSummaryDto
            {
                Revenue = revenue,
                RevenueChangePercent = AdminHomeDateRange.PercentChange(revenue, prevRevenue),
                OrdersCount = ordersCount,
                OrdersChangePercent = AdminHomeDateRange.PercentChange(ordersCount, orderStats?.PrevOrdersCount ?? 0),
                AverageOrderValue = ordersCount == 0 ? 0 : Math.Round(revenue / ordersCount, 2),
                ActiveRentals = orderStats?.ActiveRentals ?? 0,
                PendingOrders = orderStats?.PendingOrders ?? 0,
                AvailableVehicles = fleet?.Available ?? 0,
                TotalVehicles = totalVehicles,
                VehicleUtilizationPercent = utilization,
                NewCustomers = newCustomers,
                NewCustomersChangePercent = AdminHomeDateRange.PercentChange(newCustomers, customerStats?.PrevNewCustomers ?? 0),
                TreasuryBalance = (treasury?.Debit ?? 0) - (treasury?.Credit ?? 0),
                UnpaidCancellationFeesCount = unpaidCancellationFeesCount,
                From = from,
                To = to
            };

            return Result.Success(dto);
        }
    }
}
