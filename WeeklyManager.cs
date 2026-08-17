using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WeeklyManager
{
    public class WeeklyManager : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private const string ReadyTagName = "Weeklies Available!";
        private const string LegacyReadyTagName = "WEEKLY READY";
        private static readonly TimeSpan SchedulerInterval = TimeSpan.FromMinutes(1);

        private readonly WeeklyManagerSettingsViewModel settings;
        private readonly HashSet<Guid> loggedMissingGameIds = new HashSet<Guid>();
        private DispatcherTimer schedulerTimer;
        private bool isProcessingSchedules;
        private bool isSettingsEditing;

        internal new IPlayniteAPI PlayniteApi { get; }

        public override Guid Id { get; } = Guid.Parse("cb076ecb-ea40-4036-8094-f1c554566b49");

        public WeeklyManager(IPlayniteAPI api) : base(api)
        {
            PlayniteApi = api;
            settings = new WeeklyManagerSettingsViewModel(this);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };

            logger.Info($"Weekly Manager initialized. Loaded {settings.TrackedGames.Count} tracked game(s).");
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new WeeklyManagerSettingsView();
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            logger.Info($"Weekly Manager startup. Processing {settings.TrackedGames.Count} tracked game(s).");
            ReconcileAllReadyTags();
            ProcessDueEvents();

            schedulerTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = SchedulerInterval
            };
            schedulerTimer.Tick += SchedulerTimer_Tick;
            schedulerTimer.Start();
        }

        public override void OnApplicationStopped(OnApplicationStoppedEventArgs args)
        {
            StopScheduler();
            logger.Info("Weekly Manager stopped.");
        }

        public override void Dispose()
        {
            StopScheduler();
            base.Dispose();
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            var selectedIds = new HashSet<Guid>((args.Games ?? Enumerable.Empty<Game>()).Select(a => a.Id));
            var hasTrackedGame = selectedIds.Any(IsGameTracked);

            if (hasTrackedGame)
            {
                yield return new GameMenuItem
                {
                    MenuSection = "Weekly Manager",
                    Description = "Mark Weekly Complete",
                    Action = actionArgs => SetSelectedGamesState(actionArgs.Games, WeeklyState.COMPLETE)
                };

                yield return new GameMenuItem
                {
                    MenuSection = "Weekly Manager",
                    Description = "Mark Weekly Incomplete",
                    Action = actionArgs => SetSelectedGamesState(actionArgs.Games, WeeklyState.READY)
                };
            }

            yield return new GameMenuItem
            {
                MenuSection = "Weekly Manager",
                Description = "Edit Weekly Settings",
                Action = _ => OpenSettingsView()
            };
        }

        internal void SetSettingsEditing(bool editing)
        {
            isSettingsEditing = editing;
        }

        internal bool IsGameTracked(Guid gameId)
        {
            return settings.TrackedGames.Any(a => a.GameId == gameId);
        }

        internal string GetTrackedGameState(Guid gameId)
        {
            var trackedGame = settings.TrackedGames.FirstOrDefault(a => a.GameId == gameId);
            return trackedGame == null ? null : GetUserFacingStateName(trackedGame.CurrentState);
        }

        internal bool MarkTrackedGameComplete(Guid gameId)
        {
            return SetTrackedGamesState(new[] { gameId }, WeeklyState.COMPLETE);
        }

        internal bool MarkTrackedGameIncomplete(Guid gameId)
        {
            return SetTrackedGamesState(new[] { gameId }, WeeklyState.READY);
        }

        internal void ReconcileTrackedGameTag(Guid gameId)
        {
            var trackedGame = settings.TrackedGames.FirstOrDefault(a => a.GameId == gameId);
            if (trackedGame != null)
            {
                ReconcileReadyTag(trackedGame);
            }
        }

        internal void PrepareSettingsForSave(WeeklyManagerSettings previous, WeeklyManagerSettings current)
        {
            var now = DateTime.Now;
            var previousById = (previous?.TrackedGames ?? new System.Collections.ObjectModel.ObservableCollection<TrackedGameSettings>())
                .GroupBy(a => a.GameId)
                .ToDictionary(a => a.Key, a => a.First());

            foreach (var trackedGame in current.TrackedGames)
            {
                if (WeeklyScheduleCalculator.TryParseLocalTime(trackedGame.WeeklyResetTime, out var resetTime) &&
                    (!previousById.TryGetValue(trackedGame.GameId, out var oldGame) ||
                     oldGame.WeeklyResetDay != trackedGame.WeeklyResetDay ||
                     !TimesEqual(oldGame.WeeklyResetTime, trackedGame.WeeklyResetTime)))
                {
                    trackedGame.LastResetProcessedLocal = WeeklyScheduleCalculator.GetMostRecentOccurrence(
                        now, trackedGame.WeeklyResetDay, resetTime);
                }

                if (WeeklyScheduleCalculator.TryParseLocalTime(
                        trackedGame.SecondaryReminderTime, out var reminderTime) &&
                    (!previousById.TryGetValue(trackedGame.GameId, out oldGame) ||
                     !oldGame.SecondaryReminderEnabled ||
                     oldGame.SecondaryReminderDay != trackedGame.SecondaryReminderDay ||
                     !TimesEqual(oldGame.SecondaryReminderTime, trackedGame.SecondaryReminderTime)))
                {
                    trackedGame.LastSecondaryReminderProcessedLocal = WeeklyScheduleCalculator.GetMostRecentOccurrence(
                        now, trackedGame.SecondaryReminderDay, reminderTime);
                }
            }
        }

        internal void ApplySettingsChanges(WeeklyManagerSettings previous, WeeklyManagerSettings current)
        {
            try
            {
                var currentById = current.TrackedGames.ToDictionary(a => a.GameId);
                if (previous?.TrackedGames != null)
                {
                    foreach (var oldGame in previous.TrackedGames)
                    {
                        if (!currentById.TryGetValue(oldGame.GameId, out var newGame) || !newGame.Enabled)
                        {
                            UpdateReadyTag(oldGame.GameId, false);
                        }
                    }
                }

                foreach (var trackedGame in current.TrackedGames)
                {
                    ReconcileReadyTag(trackedGame);
                }

                ProcessDueEvents();
            }
            catch (Exception exception)
            {
                LogException(exception, "Failed to apply Weekly Manager settings changes.");
            }
        }

        internal void LogException(Exception exception, string message)
        {
            logger.Error(exception, message);
        }

        private static bool TimesEqual(string first, string second)
        {
            return WeeklyScheduleCalculator.TryParseLocalTime(first, out var firstTime) &&
                   WeeklyScheduleCalculator.TryParseLocalTime(second, out var secondTime) &&
                   firstTime == secondTime;
        }

        private void SchedulerTimer_Tick(object sender, EventArgs args)
        {
            ProcessDueEvents();
        }

        private void StopScheduler()
        {
            if (schedulerTimer == null)
            {
                return;
            }

            schedulerTimer.Stop();
            schedulerTimer.Tick -= SchedulerTimer_Tick;
            schedulerTimer = null;
        }

        private void ProcessDueEvents()
        {
            if (isProcessingSchedules || isSettingsEditing || !PlayniteApi.Database.IsOpen)
            {
                return;
            }

            isProcessingSchedules = true;
            try
            {
                var now = DateTime.Now;
                foreach (var trackedGame in settings.TrackedGames.ToList())
                {
                    try
                    {
                        ProcessTrackedGame(now, trackedGame);
                    }
                    catch (Exception exception)
                    {
                        logger.Error(
                            exception,
                            $"Failed to process schedules for tracked game {trackedGame.GameId}.");
                    }
                }
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Unhandled exception while processing Weekly Manager schedules.");
            }
            finally
            {
                isProcessingSchedules = false;
            }
        }

        private void ProcessTrackedGame(DateTime now, TrackedGameSettings trackedGame)
        {
            if (!trackedGame.Enabled)
            {
                return;
            }

            var game = GetReferencedGame(trackedGame);
            if (game == null)
            {
                return;
            }

            var settingsChanged = false;
            if (!string.Equals(trackedGame.CachedGameName, game.Name, StringComparison.Ordinal))
            {
                trackedGame.CachedGameName = game.Name;
                settingsChanged = true;
            }

            if (WeeklyScheduleCalculator.TryParseLocalTime(trackedGame.WeeklyResetTime, out var resetTime))
            {
                var resetOccurrence = WeeklyScheduleCalculator.GetMostRecentOccurrence(
                    now, trackedGame.WeeklyResetDay, resetTime);
                if (WeeklyScheduleCalculator.IsOccurrenceDue(
                    trackedGame.LastResetProcessedLocal, resetOccurrence))
                {
                    ProcessReset(trackedGame, game, resetOccurrence);
                    settingsChanged = false;
                }
            }

            if (trackedGame.SecondaryReminderEnabled &&
                WeeklyScheduleCalculator.TryParseLocalTime(trackedGame.SecondaryReminderTime, out var reminderTime))
            {
                var reminderOccurrence = WeeklyScheduleCalculator.GetMostRecentOccurrence(
                    now, trackedGame.SecondaryReminderDay, reminderTime);
                if (WeeklyScheduleCalculator.IsOccurrenceDue(
                    trackedGame.LastSecondaryReminderProcessedLocal, reminderOccurrence))
                {
                    ProcessSecondaryReminder(trackedGame, game, reminderOccurrence);
                    settingsChanged = false;
                }
            }

            if (settingsChanged)
            {
                SavePluginSettings(settings.Settings);
            }
        }

        private Game GetReferencedGame(TrackedGameSettings trackedGame)
        {
            var game = PlayniteApi.Database.Games.Get(trackedGame.GameId);
            if (game == null)
            {
                if (loggedMissingGameIds.Add(trackedGame.GameId))
                {
                    logger.Warn(
                        $"Tracked game is missing from the Playnite database: " +
                        $"{trackedGame.CachedGameName ?? "Unknown"} ({trackedGame.GameId}).");
                }

                return null;
            }

            loggedMissingGameIds.Remove(trackedGame.GameId);
            return game;
        }

        private void ProcessReset(TrackedGameSettings trackedGame, Game game, DateTime occurrence)
        {
            trackedGame.CurrentState = WeeklyState.READY;
            trackedGame.LastResetProcessedLocal = occurrence;
            ReconcileReadyTag(trackedGame);

            // Persist the occurrence before publishing its notification. If Playnite exits
            // immediately afterward, this reset still cannot be notified a second time.
            SavePluginSettings(settings.Settings);

            var name = GetDisplayName(trackedGame, game);
            var notificationId = $"WeeklyManager_Reset_{game.Id:N}_{occurrence.Ticks}";
            PlayniteApi.Notifications.Add(new NotificationMessage(
                notificationId,
                $"{name.ToUpperInvariant()}: WEEKLY RESET\r\nWeekly activities are available.",
                NotificationType.Info));

            logger.Info($"Processed weekly reset for {name} ({game.Id}) at {occurrence:O}; state is INCOMPLETE.");
        }

        private void ProcessSecondaryReminder(
            TrackedGameSettings trackedGame,
            Game game,
            DateTime occurrence)
        {
            trackedGame.LastSecondaryReminderProcessedLocal = occurrence;
            SavePluginSettings(settings.Settings);

            var notificationId = $"WeeklyManager_Reminder_{game.Id:N}_{occurrence.Ticks}";
            var name = GetDisplayName(trackedGame, game).ToUpperInvariant();
            PlayniteApi.Notifications.Add(new NotificationMessage(
                notificationId,
                $"{name}: {trackedGame.SecondaryNotificationTitle}\r\n" +
                trackedGame.SecondaryNotificationMessage,
                NotificationType.Info));

            logger.Info(
                $"Processed secondary reminder for {GetDisplayName(trackedGame, game)} " +
                $"({game.Id}) at {occurrence:O}; weekly state was not changed.");
        }

        private void SetSelectedGamesState(IEnumerable<Game> selectedGames, WeeklyState newState)
        {
            SetTrackedGamesState(
                (selectedGames ?? Enumerable.Empty<Game>()).Select(a => a.Id),
                newState);
        }

        private bool SetTrackedGamesState(IEnumerable<Guid> gameIds, WeeklyState newState)
        {
            try
            {
                var trackedById = settings.TrackedGames.ToDictionary(a => a.GameId);
                var changedGames = new List<TrackedGameSettings>();
                foreach (var gameId in (gameIds ?? Enumerable.Empty<Guid>()).Distinct())
                {
                    if (!trackedById.TryGetValue(gameId, out var trackedGame))
                    {
                        continue;
                    }

                    trackedGame.CurrentState = newState;
                    changedGames.Add(trackedGame);
                    var game = PlayniteApi.Database.Games.Get(gameId);
                    logger.Info(
                        $"Weekly state for {GetDisplayName(trackedGame, game)} ({gameId}) " +
                        $"was manually changed to {GetUserFacingStateName(newState)}.");
                }

                if (changedGames.Count > 0)
                {
                    // Persist the state before synchronizing Playnite metadata so startup
                    // reconciliation can never observe the previous INCOMPLETE/COMPLETE value.
                    SavePluginSettings(settings.Settings);
                    foreach (var trackedGame in changedGames)
                    {
                        ReconcileTrackedGameTag(trackedGame.GameId);
                    }
                }

                return changedGames.Count > 0;
            }
            catch (Exception exception)
            {
                logger.Error(
                    exception,
                    $"Failed to mark selected game(s) {GetUserFacingStateName(newState)}.");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Weekly Manager could not update the selected game(s). See the Playnite log for details.",
                    "Weekly Manager");
                return false;
            }
        }

        private void ReconcileReadyTag(TrackedGameSettings trackedGame)
        {
            UpdateReadyTag(
                trackedGame.GameId,
                settings.Settings.UseReadyTag &&
                trackedGame.Enabled &&
                trackedGame.CurrentState == WeeklyState.READY);
        }

        private void ReconcileAllReadyTags()
        {
            foreach (var trackedGame in settings.TrackedGames)
            {
                try
                {
                    ReconcileReadyTag(trackedGame);
                }
                catch (Exception exception)
                {
                    logger.Error(
                        exception,
                        $"Failed to reconcile Weekly Manager tags for game {trackedGame.GameId}.");
                }
            }
        }

        private void UpdateReadyTag(Guid gameId, bool shouldHaveTag)
        {
            // Context-menu game objects can be older than the database object after an
            // earlier metadata update. Always re-fetch by authoritative Game.Id before
            // inspecting TagIds or a stale object can incorrectly skip tag removal.
            var game = PlayniteApi.Database.Games.Get(gameId);
            if (game == null)
            {
                return;
            }
            var readyTag = FindTag(ReadyTagName);
            var legacyReadyTag = FindTag(LegacyReadyTagName);

            if (readyTag == null && shouldHaveTag)
            {
                readyTag = PlayniteApi.Database.Tags.Add(
                    ReadyTagName,
                    (existing, requestedName) => string.Equals(
                        existing.Name, requestedName, StringComparison.Ordinal));
            }

            var tagIds = game.TagIds ?? new List<Guid>();
            var updatedTagIds = new List<Guid>(tagIds);
            var changed = false;

            if (legacyReadyTag != null)
            {
                changed |= updatedTagIds.RemoveAll(a => a == legacyReadyTag.Id) > 0;
            }

            if (readyTag != null && shouldHaveTag && !updatedTagIds.Contains(readyTag.Id))
            {
                updatedTagIds.Add(readyTag.Id);
                changed = true;
            }
            else if (readyTag != null && !shouldHaveTag)
            {
                changed |= updatedTagIds.RemoveAll(a => a == readyTag.Id) > 0;
            }

            if (!changed)
            {
                return;
            }

            var updatedGame = game.GetCopy();
            updatedGame.TagIds = updatedTagIds;
            PlayniteApi.Database.Games.Update(updatedGame);
        }

        private Tag FindTag(string tagName)
        {
            return PlayniteApi.Database.Tags.FirstOrDefault(a =>
                string.Equals(a.Name, tagName, StringComparison.Ordinal));
        }

        private static string GetDisplayName(TrackedGameSettings trackedGame, Game game)
        {
            if (!string.IsNullOrWhiteSpace(game?.Name))
            {
                return game.Name;
            }

            return string.IsNullOrWhiteSpace(trackedGame.CachedGameName)
                ? "Game"
                : trackedGame.CachedGameName;
        }

        private static string GetUserFacingStateName(WeeklyState state)
        {
            return state == WeeklyState.COMPLETE ? "COMPLETE" : "INCOMPLETE";
        }
    }
}
