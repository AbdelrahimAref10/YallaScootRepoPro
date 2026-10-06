using Application.Features.AdminHome.Common;
using Application.Features.AdminHome.DTOs;
using CSharpFunctionalExtensions;
using Domain.Common;
using Domain.Enums;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.AdminHome.Query.GetHomeCustomerGrowthQuery
{
    public record GetHomeCustomerGrowthQuery : IRequest<Result<HomeCustomerGrowthDto>>
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public int? CityId { get; set; }
        public string Granularity { get; set; } = "day";
    }

    public class GetHomeCustomerGrowthQueryHandler : IRequestHandler<GetHomeCustomerGrowthQuery, Result<HomeCustomerGrowthDto>>
    {
        private readonly DatabaseContext _context;
        private readonly IDateTimeProvider _dateTimeProvider;

        public GetHomeCustomerGrowthQueryHandler(DatabaseContext context, IDateTimeProvider dateTimeProvider)
        {
            _context = context;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task<Result<HomeCustomerGrowthDto>> Handle(GetHomeCustomerGrowthQuery request, CancellationToken cancellationToken)
        {
            var granularity = (request.Granularity ?? "day").Trim().ToLowerInvariant();
            if (granularity is not ("day" or "week" or "month"))
            {
                return Result.Failure<HomeCustomerGrowthDto>("Granularity must be day, week, or month.");
            }

            var now = _dateTimeProvider.Now;
            var (from, to) = AdminHomeDateRange.Resolve(request.From, request.To, now);

            var customers = _context.Customers.AsNoTracking().AsQueryable();
            if (request.CityId.HasValue)
            {
                customers = customers.Where(c => c.CityId == request.CityId.Value);
            }

            // All headline counts in one pass (was 7 round trips).
            var stats = await customers
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Active = g.Count(c => c.State == CustomerState.Active),
                    Inactive = g.Count(c => c.State == CustomerState.InActive),
                    Blocked = g.Count(c => c.State == CustomerState.Blocked),
                    Individual = g.Count(c => c.RegisterAs == 0),
                    Institution = g.Count(c => c.RegisterAs == 1),
                    CashBlocked = g.Count(c => c.CashBlock)
                })
                .FirstOrDefaultAsync(cancellationToken);
            var total = stats?.Total ?? 0;
            var active = stats?.Active ?? 0;
            var inactive = stats?.Inactive ?? 0;
            var blocked = stats?.Blocked ?? 0;
            var individual = stats?.Individual ?? 0;
            var institution = stats?.Institution ?? 0;
            var cashBlocked = stats?.CashBlocked ?? 0;

            var createdInRange = await customers
                .Where(c => c.CreatedDate >= from && c.CreatedDate <= to)
                .Select(c => c.CreatedDate)
                .ToListAsync(cancellationToken);

            var buckets = BuildBuckets(from, to, granularity);
            foreach (var created in createdInRange)
            {
                var key = BucketStart(created, granularity);
                if (buckets.TryGetValue(key, out var point))
                {
                    point.Count += 1;
                    point.Value += 1;
                }
            }

            return Result.Success(new HomeCustomerGrowthDto
            {
                Granularity = granularity,
                TotalCustomers = total,
                ActiveCustomers = active,
                InactiveCustomers = inactive,
                BlockedCustomers = blocked,
                IndividualCustomers = individual,
                InstitutionCustomers = institution,
                NewInRange = createdInRange.Count,
                CashBlockedCustomers = cashBlocked,
                Series = buckets.Values.OrderBy(x => x.PeriodStart).ToList(),
                From = from,
                To = to
            });
        }

        private static Dictionary<DateTime, HomeChartPointDto> BuildBuckets(DateTime from, DateTime to, string granularity)
        {
            var map = new Dictionary<DateTime, HomeChartPointDto>();
            var cursor = BucketStart(from, granularity);
            var end = to.Date;

            while (cursor <= end)
            {
                map[cursor] = new HomeChartPointDto
                {
                    PeriodStart = cursor,
                    Period = granularity == "month" ? $"{cursor:yyyy-MM}" : $"{cursor:yyyy-MM-dd}",
                    Value = 0,
                    Count = 0
                };

                cursor = granularity switch
                {
                    "week" => cursor.AddDays(7),
                    "month" => cursor.AddMonths(1),
                    _ => cursor.AddDays(1)
                };
            }

            return map;
        }

        private static DateTime BucketStart(DateTime date, string granularity)
        {
            return granularity switch
            {
                "week" => date.Date.AddDays(-(int)date.DayOfWeek),
                "month" => new DateTime(date.Year, date.Month, 1),
                _ => date.Date
            };
        }
    }
}
