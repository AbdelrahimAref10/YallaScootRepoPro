using Application.Features.Customer.DTOs;
using CSharpFunctionalExtensions;
using Domain.Common;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Features.Customer.Query.GetCustomerNotificationsQuery
{
    /// <summary>The pushes sent to the signed-in customer (VO_CustomerNotification), newest first.</summary>
    public record GetCustomerNotificationsQuery : IRequest<Result<List<CustomerNotificationDto>>>
    {
        public int? Skip { get; set; }
        public int? Take { get; set; }
    }

    public record GetCustomerUnreadNotificationsCountQuery : IRequest<Result<int>>;

    /// <summary>Marks one notification read, or all of them when <see cref="NotificationId"/> is null.</summary>
    public record MarkCustomerNotificationsReadCommand : IRequest<Result<bool>>
    {
        public int? NotificationId { get; set; }
    }

    public class GetCustomerNotificationsQueryHandler :
        IRequestHandler<GetCustomerNotificationsQuery, Result<List<CustomerNotificationDto>>>,
        IRequestHandler<GetCustomerUnreadNotificationsCountQuery, Result<int>>,
        IRequestHandler<MarkCustomerNotificationsReadCommand, Result<bool>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;

        public GetCustomerNotificationsQueryHandler(DatabaseContext context, IUserSession userSession)
        {
            _context = context;
            _userSession = userSession;
        }

        public async Task<Result<List<CustomerNotificationDto>>> Handle(
            GetCustomerNotificationsQuery request,
            CancellationToken cancellationToken)
        {
            var customerId = await CurrentCustomerIdAsync(cancellationToken);
            if (customerId.IsFailure)
                return Result.Failure<List<CustomerNotificationDto>>(customerId.Error);

            var query = _context.CustomerNotifications
                .AsNoTracking()
                .Where(n => n.CustomerId == customerId.Value)
                .OrderByDescending(n => n.CreatedDate)
                .ThenByDescending(n => n.CustomerNotificationId)
                .AsQueryable();

            if (request.Skip.HasValue)
                query = query.Skip(request.Skip.Value);

            if (request.Take.HasValue)
                query = query.Take(request.Take.Value);

            var notifications = await query
                .Select(n => new CustomerNotificationDto
                {
                    CustomerNotificationId = n.CustomerNotificationId,
                    CustomerId = n.CustomerId,
                    Title = n.Title,
                    Message = n.Message,
                    OrderId = n.OrderId,
                    OrderCode = n.Order != null ? n.Order.OrderCode : null,
                    NotificationType = n.NotificationType,
                    IsRead = n.IsRead,
                    ReadAt = n.ReadAt,
                    CreatedDate = n.CreatedDate
                })
                .ToListAsync(cancellationToken);

            return Result.Success(notifications);
        }

        public async Task<Result<int>> Handle(GetCustomerUnreadNotificationsCountQuery request, CancellationToken cancellationToken)
        {
            var customerId = await CurrentCustomerIdAsync(cancellationToken);
            if (customerId.IsFailure)
                return Result.Failure<int>(customerId.Error);

            var count = await _context.CustomerNotifications
                .AsNoTracking()
                .CountAsync(n => n.CustomerId == customerId.Value && !n.IsRead, cancellationToken);

            return Result.Success(count);
        }

        public async Task<Result<bool>> Handle(MarkCustomerNotificationsReadCommand request, CancellationToken cancellationToken)
        {
            var customerId = await CurrentCustomerIdAsync(cancellationToken);
            if (customerId.IsFailure)
                return Result.Failure<bool>(customerId.Error);

            var query = _context.CustomerNotifications
                .AsTracking()
                .Where(n => n.CustomerId == customerId.Value && !n.IsRead);

            if (request.NotificationId.HasValue)
                query = query.Where(n => n.CustomerNotificationId == request.NotificationId.Value);

            var unread = await query.ToListAsync(cancellationToken);
            foreach (var notification in unread)
                notification.MarkAsRead();

            if (unread.Count > 0)
                await _context.SaveChangesAsync(cancellationToken);

            return Result.Success(true);
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
    }
}
