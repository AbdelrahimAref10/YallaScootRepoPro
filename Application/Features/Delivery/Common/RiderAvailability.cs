namespace Application.Features.Delivery.Common
{
    public enum RiderAvailabilityStatus
    {
        /// <summary>In a running shift and switched online.</summary>
        Available = 1,

        /// <summary>In a running shift but switched offline.</summary>
        InShiftOffline = 2,

        /// <summary>No running shift right now.</summary>
        OffShift = 3
    }

    public sealed class RiderAvailability
    {
        public RiderAvailabilityStatus Status { get; init; }
        public Domain.Models.Shift? CurrentShift { get; init; }

        /// <summary>Local time the current shift run ends; null when off shift.</summary>
        public DateTime? CurrentShiftEndsAt { get; init; }

        /// <summary>Next local start among the rider's shifts within 7 days; null when in shift or no shifts.</summary>
        public DateTime? NextShiftStartsAt { get; init; }
        public Domain.Models.Shift? NextShift { get; init; }

        /// <summary>Online only counts inside a running shift.</summary>
        public bool IsEffectivelyOnline => Status == RiderAvailabilityStatus.Available;
        public bool IsInShift => Status != RiderAvailabilityStatus.OffShift;

        /// <param name="onlineChangedAtUtc">
        /// When the toggle last changed. A rider who stayed "online" past the end of a shift is offline
        /// in the next one until he switches on again, so the toggle only counts if it was set during the current run.
        /// Pass null to trust the toggle as is (e.g. right after setting it).
        /// </param>
        public static RiderAvailability Compute(
            bool isOnline,
            IEnumerable<Domain.Models.Shift> shifts,
            DateTime localNow,
            DateTime? onlineChangedAtUtc = null)
        {
            var active = shifts.Where(s => s.IsActive && !s.IsDeleted).ToList();
            var running = active
                .Select(s => (Shift: s, EndsAt: s.CurrentRunEndsAt(localNow)))
                .Where(x => x.EndsAt.HasValue)
                .OrderByDescending(x => x.EndsAt)
                .FirstOrDefault();

            if (running.Shift != null)
            {
                var length = running.Shift.EndTime - running.Shift.StartTime;
                if (length <= TimeSpan.Zero)
                    length += TimeSpan.FromDays(1);
                var runStartedAt = running.EndsAt!.Value - length;

                var online = isOnline
                    && (onlineChangedAtUtc == null || RiderTime.ToLocal(onlineChangedAtUtc.Value) >= runStartedAt);

                return new RiderAvailability
                {
                    Status = online ? RiderAvailabilityStatus.Available : RiderAvailabilityStatus.InShiftOffline,
                    CurrentShift = running.Shift,
                    CurrentShiftEndsAt = running.EndsAt
                };
            }

            (Domain.Models.Shift Shift, DateTime StartsAt)? next = null;
            foreach (var shift in active)
            {
                for (var day = 0; day <= 7; day++)
                {
                    var date = localNow.Date.AddDays(day);
                    var startsAt = date + shift.StartTime;
                    if (startsAt <= localNow || !shift.RunsOn(date.DayOfWeek))
                        continue;
                    if (next == null || startsAt < next.Value.StartsAt)
                        next = (shift, startsAt);
                    break;
                }
            }

            return new RiderAvailability
            {
                Status = RiderAvailabilityStatus.OffShift,
                NextShift = next?.Shift,
                NextShiftStartsAt = next?.StartsAt
            };
        }
    }
}
