using System;
using System.Collections.Generic;

namespace GameRoutines
{
    internal static class CustomReminderService
    {
        public static TrackedGameSettings CreateEditableCopy(TrackedGameSettings source)
        {
            if (source == null)
            {
                return null;
            }

            return new TrackedGameSettings
            {
                GameId = source.GameId,
                CachedGameName = source.CachedGameName,
                CustomReminderEnabled = source.CustomReminderEnabled,
                ReminderCadence = source.ReminderCadence,
                ReminderDay = source.ReminderDay,
                ReminderTime = source.ReminderTime,
                CustomReminderTitle = source.CustomReminderTitle,
                CustomReminderMessage = source.CustomReminderMessage,
                LastReminderProcessedLocal = source.LastReminderProcessedLocal,
                BiWeeklyReminderAnchorLocal = source.BiWeeklyReminderAnchorLocal
            };
        }

        public static void Apply(TrackedGameSettings source, TrackedGameSettings target)
        {
            if (source == null || target == null)
            {
                return;
            }

            target.CustomReminderEnabled = source.CustomReminderEnabled;
            target.ReminderCadence = source.ReminderCadence;
            target.ReminderDay = source.ReminderDay;
            target.ReminderTime = source.ReminderTime;
            target.CustomReminderTitle = source.CustomReminderTitle;
            target.CustomReminderMessage = source.CustomReminderMessage;
            target.LastReminderProcessedLocal = source.LastReminderProcessedLocal;
            target.BiWeeklyReminderAnchorLocal = source.BiWeeklyReminderAnchorLocal;
        }

        public static void Validate(
            TrackedGameSettings trackedGame,
            string gameName,
            ICollection<string> errors)
        {
            if (trackedGame == null || errors == null)
            {
                return;
            }

            if (!Enum.IsDefined(typeof(ReminderCadence), trackedGame.ReminderCadence))
            {
                errors.Add($"{gameName}: reminder frequency is invalid.");
            }

            if (trackedGame.CustomReminderTitle != null &&
                trackedGame.CustomReminderTitle.Length > TrackedGameSettings.MaximumCustomReminderTitleLength)
            {
                errors.Add($"{gameName}: custom reminder title cannot exceed 40 characters.");
            }

            if (trackedGame.CustomReminderMessage != null &&
                trackedGame.CustomReminderMessage.Length > TrackedGameSettings.MaximumCustomReminderMessageLength)
            {
                errors.Add($"{gameName}: custom reminder message cannot exceed 160 characters.");
            }

            if (!trackedGame.CustomReminderEnabled)
            {
                return;
            }

            if (!ScheduleCalculator.TryNormalizeTimeInput(trackedGame.ReminderTime, out var normalizedReminderTime) ||
                !string.Equals(trackedGame.ReminderTime, normalizedReminderTime, StringComparison.Ordinal))
            {
                errors.Add($"{gameName}: custom reminder time must use 24-hour HH:mm format.");
            }

            if (trackedGame.ReminderCadence == ReminderCadence.BiWeekly &&
                !trackedGame.BiWeeklyReminderAnchorLocal.HasValue)
            {
                errors.Add($"{gameName}: a Biweekly reminder start date is required.");
            }

            if (string.IsNullOrWhiteSpace(trackedGame.CustomReminderTitle))
            {
                errors.Add($"{gameName}: custom reminder title cannot be empty when the custom reminder is enabled.");
            }

            if (string.IsNullOrWhiteSpace(trackedGame.CustomReminderMessage))
            {
                errors.Add($"{gameName}: custom reminder message cannot be empty when the custom reminder is enabled.");
            }
        }
    }
}
