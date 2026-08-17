using System;
using System.Globalization;
using System.Linq;

namespace WeeklyManager
{
    public static class WeeklyScheduleCalculator
    {
        private static readonly string[] AcceptedTimeFormats =
        {
            @"h\:mm",
            @"hh\:mm",
            @"h\:mm\:ss",
            @"hh\:mm\:ss"
        };

        public static bool TryParseLocalTime(string value, out TimeSpan time)
        {
            if (TimeSpan.TryParseExact(
                value?.Trim(),
                AcceptedTimeFormats,
                CultureInfo.InvariantCulture,
                out time) &&
                time >= TimeSpan.Zero &&
                time < TimeSpan.FromDays(1))
            {
                return true;
            }

            time = default(TimeSpan);
            return false;
        }

        public static bool TryNormalizeTimeInput(string value, out string normalizedValue)
        {
            var input = value?.Trim();
            string hourText;
            string minuteText;

            if (input?.Length == 4 && input.All(IsAsciiDigit))
            {
                hourText = input.Substring(0, 2);
                minuteText = input.Substring(2, 2);
            }
            else
            {
                var separatorIndex = input?.IndexOf(':') ?? -1;
                if (separatorIndex < 1 || separatorIndex > 2 ||
                    input.IndexOf(':', separatorIndex + 1) >= 0)
                {
                    normalizedValue = null;
                    return false;
                }

                hourText = input.Substring(0, separatorIndex);
                minuteText = input.Substring(separatorIndex + 1);
                if (minuteText.Length != 2 ||
                    !hourText.All(IsAsciiDigit) ||
                    !minuteText.All(IsAsciiDigit))
                {
                    normalizedValue = null;
                    return false;
                }
            }

            if (!int.TryParse(hourText, NumberStyles.None, CultureInfo.InvariantCulture, out var hour) ||
                !int.TryParse(minuteText, NumberStyles.None, CultureInfo.InvariantCulture, out var minute) ||
                hour < 0 || hour > 23 || minute < 0 || minute > 59)
            {
                normalizedValue = null;
                return false;
            }

            normalizedValue = $"{hour:00}:{minute:00}";
            return true;
        }

        private static bool IsAsciiDigit(char value)
        {
            return value >= '0' && value <= '9';
        }

        public static DateTime GetMostRecentOccurrence(DateTime localNow, DayOfWeek day, TimeSpan time)
        {
            var daysSinceScheduledDay = ((int)localNow.DayOfWeek - (int)day + 7) % 7;
            var occurrence = localNow.Date.AddDays(-daysSinceScheduledDay).Add(time);
            if (occurrence > localNow)
            {
                occurrence = occurrence.AddDays(-7);
            }

            return DateTime.SpecifyKind(occurrence, DateTimeKind.Local);
        }

        public static bool IsOccurrenceDue(DateTime? lastProcessedLocal, DateTime occurrenceLocal)
        {
            return !lastProcessedLocal.HasValue || lastProcessedLocal.Value < occurrenceLocal;
        }
    }
}
