namespace Application.Features.Shifts.DTOs
{
    public class ShiftDto
    {
        public int ShiftId { get; set; }
        public int CityId { get; set; }
        public string CityName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        /// <summary>Local (Egypt) time, "HH:mm".</summary>
        public string StartTime { get; set; } = string.Empty;

        /// <summary>Local (Egypt) time, "HH:mm". Earlier than StartTime means the shift crosses midnight.</summary>
        public string EndTime { get; set; } = string.Empty;

        /// <summary>Bitmask over DayOfWeek: Sunday = 1, Monday = 2, … Saturday = 64.</summary>
        public int DaysOfWeekMask { get; set; }
        public bool IsActive { get; set; }
        public bool IsRunningNow { get; set; }
        public List<ShiftRiderDto> Riders { get; set; } = new();
    }

    public class ShiftRiderDto
    {
        public int DeliveryId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string MobileNumber { get; set; } = string.Empty;
        public bool IsOnline { get; set; }
    }
}
