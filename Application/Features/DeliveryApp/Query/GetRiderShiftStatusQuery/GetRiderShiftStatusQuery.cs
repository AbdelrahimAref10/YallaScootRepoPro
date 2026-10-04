using Application.Features.Delivery.Common;
using Application.Features.DeliveryApp.DTOs;
using Application.Features.Shifts.Common;
using CSharpFunctionalExtensions;
using Domain.Common;
using Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Application.Features.DeliveryApp.Query.GetRiderShiftStatusQuery
{
    public record GetRiderShiftStatusQuery : IRequest<Result<RiderShiftStatusDto>>;

    /// <summary>Turns the rider online/offline. Going online only works inside a running shift.</summary>
    public record SetRiderOnlineCommand : IRequest<Result<RiderShiftStatusDto>>
    {
        public bool IsOnline { get; set; }
    }

    public class GetRiderShiftStatusQueryHandler :
        IRequestHandler<GetRiderShiftStatusQuery, Result<RiderShiftStatusDto>>,
        IRequestHandler<SetRiderOnlineCommand, Result<RiderShiftStatusDto>>
    {
        private readonly DatabaseContext _context;
        private readonly IUserSession _userSession;

        public GetRiderShiftStatusQueryHandler(DatabaseContext context, IUserSession userSession)
        {
            _context = context;
            _userSession = userSession;
        }

        public async Task<Result<RiderShiftStatusDto>> Handle(GetRiderShiftStatusQuery request, CancellationToken cancellationToken)
        {
            var rider = await RiderContext.GetCurrentAsync(_context, _userSession, cancellationToken);
            if (rider.IsFailure)
                return Result.Failure<RiderShiftStatusDto>(rider.Error);

            var shifts = await LoadShiftsAsync(rider.Value.DeliveryId, cancellationToken);
            return Result.Success(Build(rider.Value.IsOnline, shifts, rider.Value.OnlineStatusChangedAt));
        }

        public async Task<Result<RiderShiftStatusDto>> Handle(SetRiderOnlineCommand request, CancellationToken cancellationToken)
        {
            var rider = await RiderContext.GetCurrentAsync(_context, _userSession, cancellationToken, tracking: true);
            if (rider.IsFailure)
                return Result.Failure<RiderShiftStatusDto>(rider.Error);

            var shifts = await LoadShiftsAsync(rider.Value.DeliveryId, cancellationToken);
            var availability = RiderAvailability.Compute(request.IsOnline, shifts, RiderTime.LocalNow());

            if (request.IsOnline && !availability.IsInShift)
                return Result.Failure<RiderShiftStatusDto>("You can go online only during your shift");

            rider.Value.SetOnline(request.IsOnline, _userSession.UserName);
            await _context.SaveChangesAsync(cancellationToken);

            return Result.Success(Build(request.IsOnline, shifts, null));
        }

        private Task<List<Domain.Models.Shift>> LoadShiftsAsync(int deliveryId, CancellationToken ct) =>
            _context.DeliveryShifts
                .AsNoTracking()
                .Where(ds => ds.DeliveryId == deliveryId && ds.Shift.IsActive && !ds.Shift.IsDeleted)
                .Select(ds => ds.Shift)
                .ToListAsync(ct);

        private static RiderShiftStatusDto Build(bool toggle, List<Domain.Models.Shift> shifts, DateTime? toggledAtUtc)
        {
            var availability = RiderAvailability.Compute(toggle, shifts, RiderTime.LocalNow(), toggledAtUtc);
            return new RiderShiftStatusDto
            {
                IsOnline = availability.IsEffectivelyOnline,
                OnlineToggle = toggle,
                IsInShift = availability.IsInShift,
                CanGoOnline = availability.IsInShift,
                CurrentShift = availability.CurrentShift == null ? null : ToDto(availability.CurrentShift),
                CurrentShiftEndsAt = availability.CurrentShiftEndsAt.HasValue ? RiderTime.ToUtc(availability.CurrentShiftEndsAt.Value) : null,
                NextShift = availability.NextShift == null ? null : ToDto(availability.NextShift),
                NextShiftStartsAt = availability.NextShiftStartsAt.HasValue ? RiderTime.ToUtc(availability.NextShiftStartsAt.Value) : null,
                Shifts = shifts.OrderBy(s => s.StartTime).Select(ToDto).ToList()
            };
        }

        private static RiderShiftDto ToDto(Domain.Models.Shift s) => new()
        {
            ShiftId = s.ShiftId,
            Name = s.Name,
            StartTime = ShiftMapper.FormatTime(s.StartTime),
            EndTime = ShiftMapper.FormatTime(s.EndTime),
            DaysOfWeekMask = s.DaysOfWeekMask
        };
    }
}
