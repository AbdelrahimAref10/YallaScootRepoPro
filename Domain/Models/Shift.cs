using Domain.Common;

namespace Domain.Models
{
    /// <summary>
    /// A working window for riders in one city. Times are local (Egypt) and the
    /// window may cross midnight (e.g. 22:00 → 06:00). <see cref="DaysOfWeekMask"/>
    /// is a bitmask over <see cref="DayOfWeek"/> (Sunday = 1, Monday = 2, … Saturday = 64)
    /// and refers to the day the shift starts on.
    /// </summary>
    public class Shift : IAuditable
    {
        public const int AllDays = 127;

        public int ShiftId { get; private set; }
        public int CityId { get; private set; }
        public string Name { get; private set; } = string.Empty;
        public TimeSpan StartTime { get; private set; }
        public TimeSpan EndTime { get; private set; }
        public int DaysOfWeekMask { get; private set; } = AllDays;
        public bool IsActive { get; private set; } = true;
        public bool IsDeleted { get; private set; }

        public City City { get; private set; } = null!;
        public ICollection<DeliveryShift> DeliveryShifts { get; private set; } = new List<DeliveryShift>();

        public string? CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string? LastModifiedBy { get; set; }
        public DateTime LastModifiedDate { get; set; }

        private Shift() { }

        public static Shift Create(
            int cityId,
            string name,
            TimeSpan startTime,
            TimeSpan endTime,
            int daysOfWeekMask,
            bool isActive = true,
            string? createdBy = null)
        {
            Validate(cityId, name, startTime, endTime, daysOfWeekMask);
            return new Shift
            {
                CityId = cityId,
                Name = name.Trim(),
                StartTime = startTime,
                EndTime = endTime,
                DaysOfWeekMask = daysOfWeekMask,
                IsActive = isActive,
                IsDeleted = false,
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };
        }

        public void Update(
            int cityId,
            string name,
            TimeSpan startTime,
            TimeSpan endTime,
            int daysOfWeekMask,
            bool isActive,
            string? modifiedBy = null)
        {
            Validate(cityId, name, startTime, endTime, daysOfWeekMask);
            CityId = cityId;
            Name = name.Trim();
            StartTime = startTime;
            EndTime = endTime;
            DaysOfWeekMask = daysOfWeekMask;
            IsActive = isActive;
            LastModifiedBy = modifiedBy;
            LastModifiedDate = DateTime.UtcNow;
        }

        public void SoftDelete(string? modifiedBy = null)
        {
            IsDeleted = true;
            IsActive = false;
            LastModifiedBy = modifiedBy;
            LastModifiedDate = DateTime.UtcNow;
        }

        /// <summary>True when <paramref name="localNow"/> falls inside this shift.</summary>
        public bool IsRunningAt(DateTime localNow)
        {
            if (!IsActive || IsDeleted)
                return false;

            var time = localNow.TimeOfDay;
            if (StartTime < EndTime)
                return RunsOn(localNow.DayOfWeek) && time >= StartTime && time < EndTime;

            // Crosses midnight: either the evening part of today, or the morning part of a shift that started yesterday.
            if (time >= StartTime)
                return RunsOn(localNow.DayOfWeek);
            return time < EndTime && RunsOn(localNow.AddDays(-1).DayOfWeek);
        }

        /// <summary>Local end of the run that contains <paramref name="localNow"/>; null when not running.</summary>
        public DateTime? CurrentRunEndsAt(DateTime localNow)
        {
            if (!IsRunningAt(localNow))
                return null;

            var end = localNow.Date + EndTime;
            if (StartTime >= EndTime && localNow.TimeOfDay >= StartTime)
                end = end.AddDays(1);
            return end;
        }

        public bool RunsOn(DayOfWeek day) => (DaysOfWeekMask & (1 << (int)day)) != 0;

        private static void Validate(int cityId, string name, TimeSpan startTime, TimeSpan endTime, int daysOfWeekMask)
        {
            if (cityId <= 0)
                throw new ArgumentException("City ID must be greater than zero", nameof(cityId));
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Shift name cannot be empty", nameof(name));
            if (startTime < TimeSpan.Zero || startTime >= TimeSpan.FromDays(1))
                throw new ArgumentException("Start time must be within the day", nameof(startTime));
            if (endTime < TimeSpan.Zero || endTime >= TimeSpan.FromDays(1))
                throw new ArgumentException("End time must be within the day", nameof(endTime));
            if (startTime == endTime)
                throw new ArgumentException("Start and end time cannot be equal", nameof(endTime));
            if (daysOfWeekMask <= 0 || daysOfWeekMask > AllDays)
                throw new ArgumentException("Select at least one day", nameof(daysOfWeekMask));
        }
    }
}
