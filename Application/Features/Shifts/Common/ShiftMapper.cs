using Application.Features.Delivery.Common;
using Application.Features.Shifts.DTOs;
using System.Globalization;

namespace Application.Features.Shifts.Common
{
    public static class ShiftMapper
    {
        public static string FormatTime(TimeSpan time) => time.ToString(@"hh\:mm", CultureInfo.InvariantCulture);

        public static bool TryParseTime(string? value, out TimeSpan time) =>
            TimeSpan.TryParseExact(value?.Trim(), new[] { @"hh\:mm", @"h\:mm", @"hh\:mm\:ss" }, CultureInfo.InvariantCulture, out time);

        public static ShiftDto ToDto(Domain.Models.Shift shift, string cityName, IEnumerable<Domain.Models.Delivery> riders)
        {
            return new ShiftDto
            {
                ShiftId = shift.ShiftId,
                CityId = shift.CityId,
                CityName = cityName,
                Name = shift.Name,
                StartTime = FormatTime(shift.StartTime),
                EndTime = FormatTime(shift.EndTime),
                DaysOfWeekMask = shift.DaysOfWeekMask,
                IsActive = shift.IsActive,
                IsRunningNow = shift.IsRunningAt(RiderTime.LocalNow()),
                Riders = riders
                    .OrderBy(r => r.FullName)
                    .Select(r => new ShiftRiderDto
                    {
                        DeliveryId = r.DeliveryId,
                        FullName = r.FullName,
                        MobileNumber = r.MobileNumber,
                        IsOnline = r.IsOnline
                    })
                    .ToList()
            };
        }
    }
}
