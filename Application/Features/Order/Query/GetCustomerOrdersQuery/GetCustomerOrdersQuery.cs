using Application.Common;
using Application.Features.Order.Common;
using Application.Features.Order.DTOs;
using CSharpFunctionalExtensions;
using Domain.Common;
using Domain.Enums;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Order.Query.GetCustomerOrdersQuery
{
    public record GetCustomerOrdersQuery : IRequest<Result<PagedResult<OrderDto>>>
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    /// <summary>One of the signed-in customer's orders (opened from a push), with its riders.</summary>
    public record GetCustomerOrderByIdQuery : IRequest<Result<OrderDto>>
    {
        public int OrderId { get; set; }
    }

    public class GetCustomerOrdersQueryHandler :
        IRequestHandler<GetCustomerOrdersQuery, Result<PagedResult<OrderDto>>>,
        IRequestHandler<GetCustomerOrderByIdQuery, Result<OrderDto>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;

        public GetCustomerOrdersQueryHandler(DatabaseContext context, IUserSession userSession)
        {
            _context = context;
            _userSession = userSession;
        }

        public async Task<Result<PagedResult<OrderDto>>> Handle(GetCustomerOrdersQuery request, CancellationToken cancellationToken)
        {
            var customerId = await CurrentCustomerIdAsync(cancellationToken);
            if (customerId.IsFailure)
                return Result.Failure<PagedResult<OrderDto>>(customerId.Error);

            var query = _context.Orders
                .Where(o => o.CustomerId == customerId.Value)
                .OrderByDescending(o => o.CreatedDate)
                .AsQueryable();

            // Same clamping as the rider lists: a 0/negative page would make Skip negative and throw.
            var pageNumber = Math.Max(1, request.PageNumber);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);

            var totalCount = await query.CountAsync(cancellationToken);

            var orders = await Project(query
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize))
                .ToListAsync(cancellationToken);

            await AttachDetailsAsync(orders, cancellationToken);

            var result = new PagedResult<OrderDto>
            {
                Items = orders,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize
            };

            return Result.Success(result);
        }

        public async Task<Result<OrderDto>> Handle(GetCustomerOrderByIdQuery request, CancellationToken cancellationToken)
        {
            var customerId = await CurrentCustomerIdAsync(cancellationToken);
            if (customerId.IsFailure)
                return Result.Failure<OrderDto>(customerId.Error);

            var order = await Project(_context.Orders
                    .Where(o => o.OrderId == request.OrderId && o.CustomerId == customerId.Value))
                .FirstOrDefaultAsync(cancellationToken);

            if (order == null)
                return Result.Failure<OrderDto>("Order not found");

            await AttachDetailsAsync(new List<OrderDto> { order }, cancellationToken);
            return Result.Success(order);
        }

        private async Task<Result<int>> CurrentCustomerIdAsync(CancellationToken cancellationToken)
        {
            if (_userSession.UserId <= 0)
                return Result.Failure<int>("Customer not found or not authenticated");

            var customerId = await _context.Customers
                .AsNoTracking()
                .Where(c => c.UserId == _userSession.UserId)
                .Select(c => (int?)c.CustomerId)
                .FirstOrDefaultAsync(cancellationToken);

            return customerId.HasValue
                ? Result.Success(customerId.Value)
                : Result.Failure<int>("Customer not found");
        }

        /// <summary>Riders per vehicle trip and the cost lines, for the orders on this page.</summary>
        private async Task AttachDetailsAsync(List<OrderDto> orders, CancellationToken cancellationToken)
        {
            var orderIds = orders.Select(o => o.OrderId).ToList();
            var riders = await CustomerOrderRiders.LoadAsync(_context, orderIds, cancellationToken);

            var totals = await _context.OrderTotals
                .AsNoTracking()
                .Where(t => orderIds.Contains(t.OrderId))
                .ToDictionaryAsync(t => t.OrderId, cancellationToken);

            var previousDebts = await _context.Orders
                .AsNoTracking()
                .Where(o => orderIds.Contains(o.OrderId))
                .ToDictionaryAsync(o => o.OrderId, o => o.PreviousDebt, cancellationToken);

            foreach (var order in orders)
            {
                order.Riders = riders.TryGetValue(order.OrderId, out var list) ? list : new List<CustomerOrderRiderDto>();

                if (totals.TryGetValue(order.OrderId, out var t))
                {
                    order.PriceBreakdown = new OrderPriceBreakdownDto
                    {
                        SubTotal = t.SubTotal,
                        ServiceFees = t.ServiceFees,
                        DeliveryFees = t.DeliveryFees,
                        UrgentFees = t.UrgentFees,
                        Discount = t.TieredDiscount,
                        PreviousDebt = previousDebts.TryGetValue(order.OrderId, out var debt) ? debt : 0,
                        Total = order.OrderTotal
                    };
                }
            }
        }

        private static IQueryable<OrderDto> Project(IQueryable<Domain.Models.Order> query) =>
            query.Select(o => new OrderDto
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
                Notes = o.Notes,
                HotelName = o.HotelName,
                HotelAddress = o.HotelAddress,
                HotelPhone = o.HotelPhone,
                IsUrgent = o.IsUrgent,
                PaymentMethod = (PaymentMethod)o.PaymentMethodId,
                OrderState = o.OrderState,
                CreatedDate = o.CreatedDate
            });
    }
}
