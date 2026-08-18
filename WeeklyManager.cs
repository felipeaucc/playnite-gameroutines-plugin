using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WeeklyManager
{
    public class WeeklyManager : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private const string ReadyTagName = "Tasks Available!";
        private const string LegacyWeekliesTagName = "Weeklies Available!";
        private const string LegacyReadyTagName = "WEEKLY READY";
        private const string CustomElementSourceName = "WeeklyManager";
        private const string AutomaticCompletionWarningTitle = "Automatic Completion Enabled";
        internal const string ChecklistElementName = "Checklist";
        internal const string StateToggleElementName = "StateToggle";
        internal const string IncompleteIndicatorElementName = "IncompleteIndicator";
        private static readonly TimeSpan SchedulerInterval = TimeSpan.FromMinutes(1);

        private readonly WeeklyManagerSettingsViewModel settings;
        private readonly HashSet<Guid> loggedMissingGameIds = new HashSet<Guid>();
        private readonly Dictionary<Guid, Window> openChecklistWindows = new Dictionary<Guid, Window>();
        private readonly Dictionary<Guid, Window> openManageChecklistWindows = new Dictionary<Guid, Window>();
        private DispatcherTimer schedulerTimer;
        private bool isProcessingSchedules;
        private bool isSettingsEditing;

        internal new IPlayniteAPI PlayniteApi { get; }

        internal event EventHandler<WeeklyManagerUiStateChangedEventArgs> UiStateChanged;

        public override Guid Id { get; } = Guid.Parse("cb076ecb-ea40-4036-8094-f1c554566b49");

        public WeeklyManager(IPlayniteAPI api) : base(api)
        {
            PlayniteApi = api;
            settings = new WeeklyManagerSettingsViewModel(this);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };

            AddCustomElementSupport(new AddCustomElementSupportArgs
            {
                SourceName = CustomElementSourceName,
                ElementList = new List<string>
                {
                    ChecklistElementName,
                    StateToggleElementName,
                    IncompleteIndicatorElementName
                }
            });

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

        public override Control GetGameViewControl(GetGameViewControlArgs args)
        {
            switch (args?.Name)
            {
                case ChecklistElementName:
                    return new WeeklyManagerChecklistControl(this);
                case StateToggleElementName:
                    return new WeeklyManagerStateToggleControl(this);
                case IncompleteIndicatorElementName:
                    return new WeeklyManagerIncompleteIndicatorControl(this);
                default:
                    return null;
            }
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            logger.Info($"Weekly Manager startup. Processing {settings.TrackedGames.Count} tracked game(s).");
            ReconcileAllChecklistStates();
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
            CloseChecklistWindows();
            StopScheduler();
            logger.Info("Weekly Manager stopped.");
        }

        public override void Dispose()
        {
            CloseChecklistWindows();
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
                    Description = "Open Checklist",
                    Action = actionArgs => OpenSelectedChecklist(actionArgs.Games)
                };

                yield return new GameMenuItem
                {
                    MenuSection = "Weekly Manager",
                    Description = "Mark Tasks Complete",
                    Action = actionArgs => SetSelectedGamesState(actionArgs.Games, WeeklyState.COMPLETE)
                };

                yield return new GameMenuItem
                {
                    MenuSection = "Weekly Manager",
                    Description = "Mark Tasks Incomplete",
                    Action = actionArgs => SetSelectedGamesState(actionArgs.Games, WeeklyState.READY)
                };

                yield return new GameMenuItem
                {
                    MenuSection = "Weekly Manager",
                    Description = "Reset Checklist",
                    Action = actionArgs => ResetSelectedGamesChecklists(actionArgs.Games)
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
            if (!editing)
            {
                // A reset can become due while Playnite's modal settings window is open.
                // Process it as soon as editing ends instead of waiting for the next timer tick.
                ProcessDueEvents();
            }
        }

        internal void NotifyUiStateChanged(Guid? gameId = null)
        {
            UiStateChanged?.Invoke(this, new WeeklyManagerUiStateChangedEventArgs(gameId));
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

        internal TrackedGameSettings GetTrackedGameSettings(Guid gameId)
        {
            return FindTrackedGame(gameId);
        }

        internal bool ShouldShowIncompleteCoverIndicator(Guid gameId)
        {
            var trackedGame = FindTrackedGame(gameId);
            return trackedGame != null &&
                   trackedGame.CurrentState == WeeklyState.READY &&
                   settings.Settings.ShowIncompleteCoverIndicator &&
                   trackedGame.ShowIncompleteCoverIndicator;
        }

        internal bool MarkTrackedGameComplete(Guid gameId)
        {
            return SetTrackedGamesState(new[] { gameId }, WeeklyState.COMPLETE);
        }

        internal bool MarkTrackedGameIncomplete(Guid gameId)
        {
            return SetTrackedGamesState(new[] { gameId }, WeeklyState.READY);
        }

        internal IReadOnlyList<ChecklistItemSettings> GetTrackedGameChecklist(Guid gameId)
        {
            var trackedGame = FindTrackedGame(gameId);
            return trackedGame?.Checklist?.ToList() ?? new List<ChecklistItemSettings>();
        }

        internal ChecklistProgress GetChecklistProgress(Guid gameId)
        {
            return ChecklistService.GetProgress(FindTrackedGame(gameId));
        }

        internal bool SetChecklistItemChecked(Guid gameId, Guid itemId, bool isChecked)
        {
            var trackedGame = FindTrackedGame(gameId);
            if (!ChecklistService.SetItemChecked(trackedGame, itemId, isChecked))
            {
                return false;
            }

            CompleteChecklistMutation(trackedGame, true, "checklist item state changed");
            return true;
        }

        internal bool ResetChecklist(Guid gameId, bool confirm)
        {
            return ResetChecklist(FindTrackedGame(gameId), confirm, true);
        }

        internal bool AddChecklistItem(
            TrackedGameSettings trackedGame,
            string text,
            bool persistChanges)
        {
            if (ChecklistService.AddItem(trackedGame, text) == null)
            {
                return false;
            }

            CompleteChecklistMutation(trackedGame, persistChanges, "checklist item added");
            return true;
        }

        internal bool AddChecklistItem(Guid gameId, string text)
        {
            return AddChecklistItem(FindTrackedGame(gameId), text, true);
        }

        internal bool EditChecklistItem(
            TrackedGameSettings trackedGame,
            Guid itemId,
            string text,
            bool persistChanges)
        {
            if (!ChecklistService.EditItem(trackedGame, itemId, text))
            {
                return false;
            }

            CompleteChecklistMutation(trackedGame, persistChanges, "checklist item edited");
            return true;
        }

        internal bool EditChecklistItem(Guid gameId, Guid itemId, string text)
        {
            return EditChecklistItem(FindTrackedGame(gameId), itemId, text, true);
        }

        internal bool DeleteChecklistItem(
            TrackedGameSettings trackedGame,
            Guid itemId,
            bool persistChanges)
        {
            if (!ChecklistService.DeleteItem(trackedGame, itemId))
            {
                return false;
            }

            CompleteChecklistMutation(trackedGame, persistChanges, "checklist item deleted");
            return true;
        }

        internal bool DeleteChecklistItem(Guid gameId, Guid itemId)
        {
            return DeleteChecklistItem(FindTrackedGame(gameId), itemId, true);
        }

        internal bool MoveChecklistItem(
            TrackedGameSettings trackedGame,
            Guid itemId,
            int offset,
            bool persistChanges)
        {
            if (!ChecklistService.MoveItem(trackedGame, itemId, offset))
            {
                return false;
            }

            if (persistChanges)
            {
                PersistAndReconcile(new[] { trackedGame });
            }

            return true;
        }

        internal bool MoveChecklistItem(Guid gameId, Guid itemId, int offset)
        {
            return MoveChecklistItem(FindTrackedGame(gameId), itemId, offset, true);
        }

        internal void ChecklistItemStateChanged(
            TrackedGameSettings trackedGame,
            bool persistChanges)
        {
            CompleteChecklistMutation(trackedGame, persistChanges, "checklist item state changed");
        }

        internal void ChecklistAutoCompletionChanged(
            TrackedGameSettings trackedGame,
            bool persistChanges)
        {
            CompleteChecklistMutation(trackedGame, persistChanges, "checklist auto-completion setting changed");
        }

        internal bool SetChecklistAutoCompletion(Guid gameId, bool enabled)
        {
            var trackedGame = FindTrackedGame(gameId);
            if (trackedGame == null ||
                trackedGame.AutomaticallyCompleteFromChecklist == enabled)
            {
                return false;
            }

            trackedGame.AutomaticallyCompleteFromChecklist = enabled;
            ChecklistAutoCompletionChanged(trackedGame, true);
            return true;
        }

        internal bool ResetChecklist(
            TrackedGameSettings trackedGame,
            bool confirm,
            bool persistChanges)
        {
            if (trackedGame == null)
            {
                return false;
            }

            if (confirm && ChecklistService.GetProgress(trackedGame).Completed > 0 &&
                PlayniteApi.Dialogs.ShowMessage(
                    $"Reset the checklist for {GetDisplayName(trackedGame, PlayniteApi.Database.Games.Get(trackedGame.GameId))}?",
                    "Weekly Manager",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return false;
            }

            ChecklistService.Reset(trackedGame);
            ApplyWeeklyState(trackedGame, WeeklyState.READY, false, "checklist reset");
            ReconcileChecklistDrivenState(trackedGame, "checklist reset reconciliation");
            if (persistChanges)
            {
                PersistAndReconcile(new[] { trackedGame });
            }

            return true;
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
                ChecklistService.Normalize(trackedGame);
                ReconcileChecklistDrivenState(trackedGame, "settings saved");

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
                foreach (var removedGameId in openChecklistWindows.Keys
                    .Where(a => !currentById.ContainsKey(a))
                    .ToList())
                {
                    openChecklistWindows[removedGameId].Close();
                }

                foreach (var removedGameId in openManageChecklistWindows.Keys
                    .Where(a => !currentById.ContainsKey(a))
                    .ToList())
                {
                    openManageChecklistWindows[removedGameId].Close();
                }

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
                NotifyUiStateChanged();
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

        private void OpenSelectedChecklist(IEnumerable<Game> selectedGames)
        {
            var game = (selectedGames ?? Enumerable.Empty<Game>()).FirstOrDefault(a => IsGameTracked(a.Id));
            if (game != null)
            {
                OpenChecklistWindow(game.Id);
            }
        }

        internal void OpenChecklistWindow(Guid gameId)
        {
            try
            {
                if (!IsGameTracked(gameId))
                {
                    return;
                }

                if (openChecklistWindows.TryGetValue(gameId, out var existingWindow))
                {
                    if (existingWindow.WindowState == WindowState.Minimized)
                    {
                        existingWindow.WindowState = WindowState.Normal;
                    }

                    existingWindow.Activate();
                    return;
                }

                var viewModel = new WeeklyChecklistViewModel(this, gameId);
                var window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                {
                    ShowCloseButton = true,
                    ShowMaximizeButton = true,
                    ShowMinimizeButton = true
                });
                window.Title = "Checklist";
                window.Width = 540;
                window.Height = 620;
                window.MinWidth = 420;
                window.MinHeight = 360;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                var owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                if (owner != null && !ReferenceEquals(owner, window))
                {
                    window.Owner = owner;
                }

                window.Content = new WeeklyChecklistView
                {
                    DataContext = viewModel
                };
                window.Closed += (sender, args) =>
                {
                    if (openChecklistWindows.TryGetValue(gameId, out var registeredWindow) &&
                        ReferenceEquals(registeredWindow, window))
                    {
                        openChecklistWindows.Remove(gameId);
                    }

                    viewModel.Dispose();
                };

                openChecklistWindows[gameId] = window;
                window.Show();
            }
            catch (Exception exception)
            {
                logger.Error(exception, $"Failed to open checklist window for game {gameId}.");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Weekly Manager could not open this checklist. See the Playnite log for details.",
                    "Weekly Manager");
            }
        }

        internal void OpenManageChecklistWindow(Guid gameId)
        {
            try
            {
                if (!IsGameTracked(gameId))
                {
                    return;
                }

                if (openManageChecklistWindows.TryGetValue(gameId, out var existingWindow))
                {
                    if (existingWindow.WindowState == WindowState.Minimized)
                    {
                        existingWindow.WindowState = WindowState.Normal;
                    }

                    existingWindow.Activate();
                    return;
                }

                var viewModel = new ManageChecklistViewModel(this, gameId);
                var window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                {
                    ShowCloseButton = true,
                    ShowMaximizeButton = true,
                    ShowMinimizeButton = true
                });
                window.Title = "Manage Checklist";
                window.Width = 620;
                window.Height = 560;
                window.MinWidth = 480;
                window.MinHeight = 340;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                var owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                if (owner != null && !ReferenceEquals(owner, window))
                {
                    window.Owner = owner;
                }

                window.Content = new ManageChecklistView
                {
                    DataContext = viewModel
                };
                window.Closed += (sender, args) =>
                {
                    if (openManageChecklistWindows.TryGetValue(gameId, out var registeredWindow) &&
                        ReferenceEquals(registeredWindow, window))
                    {
                        openManageChecklistWindows.Remove(gameId);
                    }

                    viewModel.Dispose();
                };

                openManageChecklistWindows[gameId] = window;
                window.Show();
            }
            catch (Exception exception)
            {
                logger.Error(exception, $"Failed to open checklist management window for game {gameId}.");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Weekly Manager could not open checklist management. See the Playnite log for details.",
                    "Weekly Manager");
            }
        }

        private void CloseChecklistWindows()
        {
            foreach (var window in openChecklistWindows.Values.ToList())
            {
                window.Close();
            }

            openChecklistWindows.Clear();

            foreach (var window in openManageChecklistWindows.Values.ToList())
            {
                window.Close();
            }

            openManageChecklistWindows.Clear();
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
            ChecklistService.Reset(trackedGame);
            ApplyWeeklyState(trackedGame, WeeklyState.READY, false, "weekly reset");
            ReconcileChecklistDrivenState(trackedGame, "weekly reset checklist reconciliation");
            trackedGame.LastResetProcessedLocal = occurrence;

            // Persist the occurrence before publishing its notification. If Playnite exits
            // immediately afterward, this reset still cannot be notified a second time.
            PersistAndReconcile(new[] { trackedGame });

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
                $"Processed custom reminder for {GetDisplayName(trackedGame, game)} " +
                $"({game.Id}) at {occurrence:O}; weekly state was not changed.");
        }

        private void SetSelectedGamesState(IEnumerable<Game> selectedGames, WeeklyState newState)
        {
            SetTrackedGamesState(
                (selectedGames ?? Enumerable.Empty<Game>()).Select(a => a.Id),
                newState);
        }

        private void ResetSelectedGamesChecklists(IEnumerable<Game> selectedGames)
        {
            try
            {
                var trackedGames = (selectedGames ?? Enumerable.Empty<Game>())
                    .Select(a => FindTrackedGame(a.Id))
                    .Where(a => a != null)
                    .Distinct()
                    .ToList();
                if (trackedGames.Count == 0)
                {
                    return;
                }

                if (trackedGames.Any(a => ChecklistService.GetProgress(a).Completed > 0) &&
                    PlayniteApi.Dialogs.ShowMessage(
                        trackedGames.Count == 1
                            ? $"Reset the checklist for {GetDisplayName(trackedGames[0], PlayniteApi.Database.Games.Get(trackedGames[0].GameId))}?"
                            : $"Reset the checklists for {trackedGames.Count} tracked games?",
                        "Weekly Manager",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    return;
                }

                foreach (var trackedGame in trackedGames)
                {
                    ChecklistService.Reset(trackedGame);
                    ApplyWeeklyState(trackedGame, WeeklyState.READY, false, "checklist reset");
                    ReconcileChecklistDrivenState(trackedGame, "checklist reset reconciliation");
                }

                PersistAndReconcile(trackedGames);
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Failed to reset selected game checklist(s).");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Weekly Manager could not reset the selected checklist(s). See the Playnite log for details.",
                    "Weekly Manager");
            }
        }

        private bool SetTrackedGamesState(IEnumerable<Guid> gameIds, WeeklyState newState)
        {
            try
            {
                var trackedById = settings.TrackedGames.ToDictionary(a => a.GameId);
                var changedGames = new List<TrackedGameSettings>();
                var wasBlocked = false;
                foreach (var gameId in (gameIds ?? Enumerable.Empty<Guid>()).Distinct())
                {
                    if (!trackedById.TryGetValue(gameId, out var trackedGame))
                    {
                        continue;
                    }

                    if (trackedGame.AutomaticallyCompleteFromChecklist)
                    {
                        wasBlocked = true;
                        continue;
                    }

                    ApplyWeeklyState(trackedGame, newState, false, "manual state change");
                    changedGames.Add(trackedGame);
                }

                if (wasBlocked)
                {
                    ShowAutomaticCompletionWarning();
                }

                if (changedGames.Count > 0)
                {
                    PersistAndReconcile(changedGames);
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

        private void ShowAutomaticCompletionWarning()
        {
            if (!settings.Settings.ShowBlockedManualStateWarning)
            {
                return;
            }

            try
            {
                var warningView = new BlockedManualStateWarningView();
                var window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                {
                    ShowCloseButton = true,
                    ShowMaximizeButton = false,
                    ShowMinimizeButton = false
                });
                window.Title = AutomaticCompletionWarningTitle;
                window.Width = 500;
                window.SizeToContent = SizeToContent.Height;
                window.ResizeMode = ResizeMode.NoResize;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                var owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                if (owner != null && !ReferenceEquals(owner, window))
                {
                    window.Owner = owner;
                }

                window.Content = warningView;
                window.ShowDialog();

                if (warningView.DontShowAgain)
                {
                    settings.Settings.ShowBlockedManualStateWarning = false;
                    SavePluginSettings(settings.Settings);
                }
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Failed to show the blocked manual status warning.");
            }
        }

        private TrackedGameSettings FindTrackedGame(Guid gameId)
        {
            return settings.TrackedGames.FirstOrDefault(a => a.GameId == gameId);
        }

        private void CompleteChecklistMutation(
            TrackedGameSettings trackedGame,
            bool persistChanges,
            string reason)
        {
            if (trackedGame == null)
            {
                return;
            }

            ReconcileChecklistDrivenState(trackedGame, reason);
            if (persistChanges)
            {
                PersistAndReconcile(new[] { trackedGame });
            }
        }

        private void ReconcileChecklistDrivenState(TrackedGameSettings trackedGame, string reason)
        {
            if (!trackedGame.AutomaticallyCompleteFromChecklist)
            {
                trackedGame.CompletedAutomaticallyByChecklist = false;
                return;
            }

            var progress = ChecklistService.GetProgress(trackedGame);
            if (progress.IsComplete)
            {
                ApplyWeeklyState(trackedGame, WeeklyState.COMPLETE, true, reason);
            }
            else
            {
                ApplyWeeklyState(trackedGame, WeeklyState.READY, false, reason);
            }
        }

        private void ReconcileAllChecklistStates()
        {
            var changed = false;
            foreach (var trackedGame in settings.TrackedGames)
            {
                var oldState = trackedGame.CurrentState;
                var oldOwnership = trackedGame.CompletedAutomaticallyByChecklist;
                ReconcileChecklistDrivenState(trackedGame, "startup checklist reconciliation");
                changed |= oldState != trackedGame.CurrentState ||
                           oldOwnership != trackedGame.CompletedAutomaticallyByChecklist;
            }

            if (changed)
            {
                SavePluginSettings(settings.Settings);
            }
        }

        private void ApplyWeeklyState(
            TrackedGameSettings trackedGame,
            WeeklyState newState,
            bool completedAutomatically,
            string reason)
        {
            var stateChanged = trackedGame.CurrentState != newState;
            var ownershipChanged =
                trackedGame.CompletedAutomaticallyByChecklist != completedAutomatically;
            trackedGame.CurrentState = newState;
            trackedGame.CompletedAutomaticallyByChecklist = completedAutomatically;

            if (stateChanged || ownershipChanged)
            {
                var game = PlayniteApi.Database.Games.Get(trackedGame.GameId);
                logger.Info(
                    $"Weekly state for {GetDisplayName(trackedGame, game)} ({trackedGame.GameId}) " +
                    $"is {GetUserFacingStateName(newState)} after {reason}.");
            }
        }

        private void PersistAndReconcile(IEnumerable<TrackedGameSettings> trackedGames)
        {
            var affectedGames = (trackedGames ?? Enumerable.Empty<TrackedGameSettings>())
                .Where(a => a != null)
                .Distinct()
                .ToList();
            if (affectedGames.Count == 0)
            {
                return;
            }

            // Persist state and checklist changes before synchronizing Playnite metadata so
            // startup reconciliation can never observe an older value after an interruption.
            SavePluginSettings(settings.Settings);
            foreach (var trackedGame in affectedGames)
            {
                ReconcileReadyTag(trackedGame);
            }

            foreach (var trackedGame in affectedGames)
            {
                NotifyUiStateChanged(trackedGame.GameId);
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
            var legacyWeekliesTag = FindTag(LegacyWeekliesTagName);
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

            if (legacyWeekliesTag != null)
            {
                changed |= updatedTagIds.RemoveAll(a => a == legacyWeekliesTag.Id) > 0;
            }

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
