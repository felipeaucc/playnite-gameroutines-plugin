using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.ObjectModel;

namespace GameRoutines.Tests
{
    [TestClass]
    public class SettingsMigrationServiceTests
    {
        [TestMethod]
        public void Migrate_V1Settings_PreservesGlobalGameRoutineChecklistAndReminderValues()
        {
            var checklist = new ObservableCollection<ChecklistItemSettings>
            {
                TestSettingsFactory.Item(
                    TestSettingsFactory.ChecklistItemOneId, "Legacy item", true, 0)
            };
            var legacy = new LegacySettingsV1
            {
                SchemaVersion = 1,
                UseTasksAvailableTag = true,
                ShowBlockedManualStateWarning = false,
                ShowIncompleteCoverIndicator = false,
                TrackedGames = new ObservableCollection<LegacyTrackedGameSettingsV1>
                {
                    new LegacyTrackedGameSettingsV1
                    {
                        GameId = TestSettingsFactory.GameId,
                        CachedGameName = "Legacy V1",
                        Enabled = false,
                        ResetCadence = ResetCadence.Weekly,
                        ResetDay = DayOfWeek.Friday,
                        ResetTime = "06:30",
                        CurrentState = TaskState.INCOMPLETE,
                        LastResetProcessedLocal = TestSettingsFactory.ResetTimestamp,
                        Checklist = checklist,
                        AutomaticallyCompleteFromChecklist = true,
                        CompletedAutomaticallyByChecklist = false,
                        CustomReminderEnabled = true,
                        ReminderCadence = ReminderCadence.Weekly,
                        ReminderDay = DayOfWeek.Saturday,
                        ReminderTime = "18:45",
                        CustomReminderTitle = "Legacy title",
                        CustomReminderMessage = "Legacy message",
                        LastReminderProcessedLocal = TestSettingsFactory.ReminderTimestamp,
                        ShowIncompleteCoverIndicator = false
                    }
                }
            };

            var migrated = SettingsMigrationService.Migrate(legacy);

            Assert.AreEqual(2, migrated.SchemaVersion);
            Assert.IsTrue(migrated.UseTasksAvailableTag);
            Assert.IsFalse(migrated.ShowBlockedManualStateWarning);
            Assert.IsFalse(migrated.ShowIncompleteCoverIndicator);
            Assert.AreEqual(1, migrated.TrackedGames.Count);
            var game = migrated.TrackedGames[0];
            Assert.AreEqual(TestSettingsFactory.GameId, game.GameId);
            Assert.AreEqual("Legacy V1", game.CachedGameName);
            Assert.IsFalse(game.Enabled);
            Assert.IsTrue(game.CustomReminderEnabled);
            Assert.AreEqual(ReminderCadence.Weekly, game.ReminderCadence);
            Assert.AreEqual(DayOfWeek.Saturday, game.ReminderDay);
            Assert.AreEqual("18:45", game.ReminderTime);
            Assert.AreEqual("Legacy title", game.CustomReminderTitle);
            Assert.AreEqual("Legacy message", game.CustomReminderMessage);
            Assert.AreEqual(TestSettingsFactory.ReminderTimestamp, game.LastReminderProcessedLocal);
            Assert.IsFalse(game.ShowIncompleteCoverIndicator);
            Assert.AreEqual(1, game.Routines.Count);
            var routine = game.Routines[0];
            Assert.AreNotEqual(Guid.Empty, routine.Id);
            Assert.AreEqual("Weeklies", routine.Name);
            Assert.AreEqual(0, routine.Order);
            Assert.AreEqual(TaskState.INCOMPLETE, routine.CurrentState);
            Assert.AreEqual(ResetCadence.Weekly, routine.ResetCadence);
            Assert.AreEqual(DayOfWeek.Friday, routine.ResetDay);
            Assert.AreEqual("06:30", routine.ResetTime);
            Assert.AreEqual(TestSettingsFactory.ResetTimestamp, routine.LastResetProcessedLocal);
            Assert.AreSame(checklist, routine.Checklist);
            Assert.IsTrue(routine.AutomaticallyCompleteFromChecklist);
            Assert.IsTrue(routine.CountTowardOverallTaskStatus);
            Assert.IsFalse(routine.CompletedAutomaticallyByChecklist);
        }

        [DataTestMethod]
        [DataRow(1, "Dailies")]
        [DataRow(2, "Weeklies")]
        [DataRow(0, "Tasks")]
        [DataRow(3, "Tasks")]
        public void GetMigratedRoutineName_V1Cadence_ReturnsCurrentLegacyName(
            int cadence,
            string expected)
        {
            Assert.AreEqual(expected, SettingsMigrationService.GetMigratedRoutineName((ResetCadence)cadence));
        }

        [TestMethod]
        public void Migrate_V0Settings_MapsReadyTagWeeklyRoutineAndSecondaryReminder()
        {
            var checklist = new ObservableCollection<ChecklistItemSettings>
            {
                TestSettingsFactory.Item(
                    TestSettingsFactory.ChecklistItemTwoId, "Legacy V0 item", false, 0)
            };
            var legacy = new LegacySettingsV0
            {
                UseReadyTag = true,
                ShowBlockedManualStateWarning = false,
                ShowIncompleteCoverIndicator = false,
                TrackedGames = new ObservableCollection<LegacyTrackedGameSettings>
                {
                    new LegacyTrackedGameSettings
                    {
                        GameId = TestSettingsFactory.GameId,
                        CachedGameName = "Legacy V0",
                        Enabled = false,
                        WeeklyResetDay = DayOfWeek.Wednesday,
                        WeeklyResetTime = "07:15",
                        CurrentState = 1,
                        LastResetProcessedLocal = TestSettingsFactory.ResetTimestamp,
                        SecondaryReminderEnabled = true,
                        SecondaryReminderDay = DayOfWeek.Saturday,
                        SecondaryReminderTime = "20:30",
                        SecondaryNotificationTitle = "Secondary title",
                        SecondaryNotificationMessage = "Secondary message",
                        LastSecondaryReminderProcessedLocal = TestSettingsFactory.ReminderTimestamp,
                        Checklist = checklist,
                        AutomaticallyCompleteFromChecklist = true,
                        CompletedAutomaticallyByChecklist = true,
                        ShowIncompleteCoverIndicator = false
                    }
                }
            };

            var migrated = SettingsMigrationService.Migrate(legacy);

            Assert.AreEqual(2, migrated.SchemaVersion);
            Assert.IsTrue(migrated.UseTasksAvailableTag);
            Assert.IsFalse(migrated.ShowBlockedManualStateWarning);
            Assert.IsFalse(migrated.ShowIncompleteCoverIndicator);
            var game = migrated.TrackedGames[0];
            Assert.AreEqual(TestSettingsFactory.GameId, game.GameId);
            Assert.AreEqual("Legacy V0", game.CachedGameName);
            Assert.IsFalse(game.Enabled);
            Assert.IsTrue(game.CustomReminderEnabled);
            Assert.AreEqual(ReminderCadence.Weekly, game.ReminderCadence);
            Assert.AreEqual(DayOfWeek.Saturday, game.ReminderDay);
            Assert.AreEqual("20:30", game.ReminderTime);
            Assert.AreEqual("Secondary title", game.CustomReminderTitle);
            Assert.AreEqual("Secondary message", game.CustomReminderMessage);
            Assert.AreEqual(TestSettingsFactory.ReminderTimestamp, game.LastReminderProcessedLocal);
            Assert.IsFalse(game.ShowIncompleteCoverIndicator);
            var routine = game.Routines[0];
            Assert.AreEqual("Weeklies", routine.Name);
            Assert.AreEqual(TaskState.COMPLETE, routine.CurrentState);
            Assert.AreEqual(ResetCadence.Weekly, routine.ResetCadence);
            Assert.AreEqual(DayOfWeek.Wednesday, routine.ResetDay);
            Assert.AreEqual("07:15", routine.ResetTime);
            Assert.AreEqual(TestSettingsFactory.ResetTimestamp, routine.LastResetProcessedLocal);
            Assert.AreSame(checklist, routine.Checklist);
            Assert.IsTrue(routine.AutomaticallyCompleteFromChecklist);
            Assert.IsTrue(routine.CountTowardOverallTaskStatus);
            Assert.IsTrue(routine.CompletedAutomaticallyByChecklist);
        }

        [DataTestMethod]
        [DataRow(1, 1)]
        [DataRow(0, 0)]
        [DataRow(2, 0)]
        [DataRow(-1, 0)]
        public void Migrate_V0IntegerState_MapsOneToCompleteAndOthersToIncomplete(
            int legacyState,
            int expectedState)
        {
            var legacy = new LegacySettingsV0
            {
                TrackedGames = new ObservableCollection<LegacyTrackedGameSettings>
                {
                    new LegacyTrackedGameSettings { CurrentState = legacyState }
                }
            };

            var migrated = SettingsMigrationService.Migrate(legacy);

            Assert.AreEqual((TaskState)expectedState, migrated.TrackedGames[0].Routines[0].CurrentState);
        }

        [TestMethod]
        public void Migrate_NullLegacyCollections_ReturnsValidEmptyCollections()
        {
            var migratedV1 = SettingsMigrationService.Migrate(new LegacySettingsV1 { TrackedGames = null });
            var migratedV0 = SettingsMigrationService.Migrate(new LegacySettingsV0 { TrackedGames = null });

            Assert.IsNotNull(migratedV1.TrackedGames);
            Assert.AreEqual(0, migratedV1.TrackedGames.Count);
            Assert.IsNotNull(migratedV0.TrackedGames);
            Assert.AreEqual(0, migratedV0.TrackedGames.Count);
        }

        [TestMethod]
        public void Migrate_NullLegacyChecklist_ReturnsValidEmptyChecklist()
        {
            var legacyV1 = new LegacySettingsV1
            {
                TrackedGames = new ObservableCollection<LegacyTrackedGameSettingsV1>
                {
                    new LegacyTrackedGameSettingsV1 { Checklist = null }
                }
            };
            var legacyV0 = new LegacySettingsV0
            {
                TrackedGames = new ObservableCollection<LegacyTrackedGameSettings>
                {
                    new LegacyTrackedGameSettings { Checklist = null }
                }
            };

            Assert.AreEqual(0, SettingsMigrationService.Migrate(legacyV1).TrackedGames[0].Routines[0].Checklist.Count);
            Assert.AreEqual(0, SettingsMigrationService.Migrate(legacyV0).TrackedGames[0].Routines[0].Checklist.Count);
        }

        [TestMethod]
        public void CreateEmpty_NoLegacySettings_ReturnsSchemaTwoEmptySettings()
        {
            var settings = SettingsMigrationService.CreateEmpty();

            Assert.AreEqual(2, settings.SchemaVersion);
            Assert.IsNotNull(settings.TrackedGames);
            Assert.AreEqual(0, settings.TrackedGames.Count);
        }
    }
}
