using System;
using System.Globalization;
using System.Linq;

namespace GameRoutines
{
    public static class ScheduleCalculator
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

        public static bool TryGetMostRecentOccurrence(
            DateTime localNow,
            ResetCadence cadence,
            DayOfWeek day,
            TimeSpan time,
            DateTime? biWeeklyAnchorLocal,
            out DateTime occurrence)
        {
            if (cadence == ResetCadence.Never)
            {
                occurrence = default(DateTime);
                return false;
            }

            if (cadence == ResetCadence.BiWeekly)
            {
                return TryGetMostRecentBiWeeklyOccurrence(
                    localNow,
                    biWeeklyAnchorLocal,
                    out occurrence);
            }

            occurrence = cadence == ResetCadence.Daily
                ? GetMostRecentDailyOccurrence(localNow, time)
                : GetMostRecentWeeklyOccurrence(localNow, day, time);
            return true;
        }

        public static bool TryGetMostRecentOccurrence(
            DateTime localNow,
            ReminderCadence cadence,
            DayOfWeek day,
            TimeSpan time,
            DateTime? biWeeklyAnchorLocal,
            out DateTime occurrence)
        {
            if (cadence == ReminderCadence.BiWeekly)
            {
                return TryGetMostRecentBiWeeklyOccurrence(
                    localNow,
                    biWeeklyAnchorLocal,
                    out occurrence);
            }

            occurrence = cadence == ReminderCadence.Daily
                ? GetMostRecentDailyOccurrence(localNow, time)
                : GetMostRecentWeeklyOccurrence(localNow, day, time);
            return true;
        }

        public static DateTime GetFirstFutureWeeklyOccurrence(
            DateTime localNow,
            DayOfWeek day,
            TimeSpan time)
        {
            var daysUntilScheduledDay = ((int)day - (int)localNow.DayOfWeek + 7) % 7;
            var occurrence = localNow.Date.AddDays(daysUntilScheduledDay).Add(time);
            if (occurrence <= localNow)
            {
                occurrence = occurrence.AddDays(7);
            }

            return DateTime.SpecifyKind(occurrence, DateTimeKind.Local);
        }

        private static DateTime GetMostRecentDailyOccurrence(DateTime localNow, TimeSpan time)
        {
            var occurrence = localNow.Date.Add(time);
            if (occurrence > localNow)
            {
                occurrence = occurrence.AddDays(-1);
            }

            return DateTime.SpecifyKind(occurrence, DateTimeKind.Local);
        }

        private static DateTime GetMostRecentWeeklyOccurrence(
            DateTime localNow,
            DayOfWeek day,
            TimeSpan time)
        {
            var daysSinceScheduledDay = ((int)localNow.DayOfWeek - (int)day + 7) % 7;
            var occurrence = localNow.Date.AddDays(-daysSinceScheduledDay).Add(time);
            if (occurrence > localNow)
            {
                occurrence = occurrence.AddDays(-7);
            }

            return DateTime.SpecifyKind(occurrence, DateTimeKind.Local);
        }

        private static bool TryGetMostRecentBiWeeklyOccurrence(
            DateTime localNow,
            DateTime? anchorLocal,
            out DateTime occurrence)
        {
            if (!anchorLocal.HasValue)
            {
                occurrence = default(DateTime);
                return false;
            }

            var anchor = DateTime.SpecifyKind(anchorLocal.Value, DateTimeKind.Local);
            if (localNow < anchor)
            {
                occurrence = default(DateTime);
                return false;
            }

            var intervalTicks = TimeSpan.FromDays(14).Ticks;
            var intervals = (localNow.Ticks - anchor.Ticks) / intervalTicks;
            occurrence = DateTime.SpecifyKind(
                anchor.AddTicks(intervals * intervalTicks),
                DateTimeKind.Local);
            return true;
        }

        public static bool IsOccurrenceDue(DateTime? lastProcessedLocal, DateTime occurrenceLocal)
        {
            return !lastProcessedLocal.HasValue || lastProcessedLocal.Value < occurrenceLocal;
        }
    }
}
