using CSharpFunctionalExtensions;
using Domain.Common;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Delivery.Command.SetDeliveryCashDebtLimitCommand
{
    /// <summary>
    /// Admin sets how much collected cash a rider may hold before he must remit. Null removes the limit.
    /// At or above the limit he cannot be assigned the delivery trip of a cash order.
    /// </summary>
    public record SetDeliveryCashDebtLimitCommand : IRequest<Result<bool>>
    {
        public int DeliveryId { get; set; }
        public decimal? CashDebtLimit { get; set; }
    }

    public class SetDeliveryCashDebtLimitCommandHandler : IRequestHandler<SetDeliveryCashDebtLimitCommand, Result<bool>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;

        public SetDeliveryCashDebtLimitCommandHandler(DatabaseContext context, IUserSession userSession)
        {
            _context = context;
            _userSession = userSession;
        }

        public async Task<Result<bool>> Handle(SetDeliveryCashDebtLimitCommand request, CancellationToken cancellationToken)
        {
            if (request.CashDebtLimit is < 0)
                return Result.Failure<bool>("Cash debt limit cannot be negative");

            var delivery = await _context.Deliveries
                .AsTracking()
                .FirstOrDefaultAsync(d => d.DeliveryId == request.DeliveryId && !d.IsDeleted, cancellationToken);

            if (delivery == null)
                return Result.Failure<bool>("Delivery not found");

            delivery.SetCashDebtLimit(request.CashDebtLimit, _userSession.UserName ?? "System");
            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success(true);
        }
    }
}
