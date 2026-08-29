using System;
using System.Collections.ObjectModel;

namespace GameRoutines.Tests
{
    internal static class TestSettingsFactory
    {
        internal static readonly Guid GameId = new Guid("11111111-1111-1111-1111-111111111111");
        internal static readonly Guid DailyRoutineId = new Guid("22222222-2222-2222-2222-222222222222");
        internal static readonly Guid WeeklyRoutineId = new Guid("33333333-3333-3333-3333-333333333333");
        internal static readonly Guid ChecklistItemOneId = new Guid("44444444-4444-4444-4444-444444444444");
        internal static readonly Guid ChecklistItemTwoId = new Guid("55555555-5555-5555-5555-555555555555");
        internal static readonly Guid OtherRoutineId = new Guid("66666666-6666-6666-6666-666666666666");
        internal static readonly Guid MissingId = new Guid("77777777-7777-7777-7777-777777777777");
        internal static readonly Guid BlankItemId = new Guid("88888888-8888-8888-8888-888888888888");

        internal static readonly DateTime ResetTimestamp =
            DateTime.SpecifyKind(new DateTime(2025, 12, 31, 6, 30, 0), DateTimeKind.Local);

        internal static readonly DateTime ReminderTimestamp =
            DateTime.SpecifyKind(new DateTime(2026, 1, 2, 18, 45, 0), DateTimeKind.Local);

        internal static ChecklistItemSettings Item(
            Guid id,
            string text,
            bool isChecked,
            int order)
        {
            return new ChecklistItemSettings
            {
                Id = id,
                Text = text,
                IsChecked = isChecked,
                Order = order
            };
        }

        internal static RoutineSettings Routine(
            Guid id,
            string name,
            int order,
            TaskState state = TaskState.COMPLETE,
            bool participating = true)
        {
            return new RoutineSettings
            {
                Id = id,
                Name = name,
                Order = order,
                CurrentState = state,
                CountTowardOverallTaskStatus = participating,
                Checklist = new ObservableCollection<ChecklistItemSettings>()
            };
        }

        internal static TrackedGameSettings Game(params RoutineSettings[] routines)
        {
            return new TrackedGameSettings
            {
                GameId = GameId,
                CachedGameName = "Fixed Game",
                Routines = new ObservableCollection<RoutineSettings>(routines ?? new RoutineSettings[0])
            };
        }

        internal static GameRoutinesSettings CurrentSettings()
        {
            var routine = Routine(DailyRoutineId, "Dailies", 0, TaskState.INCOMPLETE);
            routine.ResetCadence = ResetCadence.BiWeekly;
            routine.ResetDay = DayOfWeek.Wednesday;
            routine.ResetTime = "06:30";
            routine.LastResetProcessedLocal = ResetTimestamp;
            routine.BiWeeklyResetAnchorLocal =
                DateTime.SpecifyKind(new DateTime(2025, 12, 17, 6, 30, 0), DateTimeKind.Local);
            routine.AutomaticallyCompleteFromChecklist = true;
            routine.CompletedAutomaticallyByChecklist = false;
            routine.Checklist.Add(Item(ChecklistItemOneId, "First", true, 0));
            routine.Checklist.Add(Item(ChecklistItemTwoId, "Second", false, 1));

            var game = Game(routine);
            game.CustomReminderEnabled = true;
            game.ReminderCadence = ReminderCadence.BiWeekly;
            game.ReminderDay = DayOfWeek.Friday;
            game.ReminderTime = "18:45";
            game.CustomReminderTitle = "Fixed title";
            game.CustomReminderMessage = "Fixed message";
            game.LastReminderProcessedLocal = ReminderTimestamp;
            game.BiWeeklyReminderAnchorLocal =
                DateTime.SpecifyKind(new DateTime(2025, 12, 19, 18, 45, 0), DateTimeKind.Local);

            return new GameRoutinesSettings
            {
                SchemaVersion = GameRoutinesSettings.CurrentSchemaVersion,
                UseTasksAvailableTag = true,
                ShowBlockedManualStateWarning = false,
                ShowIncompleteCoverIndicator = true,
                TrackedGames = new ObservableCollection<TrackedGameSettings> { game }
            };
        }
    }
}
