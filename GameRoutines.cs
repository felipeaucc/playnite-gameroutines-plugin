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

namespace GameRoutines
{
    public class GameRoutines : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private const string TasksAvailableTagName = "Tasks Available!";
        private const string LegacyWeekliesTagName = "Weeklies Available!";
        private const string LegacyReadyTagName = "WEEKLY READY";
        private const string CustomElementSourceName = "GameRoutines";
        private const string AutomaticCompletionWarningTitle = "Automatic Completion Enabled";
        internal const string ChecklistElementName = "Checklist";
        internal const string StateToggleElementName = "StateToggle";
        internal const string IncompleteIndicatorElementName = "IncompleteIndicator";
        private static readonly TimeSpan SchedulerInterval = TimeSpan.FromMinutes(1);

        private readonly GameRoutinesSettingsViewModel settings;
        private readonly HashSet<Guid> loggedMissingGameIds = new HashSet<Guid>();
        private readonly Dictionary<Guid, Window> openChecklistWindows = new Dictionary<Guid, Window>();
        private readonly Dictionary<Guid, Window> openManageChecklistWindows = new Dictionary<Guid, Window>();
        private DispatcherTimer schedulerTimer;
        private bool isProcessingSchedules;
        private bool isSettingsEditing;

        internal new IPlayniteAPI PlayniteApi { get; }

        internal event EventHandler<GameRoutinesUiStateChangedEventArgs> UiStateChanged;

        public override Guid Id { get; } = Guid.Parse("cb076ecb-ea40-4036-8094-f1c554566b49");

        public GameRoutines(IPlayniteAPI api) : base(api)
        {
            PlayniteApi = api;
            settings = new GameRoutinesSettingsViewModel(this);
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

            logger.Info($"Game Routines initialized. Loaded {settings.TrackedGames.Count} tracked game(s).");
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new GameRoutinesSettingsView();
        }

        public override Control GetGameViewControl(GetGameViewControlArgs args)
        {
            switch (args?.Name)
            {
                case ChecklistElementName:
                    return new GameRoutinesChecklistControl(this);
                case StateToggleElementName:
                    return new GameRoutinesStateToggleControl(this);
                case IncompleteIndicatorElementName:
                    return new GameRoutinesIncompleteIndicatorControl(this);
                default:
                    return null;
            }
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            logger.Info($"Game Routines startup. Processing {settings.TrackedGames.Count} tracked game(s).");
            ReconcileAllChecklistStates();
            ReconcileAllTasksAvailableTags();
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
            logger.Info("Game Routines stopped.");
        }

        public override void Dispose()
        {
            CloseChecklistWindows();
            StopScheduler();
            base.Dispose();
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            var selectedGames = (args?.Games ?? Enumerable.Empty<Game>())
                .Where(a => a != null)
                .GroupBy(a => a.Id)
                .Select(a => a.First())
                .ToList();
            var selectedIds = new HashSet<Guid>(selectedGames.Select(a => a.Id));
            var hasTrackedGame = selectedIds.Any(IsGameTracked);
            var hasUntrackedGame = selectedIds.Any(a => !IsGameTracked(a));

            if (hasUntrackedGame)
            {
                yield return new GameMenuItem
                {
                    MenuSection = "Game Routines",
                    Description = selectedGames.Count == 1
                        ? "Start Tracking This Game"
                        : "Start Tracking Selected Games",
                    Action = actionArgs => TrackSelectedGames(actionArgs.Games)
                };
            }

            if (hasTrackedGame)
            {
                yield return new GameMenuItem
                {
                    MenuSection = "Game Routines",
                    Description = "Open Checklist",
                    Action = actionArgs => OpenSelectedChecklist(actionArgs.Games)
                };

                yield return new GameMenuItem
                {
                    MenuSection = "Game Routines",
                    Description = "Mark Tasks as Complete",
                    Action = actionArgs => SetSelectedGamesState(actionArgs.Games, TaskState.COMPLETE)
                };

                yield return new GameMenuItem
                {
                    MenuSection = "Game Routines",
                    Description = "Mark Tasks as Incomplete",
                    Action = actionArgs => SetSelectedGamesState(actionArgs.Games, TaskState.INCOMPLETE)
                };

                yield return new GameMenuItem
                {
                    MenuSection = "Game Routines",
                    Description = "Reset Checklist",
                    Action = actionArgs => ResetSelectedGamesChecklists(actionArgs.Games)
                };
            }

            yield return new GameMenuItem
            {
                MenuSection = "Game Routines",
                Description = "Edit Game Routines Settings",
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
            UiStateChanged?.Invoke(this, new GameRoutinesUiStateChangedEventArgs(gameId));
        }

        internal bool IsGameTracked(Guid gameId)
        {
            return settings.TrackedGames.Any(a => a.GameId == gameId);
        }

        internal IReadOnlyList<TrackedGameSettings> TrackGames(
            IEnumerable<Game> games,
            bool persistChanges)
        {
            var trackedIds = new HashSet<Guid>(settings.TrackedGames.Select(a => a.GameId));
            var addedGames = new List<TrackedGameSettings>();
            foreach (var selectedGame in (games ?? Enumerable.Empty<Game>())
                .Where(a => a != null && a.Id != Guid.Empty)
                .GroupBy(a => a.Id)
                .Select(a => a.First()))
            {
                if (!trackedIds.Add(selectedGame.Id))
                {
                    continue;
                }

                var game = PlayniteApi.Database.Games.Get(selectedGame.Id) ?? selectedGame;
                var trackedGame = new TrackedGameSettings
                {
                    GameId = game.Id,
                    CachedGameName = game.Name,
                    Enabled = true,
                    ResetCadence = ResetCadence.Never,
                    ResetDay = DayOfWeek.Monday,
                    ResetTime = "00:00",
                    CurrentState = TaskState.COMPLETE,
                    AutomaticallyCompleteFromChecklist = false,
                    ShowIncompleteCoverIndicator = true,
                    CustomReminderEnabled = false,
                    ReminderCadence = ReminderCadence.Weekly,
                    ReminderDay = DayOfWeek.Monday,
                    ReminderTime = "00:00"
                };
                settings.TrackedGames.Add(trackedGame);
                addedGames.Add(trackedGame);
            }

            if (persistChanges && addedGames.Count > 0)
            {
                PersistAndReconcile(addedGames);
            }

            return addedGames;
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
                   trackedGame.CurrentState == TaskState.INCOMPLETE &&
                   settings.Settings.ShowIncompleteCoverIndicator &&
                   trackedGame.ShowIncompleteCoverIndicator;
        }

        internal bool MarkTrackedGameComplete(Guid gameId)
        {
            return SetTrackedGamesState(new[] { gameId }, TaskState.COMPLETE);
        }

        internal bool MarkTrackedGameIncomplete(Guid gameId)
        {
            return SetTrackedGamesState(new[] { gameId }, TaskState.INCOMPLETE);
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
                    "Game Routines",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return false;
            }

            ChecklistService.Reset(trackedGame);
            ApplyTaskState(trackedGame, TaskState.INCOMPLETE, false, "checklist reset");
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
                ReconcileTasksAvailableTag(trackedGame);
            }
        }

        internal void PrepareSettingsForSave(GameRoutinesSettings previous, GameRoutinesSettings current)
        {
            var now = DateTime.Now;
            var previousById = (previous?.TrackedGames ?? new System.Collections.ObjectModel.ObservableCollection<TrackedGameSettings>())
                .GroupBy(a => a.GameId)
                .ToDictionary(a => a.Key, a => a.First());

            foreach (var trackedGame in current.TrackedGames)
            {
                ChecklistService.Normalize(trackedGame);
                ReconcileChecklistDrivenState(trackedGame, "settings saved");

                if (trackedGame.ResetCadence != ResetCadence.Never &&
                    ScheduleCalculator.TryParseLocalTime(trackedGame.ResetTime, out var resetTime) &&
                    (!previousById.TryGetValue(trackedGame.GameId, out var oldGame) ||
                     HasResetScheduleChanged(oldGame, trackedGame)) &&
                    ScheduleCalculator.TryGetMostRecentOccurrence(
                        now,
                        trackedGame.ResetCadence,
                        trackedGame.ResetDay,
                        resetTime,
                        out var resetOccurrence))
                {
                    trackedGame.LastResetProcessedLocal = resetOccurrence;
                }

                if (trackedGame.CustomReminderEnabled &&
                    ScheduleCalculator.TryParseLocalTime(
                        trackedGame.ReminderTime, out var reminderTime) &&
                    (!previousById.TryGetValue(trackedGame.GameId, out oldGame) ||
                     HasReminderScheduleChanged(oldGame, trackedGame)))
                {
                    trackedGame.LastReminderProcessedLocal = ScheduleCalculator.GetMostRecentOccurrence(
                        now, trackedGame.ReminderCadence, trackedGame.ReminderDay, reminderTime);
                }
            }
        }

        internal void ApplySettingsChanges(GameRoutinesSettings previous, GameRoutinesSettings current)
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
                            UpdateTasksAvailableTag(oldGame.GameId, false);
                        }
                    }
                }

                foreach (var trackedGame in current.TrackedGames)
                {
                    ReconcileTasksAvailableTag(trackedGame);
                }

                ProcessDueEvents();
                NotifyUiStateChanged();
            }
            catch (Exception exception)
            {
                LogException(exception, "Failed to apply Game Routines settings changes.");
            }
        }

        internal void LogException(Exception exception, string message)
        {
            logger.Error(exception, message);
        }

        private static bool TimesEqual(string first, string second)
        {
            return ScheduleCalculator.TryParseLocalTime(first, out var firstTime) &&
                   ScheduleCalculator.TryParseLocalTime(second, out var secondTime) &&
                   firstTime == secondTime;
        }

        private static bool HasResetScheduleChanged(
            TrackedGameSettings previous,
            TrackedGameSettings current)
        {
            return previous.ResetCadence != current.ResetCadence ||
                   (current.ResetCadence == ResetCadence.Weekly &&
                    previous.ResetDay != current.ResetDay) ||
                   !TimesEqual(previous.ResetTime, current.ResetTime);
        }

        private static bool HasReminderScheduleChanged(
            TrackedGameSettings previous,
            TrackedGameSettings current)
        {
            return !previous.CustomReminderEnabled ||
                   previous.ReminderCadence != current.ReminderCadence ||
                   (current.ReminderCadence == ReminderCadence.Weekly &&
                    previous.ReminderDay != current.ReminderDay) ||
                   !TimesEqual(previous.ReminderTime, current.ReminderTime);
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

                var viewModel = new GameChecklistViewModel(this, gameId);
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

                window.Content = new GameChecklistView
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
                    "Game Routines could not open this checklist. See the Playnite log for details.",
                    "Game Routines");
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
                    "Game Routines could not open checklist management. See the Playnite log for details.",
                    "Game Routines");
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
                logger.Error(exception, "Unhandled exception while processing Game Routines schedules.");
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

            if (ScheduleCalculator.TryParseLocalTime(trackedGame.ResetTime, out var resetTime) &&
                ScheduleCalculator.TryGetMostRecentOccurrence(
                    now,
                    trackedGame.ResetCadence,
                    trackedGame.ResetDay,
                    resetTime,
                    out var resetOccurrence))
            {
                if (ScheduleCalculator.IsOccurrenceDue(
                    trackedGame.LastResetProcessedLocal, resetOccurrence))
                {
                    ProcessReset(trackedGame, game, resetOccurrence);
                    settingsChanged = false;
                }
            }

            if (trackedGame.CustomReminderEnabled &&
                ScheduleCalculator.TryParseLocalTime(trackedGame.ReminderTime, out var reminderTime))
            {
                var reminderOccurrence = ScheduleCalculator.GetMostRecentOccurrence(
                    now, trackedGame.ReminderCadence, trackedGame.ReminderDay, reminderTime);
                if (ScheduleCalculator.IsOccurrenceDue(
                    trackedGame.LastReminderProcessedLocal, reminderOccurrence))
                {
                    ProcessCustomReminder(trackedGame, game, reminderOccurrence);
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
            var cadenceName = trackedGame.ResetCadence == ResetCadence.Daily
                ? "DAILY"
                : "WEEKLY";
            var reason = $"{cadenceName.ToLowerInvariant()} reset";
            ChecklistService.Reset(trackedGame);
            ApplyTaskState(trackedGame, TaskState.INCOMPLETE, false, reason);
            ReconcileChecklistDrivenState(trackedGame, $"{reason} checklist reconciliation");
            trackedGame.LastResetProcessedLocal = occurrence;

            // Persist the occurrence before publishing its notification. If Playnite exits
            // immediately afterward, this reset still cannot be notified a second time.
            PersistAndReconcile(new[] { trackedGame });

            var name = GetDisplayName(trackedGame, game);
            var notificationId =
                $"GameRoutines_Reset_{trackedGame.ResetCadence}_{game.Id:N}_{occurrence.Ticks}";
            PlayniteApi.Notifications.Add(new NotificationMessage(
                notificationId,
                $"{name.ToUpperInvariant()}: {cadenceName} TASKS RESET\r\nTasks are available.",
                NotificationType.Info));

            logger.Info(
                $"Processed {cadenceName.ToLowerInvariant()} reset for {name} ({game.Id}) " +
                $"at {occurrence:O}; task state is {GetUserFacingStateName(trackedGame.CurrentState)}.");
        }

        private void ProcessCustomReminder(
            TrackedGameSettings trackedGame,
            Game game,
            DateTime occurrence)
        {
            trackedGame.LastReminderProcessedLocal = occurrence;
            SavePluginSettings(settings.Settings);

            var notificationId =
                $"GameRoutines_Reminder_{trackedGame.ReminderCadence}_{game.Id:N}_{occurrence.Ticks}";
            var name = GetDisplayName(trackedGame, game).ToUpperInvariant();
            PlayniteApi.Notifications.Add(new NotificationMessage(
                notificationId,
                $"{name}: {trackedGame.CustomReminderTitle}\r\n" +
                trackedGame.CustomReminderMessage,
                NotificationType.Info));

            logger.Info(
                $"Processed custom reminder for {GetDisplayName(trackedGame, game)} " +
                $"({game.Id}) at {occurrence:O}; task state and checklist were not changed.");
        }

        private void TrackSelectedGames(IEnumerable<Game> selectedGames)
        {
            try
            {
                var selection = (selectedGames ?? Enumerable.Empty<Game>())
                    .Where(a => a != null)
                    .GroupBy(a => a.Id)
                    .Select(a => a.First())
                    .ToList();
                var addedGames = TrackGames(selection, true);
                if (selection.Count > 1 && addedGames.Count > 0)
                {
                    var gameLabel = addedGames.Count == 1 ? "game" : "games";
                    PlayniteApi.Notifications.Add(new NotificationMessage(
                        "GameRoutines_Tracking_Result",
                        $"GAME ROUTINES\r\nStarted tracking {addedGames.Count} {gameLabel}.",
                        NotificationType.Info));
                }
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Failed to track selected game(s).");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Game Routines could not track the selected game(s). See the Playnite log for details.",
                    "Game Routines");
            }
        }

        private void SetSelectedGamesState(IEnumerable<Game> selectedGames, TaskState newState)
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
                        "Game Routines",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    return;
                }

                foreach (var trackedGame in trackedGames)
                {
                    ChecklistService.Reset(trackedGame);
                    ApplyTaskState(trackedGame, TaskState.INCOMPLETE, false, "checklist reset");
                    ReconcileChecklistDrivenState(trackedGame, "checklist reset reconciliation");
                }

                PersistAndReconcile(trackedGames);
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Failed to reset selected game checklist(s).");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Game Routines could not reset the selected checklist(s). See the Playnite log for details.",
                    "Game Routines");
            }
        }

        private bool SetTrackedGamesState(IEnumerable<Guid> gameIds, TaskState newState)
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

                    ApplyTaskState(trackedGame, newState, false, "manual task-state change");
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
                    "Game Routines could not update the selected game tasks. See the Playnite log for details.",
                    "Game Routines");
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
                ApplyTaskState(trackedGame, TaskState.COMPLETE, true, reason);
            }
            else
            {
                ApplyTaskState(trackedGame, TaskState.INCOMPLETE, false, reason);
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

        private void ApplyTaskState(
            TrackedGameSettings trackedGame,
            TaskState newState,
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
                    $"Task state for {GetDisplayName(trackedGame, game)} ({trackedGame.GameId}) " +
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
                ReconcileTasksAvailableTag(trackedGame);
            }

            foreach (var trackedGame in affectedGames)
            {
                NotifyUiStateChanged(trackedGame.GameId);
            }
        }

        private void ReconcileTasksAvailableTag(TrackedGameSettings trackedGame)
        {
            UpdateTasksAvailableTag(
                trackedGame.GameId,
                settings.Settings.UseTasksAvailableTag &&
                trackedGame.Enabled &&
                trackedGame.CurrentState == TaskState.INCOMPLETE);
        }

        private void ReconcileAllTasksAvailableTags()
        {
            foreach (var trackedGame in settings.TrackedGames)
            {
                try
                {
                    ReconcileTasksAvailableTag(trackedGame);
                }
                catch (Exception exception)
                {
                    logger.Error(
                        exception,
                        $"Failed to reconcile Game Routines tags for game {trackedGame.GameId}.");
                }
            }
        }

        private void UpdateTasksAvailableTag(Guid gameId, bool shouldHaveTag)
        {
            // Context-menu game objects can be older than the database object after an
            // earlier metadata update. Always re-fetch by authoritative Game.Id before
            // inspecting TagIds or a stale object can incorrectly skip tag removal.
            var game = PlayniteApi.Database.Games.Get(gameId);
            if (game == null)
            {
                return;
            }
            var tasksAvailableTag = FindTag(TasksAvailableTagName);
            var legacyWeekliesTag = FindTag(LegacyWeekliesTagName);
            var legacyReadyTag = FindTag(LegacyReadyTagName);

            if (tasksAvailableTag == null && shouldHaveTag)
            {
                tasksAvailableTag = PlayniteApi.Database.Tags.Add(
                    TasksAvailableTagName,
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

            if (tasksAvailableTag != null &&
                shouldHaveTag &&
                !updatedTagIds.Contains(tasksAvailableTag.Id))
            {
                updatedTagIds.Add(tasksAvailableTag.Id);
                changed = true;
            }
            else if (tasksAvailableTag != null && !shouldHaveTag)
            {
                changed |= updatedTagIds.RemoveAll(a => a == tasksAvailableTag.Id) > 0;
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

        private static string GetUserFacingStateName(TaskState state)
        {
            return state == TaskState.COMPLETE ? "COMPLETE" : "INCOMPLETE";
        }
    }
}
