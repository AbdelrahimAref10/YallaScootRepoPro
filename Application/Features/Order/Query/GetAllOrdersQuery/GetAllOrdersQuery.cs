using Application.Common;
using Application.Features.Order.DTOs;
using CSharpFunctionalExtensions;
using Domain.Enums;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Order.Query.GetAllOrdersQuery
{
    public record GetAllOrdersQuery : IRequest<Result<PagedResult<OrderDto>>>
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        /// <summary>Orders of this state only. When omitted, Pending orders are returned (never the whole table).</summary>
        public OrderState? State { get; set; }
        public string? OrderCode { get; set; }
        public int? CityId { get; set; }
    }

    public class GetAllOrdersQueryHandler : IRequestHandler<GetAllOrdersQuery, Result<PagedResult<OrderDto>>>
    {
        private const int MaxPageSize = 100;
        private readonly DatabaseContext _context;

        public GetAllOrdersQueryHandler(DatabaseContext context)
        {
            _context = context;
        }

        public async Task<Result<PagedResult<OrderDto>>> Handle(GetAllOrdersQuery request, CancellationToken cancellationToken)
        {
            // One state per request (defaults to Pending): the list is always filtered server-side,
            // so it stays cheap as the orders table grows. Names come from the projection below.
            var state = request.State ?? OrderState.Pending;
            var pageNumber = Math.Max(1, request.PageNumber);
            var pageSize = Math.Clamp(request.PageSize, 1, MaxPageSize);

            var query = _context.Orders.Where(o => o.OrderState == state);

            if (request.CityId is > 0)
            {
                query = query.Where(o => o.CityId == request.CityId.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.OrderCode))
            {
                query = query.Where(o => o.OrderCode.Contains(request.OrderCode));
            }

            var totalCount = await query.CountAsync(cancellationToken);

            var orders = await query
                .OrderByDescending(o => o.CreatedDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new OrderDto
                {
                    OrderId = o.OrderId,
                    OrderCode = o.OrderCode,
                    CustomerId = o.CustomerId,
                    CustomerName = o.Customer.FullName,
                    SubCategoryId = o.SubCategoryId,
                    SubCategoryName = o.SubCategory.Name,
                    CityId = o.CityId,
                    CityName = o.City.Name,
                    DestinationZoneId = o.DestinationZoneId,
                    ReservationDateFrom = o.ReservationDateFrom,
                    ReservationDateTo = o.ReservationDateTo,
                    VehiclesCount = o.VehiclesCount,
                    OrderSubTotal = o.OrderSubTotal,
                    OrderTotal = o.OrderTotal,
                    PreviousDebt = o.PreviousDebt,
                    MoneyRefunded = o.MoneyRefunded,
                    Notes = o.Notes,
                    HotelName = o.HotelName,
                    HotelAddress = o.HotelAddress,
                    HotelPhone = o.HotelPhone,
                    IsUrgent = o.IsUrgent,
                    PaymentMethod = (PaymentMethod)o.PaymentMethodId,
                    OrderState = o.OrderState,
                    CreatedDate = o.CreatedDate
                })
                .ToListAsync(cancellationToken);

            var result = new PagedResult<OrderDto>
            {
                Items = orders,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Result.Success(result);
        }
    }
}

