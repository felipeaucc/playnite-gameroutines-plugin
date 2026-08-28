using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private const string FusionXDesktopThemeId =
            "FusionX_54244ec8-29ec-418e-bce7-415250c8d67b";
        internal const string ChecklistElementName = "Checklist";
        internal const string StateToggleElementName = "StateToggle";
        internal const string IncompleteIndicatorElementName = "IncompleteIndicator";
        private static readonly TimeSpan SchedulerInterval = TimeSpan.FromMinutes(1);

        private readonly GameRoutinesSettingsViewModel settings;
        private readonly HashSet<Guid> loggedMissingGameIds = new HashSet<Guid>();
        private readonly HashSet<string> incompleteIndicatorSupportedDesktopThemeIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Guid, Window> openChecklistWindows = new Dictionary<Guid, Window>();
        private readonly Dictionary<string, Window> openRoutineChecklistWindows = new Dictionary<string, Window>();
        private readonly Dictionary<Guid, Window> openManageChecklistWindows = new Dictionary<Guid, Window>();
        private readonly Dictionary<Guid, Window> openCustomReminderWindows = new Dictionary<Guid, Window>();
        private readonly PersistentNotificationService persistentNotifications;
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
            persistentNotifications = new PersistentNotificationService(this);
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
                    if (args.Mode == ApplicationMode.Desktop)
                    {
                        RecordIncompleteIndicatorThemeSupport();
                    }

                    return new GameRoutinesIncompleteIndicatorControl(this);
                default:
                    return null;
            }
        }

        public override void OnApplicationStarted(OnApplicationStartedEventArgs args)
        {
            logger.Info($"Game Routines startup. Processing {settings.TrackedGames.Count} tracked game(s).");
            persistentNotifications.Start();
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
            persistentNotifications.Stop();
            logger.Info("Game Routines stopped.");
        }

        public override void Dispose()
        {
            CloseChecklistWindows();
            StopScheduler();
            persistentNotifications.Dispose();
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
                    Description = "Reset All Routine Checklists",
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
                ProcessDueEvents();
            }
        }

        internal void OpenSettingsForGame(Guid gameId)
        {
            settings.SelectTrackedGame(gameId);
            OpenSettingsView();
        }

        internal void OpenCustomReminderWindow(Guid gameId)
        {
            try
            {
                if (!IsGameTracked(gameId))
                {
                    return;
                }

                if (openCustomReminderWindows.TryGetValue(gameId, out var existingWindow))
                {
                    ActivateWindow(existingWindow);
                    return;
                }

                var viewModel = new CustomReminderViewModel(this, gameId);
                var window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                {
                    ShowCloseButton = true,
                    ShowMaximizeButton = false,
                    ShowMinimizeButton = true
                });
                window.Title = "Custom Reminder";
                window.Width = 650;
                window.Height = 520;
                window.MinWidth = 520;
                window.MinHeight = 420;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                var owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                if (owner != null && !ReferenceEquals(owner, window))
                {
                    window.Owner = owner;
                }

                window.Content = new CustomReminderWindowView { DataContext = viewModel };
                window.Closed += (sender, args) =>
                {
                    if (openCustomReminderWindows.TryGetValue(gameId, out var registeredWindow) &&
                        ReferenceEquals(registeredWindow, window))
                    {
                        openCustomReminderWindows.Remove(gameId);
                    }
                };

                openCustomReminderWindows[gameId] = window;
                window.Show();
            }
            catch (Exception exception)
            {
                logger.Error(exception, $"Failed to open custom reminder editor for game {gameId}.");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Game Routines could not open the Custom Reminder editor. See the Playnite log for details.",
                    "Game Routines");
            }
        }

        internal bool SaveCustomReminder(Guid gameId, TrackedGameSettings editedReminder)
        {
            var trackedGame = FindTrackedGame(gameId);
            if (trackedGame == null || editedReminder == null)
            {
                return false;
            }

            var scheduleChanged = HasReminderScheduleChanged(trackedGame, editedReminder);
            CustomReminderService.Apply(editedReminder, trackedGame);
            if (scheduleChanged)
            {
                PrepareReminderScheduleAfterChange(trackedGame, DateTime.Now);
            }
            PersistAndReconcile(new[] { trackedGame });
            return true;
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
                    ShowIncompleteCoverIndicator = true,
                    CustomReminderEnabled = false,
                    ReminderCadence = ReminderCadence.Weekly,
                    ReminderDay = DayOfWeek.Monday,
                    ReminderTime = "00:00"
                };
                trackedGame.Routines.Add(RoutineService.CreateDefault("Tasks", 0));
                settings.TrackedGames.Add(trackedGame);
                addedGames.Add(trackedGame);
            }

            if (persistChanges && addedGames.Count > 0)
            {
                PersistAndReconcile(addedGames);
            }

            return addedGames;
        }

        internal TrackedGameSettings GetTrackedGameSettings(Guid gameId)
        {
            return FindTrackedGame(gameId);
        }

        internal RoutineSettings GetRoutine(Guid gameId, Guid routineId)
        {
            return FindTrackedGame(gameId)?.Routines?
                .FirstOrDefault(a => a != null && a.Id == routineId);
        }

        internal string GetTrackedGameState(Guid gameId)
        {
            var trackedGame = FindTrackedGame(gameId);
            return trackedGame == null ? null : GetUserFacingStateName(trackedGame.CurrentState);
        }

        internal bool ShouldShowIncompleteCoverIndicator(Guid gameId)
        {
            var trackedGame = FindTrackedGame(gameId);
            return trackedGame != null &&
                   trackedGame.Enabled &&
                   trackedGame.CurrentState == TaskState.INCOMPLETE &&
                   settings.Settings.ShowIncompleteCoverIndicator &&
                   trackedGame.ShowIncompleteCoverIndicator;
        }

        internal bool IsIncompleteIndicatorSupportedByCurrentTheme
        {
            get
            {
                var themeId = PlayniteApi.ApplicationSettings.DesktopTheme;
                return !string.IsNullOrEmpty(themeId) &&
                    incompleteIndicatorSupportedDesktopThemeIds.Contains(themeId);
            }
        }

        internal bool UseFusionXChecklistIconWeight =>
            string.Equals(
                PlayniteApi.ApplicationSettings.DesktopTheme,
                FusionXDesktopThemeId,
                StringComparison.OrdinalIgnoreCase);

        private void RecordIncompleteIndicatorThemeSupport()
        {
            var themeId = PlayniteApi.ApplicationSettings.DesktopTheme;
            if (!string.IsNullOrEmpty(themeId) &&
                incompleteIndicatorSupportedDesktopThemeIds.Add(themeId))
            {
                settings.NotifyIncompleteIndicatorThemeSupportChanged();
            }
        }

        internal bool MarkTrackedGameComplete(Guid gameId)
        {
            return SetTrackedGamesState(new[] { gameId }, TaskState.COMPLETE);
        }

        internal bool MarkTrackedGameIncomplete(Guid gameId)
        {
            return SetTrackedGamesState(new[] { gameId }, TaskState.INCOMPLETE);
        }

        internal ChecklistProgress GetChecklistProgress(Guid gameId, Guid routineId)
        {
            return ChecklistService.GetProgress(GetRoutine(gameId, routineId));
        }

        internal RoutineSettings AddRoutine(TrackedGameSettings trackedGame, bool persistChanges)
        {
            if (trackedGame == null)
            {
                return null;
            }

            var routine = RoutineService.CreateDefault(
                RoutineService.GenerateUniqueName(trackedGame.Routines),
                trackedGame.Routines.Count);
            trackedGame.Routines.Add(routine);
            trackedGame.NotifyOverallStateChanged();
            CompleteRoutineMutation(trackedGame, persistChanges);
            return routine;
        }

        internal bool RenameRoutine(
            TrackedGameSettings trackedGame,
            Guid routineId,
            string name,
            bool persistChanges)
        {
            var routine = trackedGame?.Routines?.FirstOrDefault(a => a != null && a.Id == routineId);
            if (routine == null)
            {
                return false;
            }

            if (!RoutineService.TryValidateName(
                    trackedGame, routineId, name, out var normalizedName, out var error))
            {
                PlayniteApi.Dialogs.ShowMessage(
                    error,
                    "Game Routines",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            if (string.Equals(routine.Name, normalizedName, StringComparison.Ordinal))
            {
                return true;
            }

            routine.Name = normalizedName;
            CompleteRoutineMutation(trackedGame, persistChanges);
            return true;
        }

        internal bool DeleteRoutine(
            TrackedGameSettings trackedGame,
            Guid routineId,
            bool confirm,
            bool persistChanges)
        {
            var routine = trackedGame?.Routines?.FirstOrDefault(a => a != null && a.Id == routineId);
            if (routine == null)
            {
                return false;
            }

            if (confirm && PlayniteApi.Dialogs.ShowMessage(
                    $"Delete the \"{routine.Name}\" checklist?\r\n\r\n" +
                    "This will permanently remove its checklist items and routine settings.",
                    "Delete Checklist",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return false;
            }

            trackedGame.Routines.Remove(routine);
            CloseRoutineChecklistWindow(trackedGame.GameId, routine.Id);
            for (var index = 0; index < trackedGame.Routines.Count; index++)
            {
                trackedGame.Routines[index].Order = index;
            }

            trackedGame.NotifyOverallStateChanged();
            CompleteRoutineMutation(trackedGame, persistChanges);
            return true;
        }

        internal bool MoveRoutine(
            TrackedGameSettings trackedGame,
            Guid routineId,
            int offset,
            bool persistChanges)
        {
            if (!RoutineService.MoveRoutine(trackedGame, routineId, offset))
            {
                return false;
            }

            CompleteRoutineMutation(trackedGame, persistChanges);
            return true;
        }

        internal bool CommitRoutineScheduleChange(Guid gameId, Guid routineId)
        {
            var trackedGame = FindTrackedGame(gameId);
            var routine = trackedGame?.Routines?.FirstOrDefault(a => a != null && a.Id == routineId);
            if (routine == null)
            {
                return false;
            }

            PrepareResetScheduleAfterChange(routine, DateTime.Now);
            PersistAndReconcile(new[] { trackedGame });
            return true;
        }

        internal bool SetRoutineState(
            TrackedGameSettings trackedGame,
            RoutineSettings routine,
            TaskState newState,
            bool persistChanges)
        {
            if (trackedGame == null || routine == null || !trackedGame.Routines.Contains(routine))
            {
                return false;
            }

            if (routine.AutomaticallyCompleteFromChecklist)
            {
                var checklistState = GetChecklistDerivedState(routine);
                if (checklistState != newState)
                {
                    ShowAutomaticCompletionWarning(
                        new[] { new AutomaticCompletionBlocker(trackedGame, routine, checklistState) },
                        newState,
                        false);
                    return false;
                }
            }

            ApplyRoutineState(
                trackedGame,
                routine,
                newState,
                routine.AutomaticallyCompleteFromChecklist && newState == TaskState.COMPLETE,
                "manual routine-state change");
            if (persistChanges)
            {
                PersistAndReconcile(new[] { trackedGame });
            }
            else
            {
                trackedGame.NotifyOverallStateChanged();
                ReconcileTasksAvailableTag(trackedGame);
                NotifyUiStateChanged(trackedGame.GameId);
            }

            return true;
        }

        internal bool SetRoutineState(Guid gameId, Guid routineId, TaskState newState)
        {
            var trackedGame = FindTrackedGame(gameId);
            return SetRoutineState(trackedGame, GetRoutine(gameId, routineId), newState, true);
        }

        internal void RoutineAggregateSettingChanged(
            TrackedGameSettings trackedGame,
            bool persistChanges)
        {
            if (trackedGame == null)
            {
                return;
            }

            trackedGame.NotifyOverallStateChanged();
            if (persistChanges)
            {
                PersistAndReconcile(new[] { trackedGame });
            }
            else
            {
                ReconcileTasksAvailableTag(trackedGame);
                NotifyUiStateChanged(trackedGame.GameId);
            }
        }

        internal bool SetRoutineCountTowardOverallTaskStatus(
            Guid gameId,
            Guid routineId,
            bool enabled)
        {
            var trackedGame = FindTrackedGame(gameId);
            var routine = trackedGame?.Routines?
                .FirstOrDefault(a => a != null && a.Id == routineId);
            if (routine == null)
            {
                return false;
            }

            if (routine.CountTowardOverallTaskStatus == enabled)
            {
                return true;
            }

            routine.CountTowardOverallTaskStatus = enabled;
            RoutineAggregateSettingChanged(trackedGame, true);
            return true;
        }

        internal bool SetChecklistItemChecked(
            Guid gameId,
            Guid routineId,
            Guid itemId,
            bool isChecked)
        {
            var trackedGame = FindTrackedGame(gameId);
            var routine = GetRoutine(gameId, routineId);
            if (!ChecklistService.SetItemChecked(routine, itemId, isChecked))
            {
                return false;
            }

            CompleteChecklistMutation(trackedGame, routine, true, "checklist item state changed");
            return true;
        }

        internal bool AddChecklistItem(
            TrackedGameSettings trackedGame,
            RoutineSettings routine,
            string text,
            bool persistChanges)
        {
            if (trackedGame == null || routine == null || !trackedGame.Routines.Contains(routine) ||
                ChecklistService.AddItem(routine, text) == null)
            {
                return false;
            }

            CompleteChecklistMutation(trackedGame, routine, persistChanges, "checklist item added");
            return true;
        }

        internal bool AddChecklistItem(Guid gameId, Guid routineId, string text)
        {
            var trackedGame = FindTrackedGame(gameId);
            return AddChecklistItem(trackedGame, GetRoutine(gameId, routineId), text, true);
        }

        internal bool EditChecklistItem(
            TrackedGameSettings trackedGame,
            RoutineSettings routine,
            Guid itemId,
            string text,
            bool persistChanges)
        {
            if (trackedGame == null || routine == null || !trackedGame.Routines.Contains(routine) ||
                !ChecklistService.EditItem(routine, itemId, text))
            {
                return false;
            }

            CompleteChecklistMutation(trackedGame, routine, persistChanges, "checklist item edited");
            return true;
        }

        internal bool EditChecklistItem(Guid gameId, Guid routineId, Guid itemId, string text)
        {
            var trackedGame = FindTrackedGame(gameId);
            return EditChecklistItem(trackedGame, GetRoutine(gameId, routineId), itemId, text, true);
        }

        internal bool DeleteChecklistItem(
            TrackedGameSettings trackedGame,
            RoutineSettings routine,
            Guid itemId,
            bool persistChanges)
        {
            if (trackedGame == null || routine == null || !trackedGame.Routines.Contains(routine) ||
                !ChecklistService.DeleteItem(routine, itemId))
            {
                return false;
            }

            CompleteChecklistMutation(trackedGame, routine, persistChanges, "checklist item deleted");
            return true;
        }

        internal bool DeleteChecklistItem(Guid gameId, Guid routineId, Guid itemId)
        {
            var trackedGame = FindTrackedGame(gameId);
            return DeleteChecklistItem(trackedGame, GetRoutine(gameId, routineId), itemId, true);
        }

        internal bool MoveChecklistItem(
            TrackedGameSettings trackedGame,
            RoutineSettings routine,
            Guid itemId,
            int offset,
            bool persistChanges)
        {
            if (trackedGame == null || routine == null || !trackedGame.Routines.Contains(routine) ||
                !ChecklistService.MoveItem(routine, itemId, offset))
            {
                return false;
            }

            if (persistChanges)
            {
                PersistAndReconcile(new[] { trackedGame });
            }
            else
            {
                NotifyUiStateChanged(trackedGame.GameId);
            }

            return true;
        }

        internal bool MoveChecklistItem(Guid gameId, Guid routineId, Guid itemId, int offset)
        {
            var trackedGame = FindTrackedGame(gameId);
            return MoveChecklistItem(trackedGame, GetRoutine(gameId, routineId), itemId, offset, true);
        }

        internal void ChecklistItemStateChanged(
            TrackedGameSettings trackedGame,
            RoutineSettings routine,
            bool persistChanges)
        {
            CompleteChecklistMutation(trackedGame, routine, persistChanges, "checklist item state changed");
        }

        internal void ChecklistAutoCompletionChanged(
            TrackedGameSettings trackedGame,
            RoutineSettings routine,
            bool persistChanges)
        {
            CompleteChecklistMutation(
                trackedGame,
                routine,
                persistChanges,
                "checklist auto-completion setting changed");
        }

        internal bool SetChecklistAutoCompletion(Guid gameId, Guid routineId, bool enabled)
        {
            var trackedGame = FindTrackedGame(gameId);
            var routine = GetRoutine(gameId, routineId);
            if (trackedGame == null || routine == null ||
                routine.AutomaticallyCompleteFromChecklist == enabled)
            {
                return false;
            }

            routine.AutomaticallyCompleteFromChecklist = enabled;
            ChecklistAutoCompletionChanged(trackedGame, routine, true);
            return true;
        }

        internal bool ResetChecklist(Guid gameId, Guid routineId, bool confirm)
        {
            var trackedGame = FindTrackedGame(gameId);
            return ResetChecklist(trackedGame, GetRoutine(gameId, routineId), confirm, true);
        }

        internal bool ResetChecklist(
            TrackedGameSettings trackedGame,
            RoutineSettings routine,
            bool confirm,
            bool persistChanges)
        {
            if (trackedGame == null || routine == null || !trackedGame.Routines.Contains(routine))
            {
                return false;
            }

            if (confirm && ChecklistService.GetProgress(routine).Completed > 0 &&
                PlayniteApi.Dialogs.ShowMessage(
                    $"Reset the checklist for \"{routine.Name}\"?",
                    "Game Routines",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return false;
            }

            ChecklistService.Reset(routine);
            ApplyRoutineState(trackedGame, routine, TaskState.INCOMPLETE, false, "checklist reset");
            ReconcileChecklistDrivenState(trackedGame, routine, "checklist reset reconciliation");
            if (persistChanges)
            {
                PersistAndReconcile(new[] { trackedGame });
            }
            else
            {
                trackedGame.NotifyOverallStateChanged();
                ReconcileTasksAvailableTag(trackedGame);
                NotifyUiStateChanged(trackedGame.GameId);
            }

            return true;
        }

        internal void ReconcileTrackedGameTag(Guid gameId)
        {
            var trackedGame = FindTrackedGame(gameId);
            if (trackedGame != null)
            {
                ReconcileTasksAvailableTag(trackedGame);
            }
        }

        internal void PrepareSettingsForSave(GameRoutinesSettings previous, GameRoutinesSettings current)
        {
            var now = DateTime.Now;
            var previousById = (previous?.TrackedGames ?? new ObservableCollection<TrackedGameSettings>())
                .GroupBy(a => a.GameId)
                .ToDictionary(a => a.Key, a => a.First());

            current.SchemaVersion = GameRoutinesSettings.CurrentSchemaVersion;
            foreach (var trackedGame in current.TrackedGames)
            {
                RoutineService.Normalize(trackedGame);
                previousById.TryGetValue(trackedGame.GameId, out var oldGame);
                var oldRoutines = (oldGame?.Routines ?? new ObservableCollection<RoutineSettings>())
                    .Where(a => a != null)
                    .GroupBy(a => a.Id)
                    .ToDictionary(a => a.Key, a => a.First());

                foreach (var routine in trackedGame.Routines)
                {
                    ReconcileChecklistDrivenState(trackedGame, routine, "settings saved");
                    if (!oldRoutines.TryGetValue(routine.Id, out var oldRoutine) ||
                        HasResetScheduleChanged(oldRoutine, routine) ||
                        (routine.ResetCadence == ResetCadence.BiWeekly &&
                         !routine.BiWeeklyResetAnchorLocal.HasValue))
                    {
                        PrepareResetScheduleAfterChange(routine, now);
                    }
                }

                if (trackedGame.CustomReminderEnabled &&
                    (oldGame == null || HasReminderScheduleChanged(oldGame, trackedGame) ||
                     (trackedGame.ReminderCadence == ReminderCadence.BiWeekly &&
                      !trackedGame.BiWeeklyReminderAnchorLocal.HasValue)))
                {
                    PrepareReminderScheduleAfterChange(trackedGame, now);
                }

                trackedGame.NotifyOverallStateChanged();
            }
        }

        internal void ApplySettingsChanges(GameRoutinesSettings previous, GameRoutinesSettings current)
        {
            try
            {
                var currentById = current.TrackedGames.ToDictionary(a => a.GameId);
                foreach (var removedGameId in openChecklistWindows.Keys
                    .Where(a => !currentById.ContainsKey(a)).ToList())
                {
                    openChecklistWindows[removedGameId].Close();
                }

                foreach (var routineWindowKey in openRoutineChecklistWindows.Keys
                    .Where(key => !currentById.Keys.Any(gameId =>
                        key.StartsWith($"{gameId:N}:", StringComparison.OrdinalIgnoreCase)))
                    .ToList())
                {
                    openRoutineChecklistWindows[routineWindowKey].Close();
                }

                foreach (var removedGameId in openManageChecklistWindows.Keys
                    .Where(a => !currentById.ContainsKey(a)).ToList())
                {
                    openManageChecklistWindows[removedGameId].Close();
                }

                foreach (var removedGameId in openCustomReminderWindows.Keys
                    .Where(a => !currentById.ContainsKey(a)).ToList())
                {
                    openCustomReminderWindows[removedGameId].Close();
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

        private static bool HasResetScheduleChanged(RoutineSettings previous, RoutineSettings current)
        {
            return previous.ResetCadence != current.ResetCadence ||
                   (current.ResetCadence == ResetCadence.Weekly &&
                    previous.ResetDay != current.ResetDay) ||
                   !TimesEqual(previous.ResetTime, current.ResetTime) ||
                   (current.ResetCadence == ResetCadence.BiWeekly &&
                    previous.BiWeeklyResetAnchorLocal != current.BiWeeklyResetAnchorLocal);
        }

        private static bool HasReminderScheduleChanged(
            TrackedGameSettings previous,
            TrackedGameSettings current)
        {
            return !previous.CustomReminderEnabled ||
                   previous.ReminderCadence != current.ReminderCadence ||
                   (current.ReminderCadence == ReminderCadence.Weekly &&
                    previous.ReminderDay != current.ReminderDay) ||
                   !TimesEqual(previous.ReminderTime, current.ReminderTime) ||
                   (current.ReminderCadence == ReminderCadence.BiWeekly &&
                    previous.BiWeeklyReminderAnchorLocal != current.BiWeeklyReminderAnchorLocal);
        }

        private static void PrepareResetScheduleAfterChange(RoutineSettings routine, DateTime now)
        {
            if (routine.ResetCadence == ResetCadence.BiWeekly &&
                ScheduleCalculator.TryParseLocalTime(routine.ResetTime, out var biWeeklyTime))
            {
                routine.BiWeeklyResetAnchorLocal = routine.BiWeeklyResetAnchorLocal.HasValue
                    ? DateTime.SpecifyKind(
                        routine.BiWeeklyResetAnchorLocal.Value.Date.Add(biWeeklyTime),
                        DateTimeKind.Local)
                    : ScheduleCalculator.GetFirstFutureWeeklyOccurrence(
                        now,
                        routine.ResetDay,
                        biWeeklyTime);
                if (ScheduleCalculator.TryGetMostRecentOccurrence(
                    now,
                    routine.ResetCadence,
                    routine.ResetDay,
                    biWeeklyTime,
                    routine.BiWeeklyResetAnchorLocal,
                    out var biWeeklyOccurrence))
                {
                    routine.LastResetProcessedLocal = biWeeklyOccurrence;
                }
                else
                {
                    routine.LastResetProcessedLocal = null;
                }
                return;
            }

            if (routine.ResetCadence != ResetCadence.Never &&
                ScheduleCalculator.TryParseLocalTime(routine.ResetTime, out var resetTime) &&
                ScheduleCalculator.TryGetMostRecentOccurrence(
                    now,
                    routine.ResetCadence,
                    routine.ResetDay,
                    resetTime,
                    null,
                    out var resetOccurrence))
            {
                routine.LastResetProcessedLocal = resetOccurrence;
            }
        }

        private static void PrepareReminderScheduleAfterChange(
            TrackedGameSettings trackedGame,
            DateTime now)
        {
            if (trackedGame.ReminderCadence == ReminderCadence.BiWeekly &&
                ScheduleCalculator.TryParseLocalTime(trackedGame.ReminderTime, out var biWeeklyTime))
            {
                trackedGame.BiWeeklyReminderAnchorLocal = trackedGame.BiWeeklyReminderAnchorLocal.HasValue
                    ? DateTime.SpecifyKind(
                        trackedGame.BiWeeklyReminderAnchorLocal.Value.Date.Add(biWeeklyTime),
                        DateTimeKind.Local)
                    : ScheduleCalculator.GetFirstFutureWeeklyOccurrence(
                        now,
                        trackedGame.ReminderDay,
                        biWeeklyTime);
                if (ScheduleCalculator.TryGetMostRecentOccurrence(
                    now,
                    trackedGame.ReminderCadence,
                    trackedGame.ReminderDay,
                    biWeeklyTime,
                    trackedGame.BiWeeklyReminderAnchorLocal,
                    out var biWeeklyOccurrence))
                {
                    trackedGame.LastReminderProcessedLocal = biWeeklyOccurrence;
                }
                else
                {
                    trackedGame.LastReminderProcessedLocal = null;
                }
                return;
            }

            if (trackedGame.CustomReminderEnabled &&
                ScheduleCalculator.TryParseLocalTime(trackedGame.ReminderTime, out var reminderTime) &&
                ScheduleCalculator.TryGetMostRecentOccurrence(
                    now,
                    trackedGame.ReminderCadence,
                    trackedGame.ReminderDay,
                    reminderTime,
                    null,
                    out var reminderOccurrence))
            {
                trackedGame.LastReminderProcessedLocal = reminderOccurrence;
            }
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

        internal void OpenChecklistWindow(Guid gameId, Guid? routineId = null)
        {
            try
            {
                if (!IsGameTracked(gameId))
                {
                    return;
                }

                var routine = routineId.HasValue ? GetRoutine(gameId, routineId.Value) : null;
                if (routineId.HasValue && routine == null)
                {
                    return;
                }

                var routineWindowKey = routineId.HasValue
                    ? GetRoutineWindowKey(gameId, routineId.Value)
                    : null;
                Window existingWindow;
                if ((!routineId.HasValue && openChecklistWindows.TryGetValue(gameId, out existingWindow)) ||
                    (routineId.HasValue && openRoutineChecklistWindows.TryGetValue(routineWindowKey, out existingWindow)))
                {
                    ActivateWindow(existingWindow);
                    return;
                }

                var viewModel = new GameChecklistViewModel(this, gameId, routineId);
                var window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                {
                    ShowCloseButton = true,
                    ShowMaximizeButton = true,
                    ShowMinimizeButton = true
                });
                window.Title = routineId.HasValue
                    ? $"{GetRoutineDisplayName(routine)} Checklist"
                    : "Checklists";
                window.Width = routineId.HasValue ? 560 : 660;
                window.Height = 650;
                window.MinWidth = 440;
                window.MinHeight = 360;
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                window.Owner = null;
                window.ShowInTaskbar = true;

                window.Content = new GameChecklistView
                {
                    DataContext = viewModel,
                    ShowOpenWindowButton = false,
                    ShowRoutinePopOutButtons = false
                };
                window.Closed += (sender, args) =>
                {
                    if (routineId.HasValue)
                    {
                        if (openRoutineChecklistWindows.TryGetValue(routineWindowKey, out var registeredRoutineWindow) &&
                            ReferenceEquals(registeredRoutineWindow, window))
                        {
                            openRoutineChecklistWindows.Remove(routineWindowKey);
                        }
                    }
                    else if (openChecklistWindows.TryGetValue(gameId, out var registeredWindow) &&
                             ReferenceEquals(registeredWindow, window))
                    {
                        openChecklistWindows.Remove(gameId);
                    }

                    viewModel.Dispose();
                };

                if (routineId.HasValue)
                {
                    openRoutineChecklistWindows[routineWindowKey] = window;
                }
                else
                {
                    openChecklistWindows[gameId] = window;
                }
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

        internal void OpenManageChecklistWindow(Guid gameId, Guid? routineId = null)
        {
            try
            {
                if (!IsGameTracked(gameId))
                {
                    return;
                }

                if (openManageChecklistWindows.TryGetValue(gameId, out var existingWindow))
                {
                    SelectRoutineInWindow(existingWindow, routineId);
                    ActivateWindow(existingWindow);
                    return;
                }

                var viewModel = new ManageChecklistViewModel(this, gameId, routineId);
                var window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                {
                    ShowCloseButton = true,
                    ShowMaximizeButton = true,
                    ShowMinimizeButton = true
                });
                window.Title = "Manage Checklist";
                window.Width = 620;
                window.Height = 650;
                window.MinWidth = 480;
                window.MinHeight = 340;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                var owner = PlayniteApi.Dialogs.GetCurrentAppWindow();
                if (owner != null && !ReferenceEquals(owner, window))
                {
                    window.Owner = owner;
                }

                window.Content = new ManageChecklistView { DataContext = viewModel };
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

        private static void SelectRoutineInWindow(Window window, Guid? routineId)
        {
            if (!routineId.HasValue || !(window?.Content is FrameworkElement content))
            {
                return;
            }

            if (content.DataContext is ManageChecklistViewModel manageViewModel)
            {
                manageViewModel.SelectRoutine(routineId.Value);
            }
        }

        private static void ActivateWindow(Window window)
        {
            if (window == null)
            {
                return;
            }

            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Activate();
        }

        private static string GetRoutineWindowKey(Guid gameId, Guid routineId)
        {
            return $"{gameId:N}:{routineId:N}";
        }

        private void CloseRoutineChecklistWindow(Guid gameId, Guid routineId)
        {
            var key = GetRoutineWindowKey(gameId, routineId);
            if (openRoutineChecklistWindows.TryGetValue(key, out var window))
            {
                window.Close();
            }
        }

        private void CloseChecklistWindows()
        {
            foreach (var window in openChecklistWindows.Values.ToList())
            {
                window.Close();
            }
            openChecklistWindows.Clear();

            foreach (var window in openRoutineChecklistWindows.Values.ToList())
            {
                window.Close();
            }
            openRoutineChecklistWindows.Clear();

            foreach (var window in openManageChecklistWindows.Values.ToList())
            {
                window.Close();
            }
            openManageChecklistWindows.Clear();

            foreach (var window in openCustomReminderWindows.Values.ToList())
            {
                window.Close();
            }
            openCustomReminderWindows.Clear();
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
                        logger.Error(exception, $"Failed to process schedules for tracked game {trackedGame.GameId}.");
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

            foreach (var routine in trackedGame.Routines.Where(a => a != null))
            {
                if (routine.ResetCadence == ResetCadence.BiWeekly &&
                    !routine.BiWeeklyResetAnchorLocal.HasValue)
                {
                    PrepareResetScheduleAfterChange(routine, now);
                    settingsChanged = true;
                }
            }

            if (trackedGame.CustomReminderEnabled &&
                trackedGame.ReminderCadence == ReminderCadence.BiWeekly &&
                !trackedGame.BiWeeklyReminderAnchorLocal.HasValue)
            {
                PrepareReminderScheduleAfterChange(trackedGame, now);
                settingsChanged = true;
            }

            foreach (var routine in trackedGame.Routines.OrderBy(a => a.Order).ToList())
            {
                if (ScheduleCalculator.TryParseLocalTime(routine.ResetTime, out var resetTime) &&
                    ScheduleCalculator.TryGetMostRecentOccurrence(
                        now,
                        routine.ResetCadence,
                        routine.ResetDay,
                        resetTime,
                        routine.BiWeeklyResetAnchorLocal,
                        out var resetOccurrence) &&
                    ScheduleCalculator.IsOccurrenceDue(routine.LastResetProcessedLocal, resetOccurrence))
                {
                    ProcessReset(trackedGame, routine, game, resetOccurrence);
                    settingsChanged = false;
                }
            }

            if (trackedGame.CustomReminderEnabled &&
                ScheduleCalculator.TryParseLocalTime(trackedGame.ReminderTime, out var reminderTime))
            {
                if (ScheduleCalculator.TryGetMostRecentOccurrence(
                        now,
                        trackedGame.ReminderCadence,
                        trackedGame.ReminderDay,
                        reminderTime,
                        trackedGame.BiWeeklyReminderAnchorLocal,
                        out var reminderOccurrence) &&
                    ScheduleCalculator.IsOccurrenceDue(trackedGame.LastReminderProcessedLocal, reminderOccurrence))
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

        private void ProcessReset(
            TrackedGameSettings trackedGame,
            RoutineSettings routine,
            Game game,
            DateTime occurrence)
        {
            var reason = $"{CadenceDisplay.GetName(routine.ResetCadence).ToLowerInvariant()} reset";
            ChecklistService.Reset(routine);
            ApplyRoutineState(trackedGame, routine, TaskState.INCOMPLETE, false, reason);
            ReconcileChecklistDrivenState(trackedGame, routine, $"{reason} checklist reconciliation");
            routine.LastResetProcessedLocal = occurrence;

            PersistAndReconcile(new[] { trackedGame });

            var gameName = GetDisplayName(trackedGame, game);
            var routineName = GetRoutineDisplayName(routine);
            var notificationId = $"GameRoutines_Reset_{game.Id:N}_{routine.Id:N}_{occurrence.Ticks}";
            persistentNotifications.Post(
                notificationId,
                game.Id,
                routine.Id,
                "RoutineReset",
                occurrence,
                $"{gameName.ToUpperInvariant()}: {routineName.ToUpperInvariant()} RESET",
                "Tasks are available.",
                NotificationType.Info);

            logger.Info(
                $"Processed {CadenceDisplay.GetName(routine.ResetCadence).ToLowerInvariant()} reset for " +
                $"{gameName} / {routineName} ({game.Id}, {routine.Id}) at {occurrence:O}; " +
                $"routine state is {GetUserFacingStateName(routine.CurrentState)}.");
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
            persistentNotifications.Post(
                notificationId,
                game.Id,
                null,
                "CustomReminder",
                occurrence,
                $"{name}: {trackedGame.CustomReminderTitle}",
                trackedGame.CustomReminderMessage,
                NotificationType.Info);

            logger.Info(
                $"Processed custom reminder for {GetDisplayName(trackedGame, game)} " +
                $"({game.Id}) at {occurrence:O}; routine states and checklists were not changed.");
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
                (selectedGames ?? Enumerable.Empty<Game>()).Where(a => a != null).Select(a => a.Id),
                newState);
        }

        private void ResetSelectedGamesChecklists(IEnumerable<Game> selectedGames)
        {
            try
            {
                var trackedGames = (selectedGames ?? Enumerable.Empty<Game>())
                    .Where(a => a != null)
                    .Select(a => FindTrackedGame(a.Id))
                    .Where(a => a != null)
                    .Distinct()
                    .ToList();
                var routines = trackedGames.SelectMany(a => a.Routines.Select(r => new { Game = a, Routine = r })).ToList();
                if (routines.Count == 0)
                {
                    return;
                }

                if (routines.Any(a => ChecklistService.GetProgress(a.Routine).Completed > 0) &&
                    PlayniteApi.Dialogs.ShowMessage(
                        routines.Count == 1
                            ? $"Reset the checklist for \"{routines[0].Routine.Name}\"?"
                            : $"Reset all {routines.Count} routine checklists for the selected games?",
                        "Game Routines",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question) != MessageBoxResult.Yes)
                {
                    return;
                }

                foreach (var entry in routines)
                {
                    ChecklistService.Reset(entry.Routine);
                    ApplyRoutineState(entry.Game, entry.Routine, TaskState.INCOMPLETE, false, "checklist reset");
                    ReconcileChecklistDrivenState(entry.Game, entry.Routine, "checklist reset reconciliation");
                }

                PersistAndReconcile(trackedGames);
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Failed to reset selected game checklist(s).");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Game Routines could not reset the selected checklists. See the Playnite log for details.",
                    "Game Routines");
            }
        }

        private bool SetTrackedGamesState(IEnumerable<Guid> gameIds, TaskState newState)
        {
            try
            {
                var trackedById = settings.TrackedGames.ToDictionary(a => a.GameId);
                var targets = (gameIds ?? Enumerable.Empty<Guid>())
                    .Distinct()
                    .Where(trackedById.ContainsKey)
                    .Select(a => trackedById[a])
                    .SelectMany(game => RoutineService.GetParticipatingRoutines(game)
                        .Select(routine => new RoutineTarget(game, routine)))
                    .ToList();
                if (targets.Count == 0)
                {
                    return false;
                }

                var blockers = targets
                    .Where(a => a.Routine.AutomaticallyCompleteFromChecklist)
                    .Select(a => new AutomaticCompletionBlocker(
                        a.Game,
                        a.Routine,
                        GetChecklistDerivedState(a.Routine)))
                    .Where(a => a.ChecklistState != newState)
                    .ToList();
                if (blockers.Count > 0)
                {
                    ShowAutomaticCompletionWarning(blockers, newState);
                    return false;
                }

                var affectedGames = new HashSet<TrackedGameSettings>();
                foreach (var target in targets)
                {
                    ApplyRoutineState(
                        target.Game,
                        target.Routine,
                        newState,
                        target.Routine.AutomaticallyCompleteFromChecklist && newState == TaskState.COMPLETE,
                        "manual overall task-state change");
                    affectedGames.Add(target.Game);
                }

                PersistAndReconcile(affectedGames);
                return true;
            }
            catch (Exception exception)
            {
                logger.Error(exception, $"Failed to mark selected game(s) {GetUserFacingStateName(newState)}.");
                PlayniteApi.Dialogs.ShowErrorMessage(
                    "Game Routines could not update the selected game tasks. See the Playnite log for details.",
                    "Game Routines");
                return false;
            }
        }

        private void ShowAutomaticCompletionWarning(
            IReadOnlyList<AutomaticCompletionBlocker> blockers,
            TaskState requestedState,
            bool aggregateChange = true)
        {
            if (!settings.Settings.ShowBlockedManualStateWarning || blockers == null || blockers.Count == 0)
            {
                return;
            }

            try
            {
                var includeGameName = blockers.Select(a => a.Game.GameId).Distinct().Count() > 1;
                var warningEntries = blockers.Select(blocker =>
                {
                    var routineLabel = $"\"{GetRoutineDisplayName(blocker.Routine)}\"";
                    if (includeGameName)
                    {
                        routineLabel = $"{GetDisplayName(blocker.Game, PlayniteApi.Database.Games.Get(blocker.Game.GameId))} - {routineLabel}";
                    }

                    var resolution = requestedState == TaskState.COMPLETE
                        ? "Complete its checklist"
                        : "Uncheck an item";
                    var actionLabel = aggregateChange
                        ? "all counted routines"
                        : "this routine";
                    return new BlockedManualStateWarningEntry
                    {
                        RoutineLabel = routineLabel,
                        CurrentState = GetUserFacingStateName(blocker.ChecklistState),
                        Resolution = resolution,
                        ActionLabel = actionLabel,
                        RequestedState = GetUserFacingStateName(requestedState)
                    };
                }).ToList();
                var warningView = new BlockedManualStateWarningView(warningEntries);
                var window = PlayniteApi.Dialogs.CreateWindow(new WindowCreationOptions
                {
                    ShowCloseButton = true,
                    ShowMaximizeButton = false,
                    ShowMinimizeButton = false
                });
                window.Title = AutomaticCompletionWarningTitle;
                window.Width = 520;
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

        private void CompleteRoutineMutation(TrackedGameSettings trackedGame, bool persistChanges)
        {
            if (trackedGame == null)
            {
                return;
            }

            trackedGame.NotifyOverallStateChanged();
            if (persistChanges)
            {
                PersistAndReconcile(new[] { trackedGame });
            }
            else
            {
                ReconcileTasksAvailableTag(trackedGame);
                NotifyUiStateChanged(trackedGame.GameId);
            }
        }

        private void CompleteChecklistMutation(
            TrackedGameSettings trackedGame,
            RoutineSettings routine,
            bool persistChanges,
            string reason)
        {
            if (trackedGame == null || routine == null)
            {
                return;
            }

            ReconcileChecklistDrivenState(trackedGame, routine, reason);
            if (persistChanges)
            {
                PersistAndReconcile(new[] { trackedGame });
            }
            else
            {
                trackedGame.NotifyOverallStateChanged();
                ReconcileTasksAvailableTag(trackedGame);
                NotifyUiStateChanged(trackedGame.GameId);
            }
        }

        private void ReconcileChecklistDrivenState(
            TrackedGameSettings trackedGame,
            RoutineSettings routine,
            string reason)
        {
            if (!routine.AutomaticallyCompleteFromChecklist)
            {
                routine.CompletedAutomaticallyByChecklist = false;
                return;
            }

            var checklistState = GetChecklistDerivedState(routine);
            ApplyRoutineState(
                trackedGame,
                routine,
                checklistState,
                checklistState == TaskState.COMPLETE,
                reason);
        }

        private static TaskState GetChecklistDerivedState(RoutineSettings routine)
        {
            return ChecklistService.GetProgress(routine).IsComplete
                ? TaskState.COMPLETE
                : TaskState.INCOMPLETE;
        }

        private void ReconcileAllChecklistStates()
        {
            var changed = false;
            foreach (var trackedGame in settings.TrackedGames)
            {
                foreach (var routine in trackedGame.Routines)
                {
                    var oldState = routine.CurrentState;
                    var oldOwnership = routine.CompletedAutomaticallyByChecklist;
                    ReconcileChecklistDrivenState(trackedGame, routine, "startup checklist reconciliation");
                    changed |= oldState != routine.CurrentState ||
                               oldOwnership != routine.CompletedAutomaticallyByChecklist;
                }
                trackedGame.NotifyOverallStateChanged();
            }

            if (changed)
            {
                SavePluginSettings(settings.Settings);
            }
        }

        private void ApplyRoutineState(
            TrackedGameSettings trackedGame,
            RoutineSettings routine,
            TaskState newState,
            bool completedAutomatically,
            string reason)
        {
            var stateChanged = routine.CurrentState != newState;
            var ownershipChanged = routine.CompletedAutomaticallyByChecklist != completedAutomatically;
            routine.CurrentState = newState;
            routine.CompletedAutomaticallyByChecklist = completedAutomatically;
            trackedGame.NotifyOverallStateChanged();

            if (stateChanged || ownershipChanged)
            {
                var game = PlayniteApi.Database.Games.Get(trackedGame.GameId);
                logger.Info(
                    $"Routine state for {GetDisplayName(trackedGame, game)} / " +
                    $"{GetRoutineDisplayName(routine)} ({trackedGame.GameId}, {routine.Id}) is " +
                    $"{GetUserFacingStateName(newState)} after {reason}.");
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

            foreach (var trackedGame in affectedGames)
            {
                trackedGame.NotifyOverallStateChanged();
            }

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
                    logger.Error(exception, $"Failed to reconcile Game Routines tags for game {trackedGame.GameId}.");
                }
            }
        }

        private void UpdateTasksAvailableTag(Guid gameId, bool shouldHaveTag)
        {
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
                    (existing, requestedName) => string.Equals(existing.Name, requestedName, StringComparison.Ordinal));
            }

            var updatedTagIds = new List<Guid>(game.TagIds ?? new List<Guid>());
            var changed = false;
            if (legacyWeekliesTag != null)
            {
                changed |= updatedTagIds.RemoveAll(a => a == legacyWeekliesTag.Id) > 0;
            }
            if (legacyReadyTag != null)
            {
                changed |= updatedTagIds.RemoveAll(a => a == legacyReadyTag.Id) > 0;
            }

            if (tasksAvailableTag != null && shouldHaveTag && !updatedTagIds.Contains(tasksAvailableTag.Id))
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
            return string.IsNullOrWhiteSpace(trackedGame.CachedGameName) ? "Game" : trackedGame.CachedGameName;
        }

        private static string GetRoutineDisplayName(RoutineSettings routine)
        {
            return string.IsNullOrWhiteSpace(routine?.Name) ? "Routine" : routine.Name;
        }

        private static string GetUserFacingStateName(TaskState state)
        {
            return state == TaskState.COMPLETE ? "COMPLETE" : "INCOMPLETE";
        }

        private sealed class RoutineTarget
        {
            public RoutineTarget(TrackedGameSettings game, RoutineSettings routine)
            {
                Game = game;
                Routine = routine;
            }

            public TrackedGameSettings Game { get; }
            public RoutineSettings Routine { get; }
        }

        private sealed class AutomaticCompletionBlocker
        {
            public AutomaticCompletionBlocker(
                TrackedGameSettings game,
                RoutineSettings routine,
                TaskState checklistState)
            {
                Game = game;
                Routine = routine;
                ChecklistState = checklistState;
            }

            public TrackedGameSettings Game { get; }
            public RoutineSettings Routine { get; }
            public TaskState ChecklistState { get; }
        }
    }
}
