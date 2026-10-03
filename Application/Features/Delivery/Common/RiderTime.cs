namespace Application.Features.Delivery.Common
{
    /// <summary>
    /// Shift times are entered in Egypt local time. Converts UTC to that clock on
    /// Windows ("Egypt Standard Time") and Linux ("Africa/Cairo") hosts alike.
    /// </summary>
    public static class RiderTime
    {
        private static readonly TimeZoneInfo Zone = Resolve();

        public static DateTime LocalNow() => ToLocal(DateTime.UtcNow);

        public static DateTime ToLocal(DateTime utc) =>
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

        /// <summary>
        /// Local → UTC. A local time skipped by the spring-forward hour (e.g. 00:00 on the DST night)
        /// does not throw: it is moved forward an hour, which is the instant the wall clock jumps to.
        /// </summary>
        public static DateTime ToUtc(DateTime local)
        {
            var l = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            if (Zone.IsInvalidTime(l))
                l = l.AddHours(1);
            return DateTime.SpecifyKind(l - Zone.GetUtcOffset(l), DateTimeKind.Utc);
        }

        private static TimeZoneInfo Resolve()
        {
            foreach (var id in new[] { "Egypt Standard Time", "Africa/Cairo" })
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(id);
                }
                catch (TimeZoneNotFoundException)
                {
                }
                catch (InvalidTimeZoneException)
                {
                }
            }

            return TimeZoneInfo.CreateCustomTimeZone("Egypt", TimeSpan.FromHours(2), "Egypt", "Egypt");
        }
    }
}
