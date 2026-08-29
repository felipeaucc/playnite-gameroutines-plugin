using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Globalization;

namespace GameRoutines.Tests
{
    [TestClass]
    public class ScheduleCalculatorTests
    {
        [DataTestMethod]
        [DataRow("0:00", 0, 0)]
        [DataRow("00:00", 0, 0)]
        [DataRow("7:05", 7, 5)]
        [DataRow("07:05", 7, 5)]
        [DataRow("23:59", 23, 59)]
        [DataRow(" 09:30 ", 9, 30)]
        [DataRow("9:30:00", 9, 30)]
        public void TryParseLocalTime_AcceptedValue_ReturnsExpectedTime(
            string input,
            int expectedHour,
            int expectedMinute)
        {
            var result = ScheduleCalculator.TryParseLocalTime(input, out var time);

            Assert.IsTrue(result);
            Assert.AreEqual(new TimeSpan(expectedHour, expectedMinute, 0), time);
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("12:00:00:00")]
        [DataRow("１２:００")]
        [DataRow("24:00")]
        [DataRow("23:60")]
        [DataRow("7:")]
        [DataRow(":30")]
        [DataRow("0705")]
        public void TryParseLocalTime_InvalidValue_ReturnsFalse(string input)
        {
            Assert.IsFalse(ScheduleCalculator.TryParseLocalTime(input, out var time));
            Assert.AreEqual(TimeSpan.Zero, time);
        }

        [DataTestMethod]
        [DataRow("0000", "00:00")]
        [DataRow("0705", "07:05")]
        [DataRow("2359", "23:59")]
        [DataRow("0:00", "00:00")]
        [DataRow("7:05", "07:05")]
        [DataRow("07:05", "07:05")]
        [DataRow(" 9:30 ", "09:30")]
        public void TryNormalizeTimeInput_AcceptedValue_ReturnsCanonicalValue(
            string input,
            string expected)
        {
            Assert.IsTrue(ScheduleCalculator.TryNormalizeTimeInput(input, out var normalized));
            Assert.AreEqual(expected, normalized);
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("7:05:00")]
        [DataRow("07:05:00")]
        [DataRow("7::05")]
        [DataRow("１２:００")]
        [DataRow("2400")]
        [DataRow("2360")]
        [DataRow("705")]
        [DataRow("7:5")]
        [DataRow("7:")]
        public void TryNormalizeTimeInput_InvalidValue_ReturnsFalse(string input)
        {
            Assert.IsFalse(ScheduleCalculator.TryNormalizeTimeInput(input, out var normalized));
            Assert.IsNull(normalized);
        }

        [TestMethod]
        public void TryGetMostRecentOccurrence_Never_ReturnsFalse()
        {
            var now = Local("2026-01-01 12:00");

            var result = ScheduleCalculator.TryGetMostRecentOccurrence(
                now,
                ResetCadence.Never,
                DayOfWeek.Monday,
                TimeSpan.Zero,
                null,
                out var occurrence);

            Assert.IsFalse(result);
            Assert.AreEqual(default(DateTime), occurrence);
        }

        [DataTestMethod]
        [DataRow("2026-05-15 08:59", "09:00", "2026-05-14 09:00")]
        [DataRow("2026-05-15 09:00", "09:00", "2026-05-15 09:00")]
        [DataRow("2026-05-15 09:01", "09:00", "2026-05-15 09:00")]
        [DataRow("2026-06-01 00:00", "23:59", "2026-05-31 23:59")]
        [DataRow("2027-01-01 00:00", "23:59", "2026-12-31 23:59")]
        public void TryGetMostRecentOccurrence_DailyBoundary_ReturnsExpectedOccurrence(
            string nowText,
            string timeText,
            string expectedText)
        {
            Assert.IsTrue(ScheduleCalculator.TryGetMostRecentOccurrence(
                Local(nowText),
                ResetCadence.Daily,
                DayOfWeek.Monday,
                TimeSpan.Parse(timeText, CultureInfo.InvariantCulture),
                null,
                out var occurrence));

            Assert.AreEqual(Local(expectedText), occurrence);
            Assert.AreEqual(DateTimeKind.Local, occurrence.Kind);
        }

        [DataTestMethod]
        [DataRow("2026-05-18 08:59", "2026-05-11 09:00")]
        [DataRow("2026-05-18 09:00", "2026-05-18 09:00")]
        [DataRow("2026-05-18 09:01", "2026-05-18 09:00")]
        [DataRow("2026-06-01 08:00", "2026-05-25 09:00")]
        [DataRow("2027-01-01 08:00", "2026-12-28 09:00")]
        public void TryGetMostRecentOccurrence_WeeklyBoundary_ReturnsExpectedOccurrence(
            string nowText,
            string expectedText)
        {
            Assert.IsTrue(ScheduleCalculator.TryGetMostRecentOccurrence(
                Local(nowText),
                ResetCadence.Weekly,
                DayOfWeek.Monday,
                TimeSpan.FromHours(9),
                null,
                out var occurrence));

            Assert.AreEqual(Local(expectedText), occurrence);
            Assert.AreEqual(DateTimeKind.Local, occurrence.Kind);
        }

        [DataTestMethod]
        [DataRow("2026-05-18 08:59", 1, "2026-05-18 09:00")]
        [DataRow("2026-05-18 09:00", 1, "2026-05-25 09:00")]
        [DataRow("2026-05-18 09:01", 1, "2026-05-25 09:00")]
        [DataRow("2026-05-19 12:00", 5, "2026-05-22 09:00")]
        [DataRow("2026-12-31 12:00", 1, "2027-01-04 09:00")]
        public void GetFirstFutureWeeklyOccurrence_FixedBoundary_ReturnsExpectedOccurrence(
            string nowText,
            int day,
            string expectedText)
        {
            var occurrence = ScheduleCalculator.GetFirstFutureWeeklyOccurrence(
                Local(nowText),
                (DayOfWeek)day,
                TimeSpan.FromHours(9));

            Assert.AreEqual(Local(expectedText), occurrence);
            Assert.AreEqual(DateTimeKind.Local, occurrence.Kind);
        }

        [TestMethod]
        public void TryGetMostRecentOccurrence_BiWeeklyMissingOrFutureAnchor_ReturnsFalse()
        {
            var now = Local("2026-01-10 09:00");

            Assert.IsFalse(ScheduleCalculator.TryGetMostRecentOccurrence(
                now, ResetCadence.BiWeekly, DayOfWeek.Monday, TimeSpan.Zero, null, out _));
            Assert.IsFalse(ScheduleCalculator.TryGetMostRecentOccurrence(
                now, ResetCadence.BiWeekly, DayOfWeek.Monday, TimeSpan.Zero,
                Local("2026-01-11 09:00"), out _));
        }

        [DataTestMethod]
        [DataRow("2025-12-20 09:00", "2025-12-20 09:00", "2025-12-20 09:00")]
        [DataRow("2025-12-20 09:00", "2025-12-25 12:00", "2025-12-20 09:00")]
        [DataRow("2025-12-20 09:00", "2026-01-03 09:00", "2026-01-03 09:00")]
        [DataRow("2025-12-20 09:00", "2026-03-30 18:00", "2026-03-28 09:00")]
        [DataRow("2026-12-20 09:00", "2027-01-18 08:00", "2027-01-17 09:00")]
        public void TryGetMostRecentOccurrence_BiWeeklyIntervals_ReturnsExpectedOccurrence(
            string anchorText,
            string nowText,
            string expectedText)
        {
            Assert.IsTrue(ScheduleCalculator.TryGetMostRecentOccurrence(
                Local(nowText),
                ResetCadence.BiWeekly,
                DayOfWeek.Monday,
                TimeSpan.Zero,
                Local(anchorText),
                out var occurrence));

            Assert.AreEqual(Local(expectedText), occurrence);
            Assert.AreEqual(DateTimeKind.Local, occurrence.Kind);
        }

        [TestMethod]
        public void IsOccurrenceDue_NoEarlierEqualOrLaterTimestamp_ReturnsExpectedResult()
        {
            var occurrence = Local("2026-04-10 09:00");

            Assert.IsTrue(ScheduleCalculator.IsOccurrenceDue(null, occurrence));
            Assert.IsTrue(ScheduleCalculator.IsOccurrenceDue(occurrence.AddTicks(-1), occurrence));
            Assert.IsFalse(ScheduleCalculator.IsOccurrenceDue(occurrence, occurrence));
            Assert.IsFalse(ScheduleCalculator.IsOccurrenceDue(occurrence.AddTicks(1), occurrence));
        }

        [DataTestMethod]
        [DataRow(0, "2026-05-15 08:59", "2026-05-14 09:00")]
        [DataRow(1, "2026-05-18 08:59", "2026-05-11 09:00")]
        [DataRow(2, "2026-01-17 09:00", "2026-01-17 09:00")]
        public void TryGetMostRecentOccurrence_ReminderCadence_ReturnsEquivalentOccurrence(
            int cadence,
            string nowText,
            string expectedText)
        {
            var anchor = cadence == (int)ReminderCadence.BiWeekly
                ? (DateTime?)Local("2026-01-03 09:00")
                : null;

            Assert.IsTrue(ScheduleCalculator.TryGetMostRecentOccurrence(
                Local(nowText),
                (ReminderCadence)cadence,
                DayOfWeek.Monday,
                TimeSpan.FromHours(9),
                anchor,
                out var occurrence));

            Assert.AreEqual(Local(expectedText), occurrence);
        }

        [TestMethod]
        public void TryGetMostRecentOccurrence_FirstBiWeeklyReminderAnchor_PreventsEarlyAndDuplicateProcessing()
        {
            var anchor = Local("2026-01-03 09:00");

            Assert.IsFalse(ScheduleCalculator.TryGetMostRecentOccurrence(
                anchor.AddTicks(-1), ReminderCadence.BiWeekly, DayOfWeek.Saturday,
                TimeSpan.FromHours(9), anchor, out _));
            Assert.IsTrue(ScheduleCalculator.TryGetMostRecentOccurrence(
                Local("2026-02-01 12:00"), ReminderCadence.BiWeekly, DayOfWeek.Saturday,
                TimeSpan.FromHours(9), anchor, out var missedOccurrence));
            Assert.AreEqual(Local("2026-01-31 09:00"), missedOccurrence);
            Assert.IsTrue(ScheduleCalculator.IsOccurrenceDue(Local("2026-01-17 09:00"), missedOccurrence));
            Assert.IsFalse(ScheduleCalculator.IsOccurrenceDue(missedOccurrence, missedOccurrence));
        }

        private static DateTime Local(string value)
        {
            return DateTime.SpecifyKind(
                DateTime.ParseExact(value, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                DateTimeKind.Local);
        }
    }
}
