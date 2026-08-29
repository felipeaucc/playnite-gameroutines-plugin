using System;
using System.Collections.ObjectModel;

namespace GameRoutines
{
    internal static class SettingsMigrationService
    {
        internal static GameRoutinesSettings Migrate(LegacySettingsV1 legacy)
        {
            var migrated = new GameRoutinesSettings
            {
                SchemaVersion = GameRoutinesSettings.CurrentSchemaVersion,
                UseTasksAvailableTag = legacy.UseTasksAvailableTag,
                ShowBlockedManualStateWarning = legacy.ShowBlockedManualStateWarning,
                ShowIncompleteCoverIndicator = legacy.ShowIncompleteCoverIndicator
            };

            foreach (var oldGame in legacy.TrackedGames ?? new ObservableCollection<LegacyTrackedGameSettingsV1>())
            {
                var trackedGame = new TrackedGameSettings
                {
                    GameId = oldGame.GameId,
                    CachedGameName = oldGame.CachedGameName,
                    Enabled = oldGame.Enabled,
                    CustomReminderEnabled = oldGame.CustomReminderEnabled,
                    ReminderCadence = oldGame.ReminderCadence,
                    ReminderDay = oldGame.ReminderDay,
                    ReminderTime = oldGame.ReminderTime,
                    CustomReminderTitle = oldGame.CustomReminderTitle,
                    CustomReminderMessage = oldGame.CustomReminderMessage,
                    LastReminderProcessedLocal = oldGame.LastReminderProcessedLocal,
                    ShowIncompleteCoverIndicator = oldGame.ShowIncompleteCoverIndicator
                };
                trackedGame.Routines.Add(new RoutineSettings
                {
                    Id = Guid.NewGuid(),
                    Name = GetMigratedRoutineName(oldGame.ResetCadence),
                    Order = 0,
                    CurrentState = oldGame.CurrentState,
                    ResetCadence = oldGame.ResetCadence,
                    ResetDay = oldGame.ResetDay,
                    ResetTime = oldGame.ResetTime,
                    LastResetProcessedLocal = oldGame.LastResetProcessedLocal,
                    Checklist = oldGame.Checklist ?? new ObservableCollection<ChecklistItemSettings>(),
                    AutomaticallyCompleteFromChecklist = oldGame.AutomaticallyCompleteFromChecklist,
                    CountTowardOverallTaskStatus = true,
                    CompletedAutomaticallyByChecklist = oldGame.CompletedAutomaticallyByChecklist
                });
                migrated.TrackedGames.Add(trackedGame);
            }

            return migrated;
        }

        internal static GameRoutinesSettings Migrate(LegacySettingsV0 legacy)
        {
            var migrated = new GameRoutinesSettings
            {
                SchemaVersion = GameRoutinesSettings.CurrentSchemaVersion,
                UseTasksAvailableTag = legacy.UseReadyTag,
                ShowBlockedManualStateWarning = legacy.ShowBlockedManualStateWarning,
                ShowIncompleteCoverIndicator = legacy.ShowIncompleteCoverIndicator
            };

            foreach (var oldGame in legacy.TrackedGames ?? new ObservableCollection<LegacyTrackedGameSettings>())
            {
                var trackedGame = new TrackedGameSettings
                {
                    GameId = oldGame.GameId,
                    CachedGameName = oldGame.CachedGameName,
                    Enabled = oldGame.Enabled,
                    CustomReminderEnabled = oldGame.SecondaryReminderEnabled,
                    ReminderCadence = ReminderCadence.Weekly,
                    ReminderDay = oldGame.SecondaryReminderDay,
                    ReminderTime = oldGame.SecondaryReminderTime,
                    CustomReminderTitle = oldGame.SecondaryNotificationTitle,
                    CustomReminderMessage = oldGame.SecondaryNotificationMessage,
                    LastReminderProcessedLocal = oldGame.LastSecondaryReminderProcessedLocal,
                    ShowIncompleteCoverIndicator = oldGame.ShowIncompleteCoverIndicator
                };
                trackedGame.Routines.Add(new RoutineSettings
                {
                    Id = Guid.NewGuid(),
                    Name = "Weeklies",
                    Order = 0,
                    CurrentState = oldGame.CurrentState == 1 ? TaskState.COMPLETE : TaskState.INCOMPLETE,
                    ResetCadence = ResetCadence.Weekly,
                    ResetDay = oldGame.WeeklyResetDay,
                    ResetTime = oldGame.WeeklyResetTime,
                    LastResetProcessedLocal = oldGame.LastResetProcessedLocal,
                    Checklist = oldGame.Checklist ?? new ObservableCollection<ChecklistItemSettings>(),
                    AutomaticallyCompleteFromChecklist = oldGame.AutomaticallyCompleteFromChecklist,
                    CountTowardOverallTaskStatus = true,
                    CompletedAutomaticallyByChecklist = oldGame.CompletedAutomaticallyByChecklist
                });
                migrated.TrackedGames.Add(trackedGame);
            }

            return migrated;
        }

        internal static GameRoutinesSettings CreateEmpty()
        {
            return new GameRoutinesSettings
            {
                SchemaVersion = GameRoutinesSettings.CurrentSchemaVersion
            };
        }

        internal static string GetMigratedRoutineName(ResetCadence cadence)
        {
            switch (cadence)
            {
                case ResetCadence.Daily:
                    return "Dailies";
                case ResetCadence.Weekly:
                    return "Weeklies";
                default:
                    return "Tasks";
            }
        }
    }

    internal sealed class LegacySettingsV1
    {
        public int SchemaVersion { get; set; }
        public bool UseTasksAvailableTag { get; set; }
        public bool ShowBlockedManualStateWarning { get; set; } = true;
        public bool ShowIncompleteCoverIndicator { get; set; } = true;
        public ObservableCollection<LegacyTrackedGameSettingsV1> TrackedGames { get; set; } =
            new ObservableCollection<LegacyTrackedGameSettingsV1>();
    }

    internal sealed class LegacyTrackedGameSettingsV1
    {
        public Guid GameId { get; set; }
        public string CachedGameName { get; set; }
        public bool Enabled { get; set; } = true;
        public ResetCadence ResetCadence { get; set; } = ResetCadence.Never;
        public DayOfWeek ResetDay { get; set; } = DayOfWeek.Monday;
        public string ResetTime { get; set; } = "00:00";
        public TaskState CurrentState { get; set; } = TaskState.COMPLETE;
        public DateTime? LastResetProcessedLocal { get; set; }
        public bool CustomReminderEnabled { get; set; }
        public ReminderCadence ReminderCadence { get; set; } = ReminderCadence.Weekly;
        public DayOfWeek ReminderDay { get; set; } = DayOfWeek.Monday;
        public string ReminderTime { get; set; } = "00:00";
        public string CustomReminderTitle { get; set; }
        public string CustomReminderMessage { get; set; }
        public DateTime? LastReminderProcessedLocal { get; set; }
        public ObservableCollection<ChecklistItemSettings> Checklist { get; set; } =
            new ObservableCollection<ChecklistItemSettings>();
        public bool AutomaticallyCompleteFromChecklist { get; set; }
        public bool CompletedAutomaticallyByChecklist { get; set; }
        public bool ShowIncompleteCoverIndicator { get; set; } = true;
    }

    internal sealed class LegacySettingsV0
    {
        public bool UseReadyTag { get; set; }
        public bool ShowBlockedManualStateWarning { get; set; } = true;
        public bool ShowIncompleteCoverIndicator { get; set; } = true;
        public ObservableCollection<LegacyTrackedGameSettings> TrackedGames { get; set; } =
            new ObservableCollection<LegacyTrackedGameSettings>();
    }

    internal sealed class LegacyTrackedGameSettings
    {
        public Guid GameId { get; set; }
        public string CachedGameName { get; set; }
        public bool Enabled { get; set; } = true;
        public DayOfWeek WeeklyResetDay { get; set; } = DayOfWeek.Monday;
        public string WeeklyResetTime { get; set; } = "00:00";
        public int CurrentState { get; set; }
        public DateTime? LastResetProcessedLocal { get; set; }
        public bool SecondaryReminderEnabled { get; set; }
        public DayOfWeek SecondaryReminderDay { get; set; } = DayOfWeek.Monday;
        public string SecondaryReminderTime { get; set; } = "00:00";
        public string SecondaryNotificationTitle { get; set; }
        public string SecondaryNotificationMessage { get; set; }
        public DateTime? LastSecondaryReminderProcessedLocal { get; set; }
        public ObservableCollection<ChecklistItemSettings> Checklist { get; set; } =
            new ObservableCollection<ChecklistItemSettings>();
        public bool AutomaticallyCompleteFromChecklist { get; set; }
        public bool CompletedAutomaticallyByChecklist { get; set; }
        public bool ShowIncompleteCoverIndicator { get; set; } = true;
    }
}
