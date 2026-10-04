using CSharpFunctionalExtensions;
using Domain.Common;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.Shifts.Command.DeleteShiftCommand
{
    /// <summary>Soft-deletes a shift and drops its rider links.</summary>
    public record DeleteShiftCommand : IRequest<Result<bool>>
    {
        public int ShiftId { get; set; }
    }

    public class DeleteShiftCommandHandler : IRequestHandler<DeleteShiftCommand, Result<bool>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;

        public DeleteShiftCommandHandler(DatabaseContext context, IUserSession userSession)
        {
            _context = context;
            _userSession = userSession;
        }

        public async Task<Result<bool>> Handle(DeleteShiftCommand request, CancellationToken cancellationToken)
        {
            var shift = await _context.Shifts
                .AsTracking()
                .Include(s => s.DeliveryShifts)
                .FirstOrDefaultAsync(s => s.ShiftId == request.ShiftId && !s.IsDeleted, cancellationToken);

            if (shift == null)
                return Result.Failure<bool>($"Shift with ID {request.ShiftId} not found");

            _context.DeliveryShifts.RemoveRange(shift.DeliveryShifts);
            shift.SoftDelete(_userSession.UserName ?? "System");

            await _context.SaveChangesAsync(cancellationToken);
            return Result.Success(true);
        }
    }
}
