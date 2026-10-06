using CSharpFunctionalExtensions;
using Domain.Enums;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Order.Query.GetOrderStateCountsQuery
{
    /// <summary>Order count per state (badges on the orders page). One GROUP BY over the OrderState index.</summary>
    public record GetOrderStateCountsQuery : IRequest<Result<List<OrderStateCountDto>>>
    {
        public int? CityId { get; set; }
        public string? OrderCode { get; set; }
    }

    public class OrderStateCountDto
    {
        public OrderState State { get; set; }
        public int Count { get; set; }
    }

    public class GetOrderStateCountsQueryHandler : IRequestHandler<GetOrderStateCountsQuery, Result<List<OrderStateCountDto>>>
    {
        private readonly DatabaseContext _context;

        public GetOrderStateCountsQueryHandler(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<Result<List<OrderStateCountDto>>> Handle(GetOrderStateCountsQuery request, CancellationToken cancellationToken)
        {
            var query = _context.Orders.AsQueryable();

            if (request.CityId is > 0)
            {
                query = query.Where(o => o.CityId == request.CityId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.OrderCode))
            {
                query = query.Where(o => o.OrderCode.Contains(request.OrderCode));
            }

            var counts = await query
                .GroupBy(o => o.OrderState)
                .Select(g => new OrderStateCountDto { State = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            return Result.Success(counts);
        }
    }
}
