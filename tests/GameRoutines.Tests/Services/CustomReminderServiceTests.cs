using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace GameRoutines.Tests
{
    [TestClass]
    public class CustomReminderServiceTests
    {
        [TestMethod]
        public void CreateEditableCopy_SourceWithReminder_CopiesEveryReminderField()
        {
            var source = CreateValidReminder();

            var copy = CustomReminderService.CreateEditableCopy(source);

            Assert.AreEqual(source.GameId, copy.GameId);
            Assert.AreEqual(source.CachedGameName, copy.CachedGameName);
            Assert.AreEqual(source.CustomReminderEnabled, copy.CustomReminderEnabled);
            Assert.AreEqual(source.ReminderCadence, copy.ReminderCadence);
            Assert.AreEqual(source.ReminderDay, copy.ReminderDay);
            Assert.AreEqual(source.ReminderTime, copy.ReminderTime);
            Assert.AreEqual(source.CustomReminderTitle, copy.CustomReminderTitle);
            Assert.AreEqual(source.CustomReminderMessage, copy.CustomReminderMessage);
            Assert.AreEqual(source.LastReminderProcessedLocal, copy.LastReminderProcessedLocal);
            Assert.AreEqual(source.BiWeeklyReminderAnchorLocal, copy.BiWeeklyReminderAnchorLocal);
        }

        [TestMethod]
        public void CreateEditableCopy_SourceWithRoutines_DoesNotCopyRoutineCollection()
        {
            var source = CreateValidReminder();
            source.Routines.Add(TestSettingsFactory.Routine(
                TestSettingsFactory.DailyRoutineId, "Dailies", 0));

            var copy = CustomReminderService.CreateEditableCopy(source);

            Assert.AreEqual(0, copy.Routines.Count);
            Assert.AreNotSame(source.Routines, copy.Routines);
        }

        [TestMethod]
        public void CreateEditableCopy_NullSource_ReturnsNull()
        {
            Assert.IsNull(CustomReminderService.CreateEditableCopy(null));
        }

        [TestMethod]
        public void Apply_EditedReminder_ChangesOnlyReminderFields()
        {
            var targetRoutine = TestSettingsFactory.Routine(
                TestSettingsFactory.DailyRoutineId, "Dailies", 0, TaskState.INCOMPLETE);
            var target = TestSettingsFactory.Game(targetRoutine);
            target.Enabled = false;
            target.ShowIncompleteCoverIndicator = false;
            var source = CreateValidReminder();

            CustomReminderService.Apply(source, target);

            Assert.AreEqual(source.CustomReminderEnabled, target.CustomReminderEnabled);
            Assert.AreEqual(source.ReminderCadence, target.ReminderCadence);
            Assert.AreEqual(source.ReminderDay, target.ReminderDay);
            Assert.AreEqual(source.ReminderTime, target.ReminderTime);
            Assert.AreEqual(source.CustomReminderTitle, target.CustomReminderTitle);
            Assert.AreEqual(source.CustomReminderMessage, target.CustomReminderMessage);
            Assert.AreEqual(source.LastReminderProcessedLocal, target.LastReminderProcessedLocal);
            Assert.AreEqual(source.BiWeeklyReminderAnchorLocal, target.BiWeeklyReminderAnchorLocal);
            Assert.IsFalse(target.Enabled);
            Assert.IsFalse(target.ShowIncompleteCoverIndicator);
            Assert.AreSame(targetRoutine, target.Routines[0]);
        }

        [TestMethod]
        public void Apply_NullSourceOrTarget_DoesNothing()
        {
            var target = CreateValidReminder();
            var title = target.CustomReminderTitle;

            CustomReminderService.Apply(null, target);
            CustomReminderService.Apply(target, null);

            Assert.AreEqual(title, target.CustomReminderTitle);
        }

        [TestMethod]
        public void Validate_DisabledReminderWithBlankContent_ReturnsNoErrors()
        {
            var reminder = CreateValidReminder();
            reminder.CustomReminderEnabled = false;
            SetPrivateField(reminder, "reminderTime", "not a time");
            reminder.CustomReminderTitle = " ";
            reminder.CustomReminderMessage = null;

            var errors = Validate(reminder);

            Assert.AreEqual(0, errors.Count);
        }

        [TestMethod]
        public void Validate_EnabledReminderWithNonCanonicalTime_ReturnsTimeError()
        {
            var reminder = CreateValidReminder();
            SetPrivateField(reminder, "reminderTime", "7:05");

            CollectionAssert.Contains(
                Validate(reminder),
                "Fixed Game: custom reminder time must use 24-hour HH:mm format.");
        }

        [DataTestMethod]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(true, true)]
        public void Validate_EnabledReminderWithBlankTitleOrMessage_ReturnsRelevantErrors(
            bool blankTitle,
            bool blankMessage)
        {
            var reminder = CreateValidReminder();
            if (blankTitle)
            {
                reminder.CustomReminderTitle = " ";
            }
            if (blankMessage)
            {
                reminder.CustomReminderMessage = null;
            }

            var errors = Validate(reminder);

            Assert.AreEqual(blankTitle, errors.Exists(a => a.Contains("title cannot be empty")));
            Assert.AreEqual(blankMessage, errors.Exists(a => a.Contains("message cannot be empty")));
        }

        [TestMethod]
        public void Validate_BiWeeklyReminderWithoutAnchor_ReturnsAnchorError()
        {
            var reminder = CreateValidReminder();
            reminder.ReminderCadence = ReminderCadence.BiWeekly;
            reminder.BiWeeklyReminderAnchorLocal = null;

            CollectionAssert.Contains(
                Validate(reminder),
                "Fixed Game: a Biweekly reminder start date is required.");
        }

        [TestMethod]
        public void Validate_TitleAtBoundaryAndOverBoundary_ReturnsExpectedResult()
        {
            var reminder = CreateValidReminder();
            reminder.CustomReminderTitle = new string('T', TrackedGameSettings.MaximumCustomReminderTitleLength);
            Assert.IsFalse(Validate(reminder).Exists(a => a.Contains("title cannot exceed")));

            SetPrivateField(
                reminder,
                "customReminderTitle",
                new string('T', TrackedGameSettings.MaximumCustomReminderTitleLength + 1));
            Assert.IsTrue(Validate(reminder).Exists(a => a.Contains("title cannot exceed 40")));
        }

        [TestMethod]
        public void Validate_MessageAtBoundaryAndOverBoundary_ReturnsExpectedResult()
        {
            var reminder = CreateValidReminder();
            reminder.CustomReminderMessage = new string('M', TrackedGameSettings.MaximumCustomReminderMessageLength);
            Assert.IsFalse(Validate(reminder).Exists(a => a.Contains("message cannot exceed")));

            SetPrivateField(
                reminder,
                "customReminderMessage",
                new string('M', TrackedGameSettings.MaximumCustomReminderMessageLength + 1));
            Assert.IsTrue(Validate(reminder).Exists(a => a.Contains("message cannot exceed 160")));
        }

        [TestMethod]
        public void Validate_InvalidReminderCadence_ReturnsEnumError()
        {
            var reminder = CreateValidReminder();
            reminder.ReminderCadence = (ReminderCadence)99;

            CollectionAssert.Contains(
                Validate(reminder),
                "Fixed Game: reminder frequency is invalid.");
        }

        [TestMethod]
        public void Validate_ValidEnabledReminder_ReturnsNoErrors()
        {
            Assert.AreEqual(0, Validate(CreateValidReminder()).Count);
        }

        private static TrackedGameSettings CreateValidReminder()
        {
            return new TrackedGameSettings
            {
                GameId = TestSettingsFactory.GameId,
                CachedGameName = "Fixed Game",
                CustomReminderEnabled = true,
                ReminderCadence = ReminderCadence.Weekly,
                ReminderDay = DayOfWeek.Friday,
                ReminderTime = "18:45",
                CustomReminderTitle = "Fixed title",
                CustomReminderMessage = "Fixed message",
                LastReminderProcessedLocal = TestSettingsFactory.ReminderTimestamp,
                BiWeeklyReminderAnchorLocal = DateTime.SpecifyKind(
                    new DateTime(2025, 12, 19, 18, 45, 0),
                    DateTimeKind.Local),
                Routines = new ObservableCollection<RoutineSettings>()
            };
        }

        private static List<string> Validate(TrackedGameSettings reminder)
        {
            var errors = new List<string>();
            CustomReminderService.Validate(reminder, "Fixed Game", errors);
            return errors;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Expected private field {fieldName}.");
            field.SetValue(target, value);
        }
    }
}
