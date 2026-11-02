using System.ComponentModel.DataAnnotations;

namespace ExchangeRateService.API.Services
{
    public class DailyRateOptions
    {
        public bool Enabled { get; set; } = true;
        [Range(0, 23)]
        public int CheckHourUtc { get; set; } = 14;

        [Range(1, 1440)]
        public int RetryMinutes { get; set; } = 60;

        public DateOnly ScheduledDate(DateTimeOffset now)
        {
            return DateOnly.FromDateTime(
                now.UtcDateTime.AddHours(-CheckHourUtc));
        }

        public DateTimeOffset NextCheck(DateTimeOffset now)
        {
            var next = now.UtcDateTime.Date.AddHours(CheckHourUtc);
            return new DateTimeOffset(
                next > now.UtcDateTime ? next : next.AddDays(1),
                TimeSpan.Zero);
        }
    }
}
