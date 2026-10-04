using Application.Common;
using Application.Features.Delivery.Common;
using Application.Features.DeliveryApp.DTOs;
using CSharpFunctionalExtensions;
using Domain.Common;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.DeliveryApp.Query.GetRiderNotificationsQuery
{
    public record GetRiderNotificationsQuery : IRequest<Result<PagedResult<RiderNotificationDto>>>
    {
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 30;
    }

    public record GetRiderUnreadNotificationsCountQuery : IRequest<Result<int>>;

    public record MarkRiderNotificationAsReadCommand : IRequest<Result<bool>>
    {
        public int DeliveryNotificationId { get; set; }
    }

    public record MarkAllRiderNotificationsAsReadCommand : IRequest<Result<bool>>;

    public class GetRiderNotificationsQueryHandler :
        IRequestHandler<GetRiderNotificationsQuery, Result<PagedResult<RiderNotificationDto>>>,
        IRequestHandler<GetRiderUnreadNotificationsCountQuery, Result<int>>,
        IRequestHandler<MarkRiderNotificationAsReadCommand, Result<bool>>,
        IRequestHandler<MarkAllRiderNotificationsAsReadCommand, Result<bool>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;

        public GetRiderNotificationsQueryHandler(DatabaseContext context, IUserSession userSession)
        {
            _context = context;
            _userSession = userSession;
        }

        public async Task<Result<PagedResult<RiderNotificationDto>>> Handle(GetRiderNotificationsQuery request, CancellationToken cancellationToken)
        {
            var rider = await RiderContext.GetCurrentAsync(_context, _userSession, cancellationToken);
            if (rider.IsFailure)
                return Result.Failure<PagedResult<RiderNotificationDto>>(rider.Error);

            var pageNumber = Math.Max(1, request.PageNumber);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            var query = _context.DeliveryNotifications.AsNoTracking().Where(n => n.DeliveryId == rider.Value.DeliveryId);

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(n => n.CreatedDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new RiderNotificationDto
                {
                    Id = n.DeliveryNotificationId,
                    Title = n.Title,
                    Body = n.Message,
                    OrderId = n.OrderId,
                    IsRead = n.IsRead,
                    CreatedDate = n.CreatedDate
                })
                .ToListAsync(cancellationToken);

            return Result.Success(new PagedResult<RiderNotificationDto>
            {
                Items = items,
                TotalCount = total,
                PageNumber = pageNumber,
                PageSize = pageSize
            });
        }

        public async Task<Result<int>> Handle(GetRiderUnreadNotificationsCountQuery request, CancellationToken cancellationToken)
        {
            var rider = await RiderContext.GetCurrentAsync(_context, _userSession, cancellationToken);
            if (rider.IsFailure)
                return Result.Failure<int>(rider.Error);

            return Result.Success(await _context.DeliveryNotifications
                .AsNoTracking()
                .CountAsync(n => n.DeliveryId == rider.Value.DeliveryId && !n.IsRead, cancellationToken));
        }

        public async Task<Result<bool>> Handle(MarkRiderNotificationAsReadCommand request, CancellationToken cancellationToken)
        {
            var rider = await RiderContext.GetCurrentAsync(_context, _userSession, cancellationToken);
            if (rider.IsFailure)
                return Result.Failure<bool>(rider.Error);

            var notification = await _context.DeliveryNotifications
                .AsTracking()
                .FirstOrDefaultAsync(n => n.DeliveryNotificationId == request.DeliveryNotificationId
                    && n.DeliveryId == rider.Value.DeliveryId, cancellationToken);
            if (notification == null)
                return Result.Failure<bool>("Notification not found");

            notification.MarkAsRead();
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success(true);
        }

        public async Task<Result<bool>> Handle(MarkAllRiderNotificationsAsReadCommand request, CancellationToken cancellationToken)
        {
            var rider = await RiderContext.GetCurrentAsync(_context, _userSession, cancellationToken);
            if (rider.IsFailure)
                return Result.Failure<bool>(rider.Error);

            var unread = await _context.DeliveryNotifications
                .AsTracking()
                .Where(n => n.DeliveryId == rider.Value.DeliveryId && !n.IsRead)
                .ToListAsync(cancellationToken);
            foreach (var n in unread)
                n.MarkAsRead();

            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success(true);
        }
    }
}
