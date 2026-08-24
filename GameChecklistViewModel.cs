using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace GameRoutines
{
    public sealed class GameChecklistViewModel : ObservableObject, IDisposable
    {
        private readonly GameRoutines plugin;
        private readonly Guid gameId;
        private readonly Guid? focusedRoutineId;
        private TrackedGameSettings trackedGame;
        private ObservableCollection<RoutineSettings> subscribedRoutines;
        private string gameName;

        public string GameName
        {
            get => gameName;
            private set => SetValue(ref gameName, value);
        }

        public ObservableCollection<RoutineChecklistCardViewModel> RoutineCards { get; } =
            new ObservableCollection<RoutineChecklistCardViewModel>();

        public bool IsTracked => trackedGame != null;
        public bool HasRoutines => RoutineCards.Count > 0;
        public bool IsFocusedRoutineView => focusedRoutineId.HasValue;
        public bool ShowRoutinePopOutButtons => !IsFocusedRoutineView;

        public RelayCommand OpenAllChecklistsCommand { get; }
        public RelayCommand AddRoutineCommand { get; }

        internal TrackedGameSettings TrackedGame => trackedGame;

        internal GameChecklistViewModel(GameRoutines plugin, Guid gameId, Guid? focusedRoutineId = null)
        {
            this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            this.gameId = gameId;
            this.focusedRoutineId = focusedRoutineId;
            OpenAllChecklistsCommand = new RelayCommand(
                () => plugin.OpenChecklistWindow(gameId),
                () => IsTracked && !IsFocusedRoutineView);
            AddRoutineCommand = new RelayCommand(
                () => plugin.AddRoutine(trackedGame, true),
                () => IsTracked && !IsFocusedRoutineView);

            plugin.UiStateChanged += Plugin_UiStateChanged;
            RebindTrackedGame();
        }

        public void Dispose()
        {
            plugin.UiStateChanged -= Plugin_UiStateChanged;
            if (trackedGame != null)
            {
                trackedGame.PropertyChanged -= TrackedGame_PropertyChanged;
            }

            SubscribeToRoutines(null);
            ClearCards();
        }

        private void Plugin_UiStateChanged(object sender, GameRoutinesUiStateChangedEventArgs args)
        {
            if (args.Affects(gameId))
            {
                RebindTrackedGame();
            }
        }

        private void RebindTrackedGame()
        {
            var latestTrackedGame = plugin.GetTrackedGameSettings(gameId);
            var trackedGameChanged = !ReferenceEquals(trackedGame, latestTrackedGame);
            if (trackedGameChanged)
            {
                if (trackedGame != null)
                {
                    trackedGame.PropertyChanged -= TrackedGame_PropertyChanged;
                }

                SubscribeToRoutines(null);
                trackedGame = latestTrackedGame;
                if (trackedGame != null)
                {
                    trackedGame.PropertyChanged += TrackedGame_PropertyChanged;
                    SubscribeToRoutines(trackedGame.Routines);
                }

                OnPropertyChanged(nameof(IsTracked));
            }

            var game = plugin.PlayniteApi.Database.Games.Get(gameId);
            GameName = !string.IsNullOrWhiteSpace(game?.Name)
                ? game.Name
                : trackedGame?.CachedGameName ?? "Game";
            if (trackedGameChanged || RoutineCards.Count == 0)
            {
                RebuildCards();
            }
            CommandManager.InvalidateRequerySuggested();
        }

        private void TrackedGame_PropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (string.Equals(args.PropertyName, nameof(TrackedGameSettings.Routines), StringComparison.Ordinal))
            {
                SubscribeToRoutines(trackedGame.Routines);
                RebuildCards();
            }
        }

        private void SubscribeToRoutines(ObservableCollection<RoutineSettings> routines)
        {
            if (subscribedRoutines != null)
            {
                subscribedRoutines.CollectionChanged -= Routines_CollectionChanged;
            }

            subscribedRoutines = routines;
            if (subscribedRoutines != null)
            {
                subscribedRoutines.CollectionChanged += Routines_CollectionChanged;
            }
        }

        private void Routines_CollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            RebuildCards();
        }

        private void RebuildCards()
        {
            ClearCards();
            var routines = (trackedGame?.Routines ?? new ObservableCollection<RoutineSettings>())
                .Where(a => a != null && (!focusedRoutineId.HasValue || a.Id == focusedRoutineId.Value))
                .OrderBy(a => a.Order)
                .ToList();
            foreach (var routine in routines)
            {
                RoutineCards.Add(new RoutineChecklistCardViewModel(plugin, trackedGame, routine));
            }

            OnPropertyChanged(nameof(HasRoutines));
            OnPropertyChanged(nameof(ShowRoutinePopOutButtons));
            CommandManager.InvalidateRequerySuggested();
        }

        private void ClearCards()
        {
            foreach (var card in RoutineCards)
            {
                card.Dispose();
            }

            RoutineCards.Clear();
        }
    }

    public sealed class RoutineChecklistCardViewModel : ObservableObject, IDisposable
    {
        private readonly GameRoutines plugin;
        private readonly TrackedGameSettings trackedGame;
        private readonly RoutineSettings routine;
        private ObservableCollection<ChecklistItemSettings> subscribedChecklist;

        public Guid Id => routine.Id;
        public string Name => routine.Name;
        public string ResetCadenceText => CadenceDisplay.GetName(routine.ResetCadence);
        public string StateText => routine.DisplayState;
        public bool IsComplete => routine.IsComplete;
        public bool IsAutomaticCompletionEnabled => routine.AutomaticallyCompleteFromChecklist;
        public string AutomaticCompletionToolTip => routine.AutomaticallyCompleteFromChecklist
            ? "Automatic checklist completion: ON"
            : "Automatic checklist completion: OFF";
        public ObservableCollection<ChecklistItemSettings> Items => routine.Checklist;
        public bool HasItems => routine.Checklist?.Count > 0;
        public bool CanMoveUp => GetRoutineIndex() > 0;
        public bool CanMoveDown
        {
            get
            {
                var index = GetRoutineIndex();
                var count = trackedGame?.Routines?.Count(a => a != null) ?? 0;
                return index >= 0 && index < count - 1;
            }
        }

        public string ProgressText
        {
            get
            {
                var progress = ChecklistService.GetProgress(routine);
                return $"{progress.Completed} / {progress.Total}";
            }
        }

        public RelayCommand<ChecklistItemSettings> ToggleItemCommand { get; }
        public RelayCommand ToggleRoutineStateCommand { get; }
        public RelayCommand ToggleAutomaticCompletionCommand { get; }
        public RelayCommand ResetChecklistCommand { get; }
        public RelayCommand OpenManageChecklistWindowCommand { get; }
        public RelayCommand OpenRoutineChecklistWindowCommand { get; }
        public RelayCommand MoveRoutineUpCommand { get; }
        public RelayCommand MoveRoutineDownCommand { get; }
        public RelayCommand DeleteRoutineCommand { get; }

        internal RoutineChecklistCardViewModel(
            GameRoutines plugin,
            TrackedGameSettings trackedGame,
            RoutineSettings routine)
        {
            this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            this.trackedGame = trackedGame ?? throw new ArgumentNullException(nameof(trackedGame));
            this.routine = routine ?? throw new ArgumentNullException(nameof(routine));

            ToggleItemCommand = new RelayCommand<ChecklistItemSettings>(ToggleItem);
            ToggleRoutineStateCommand = new RelayCommand(ToggleRoutineState);
            ToggleAutomaticCompletionCommand = new RelayCommand(ToggleAutomaticCompletion);
            ResetChecklistCommand = new RelayCommand(() => plugin.ResetChecklist(trackedGame.GameId, routine.Id, true));
            OpenManageChecklistWindowCommand = new RelayCommand(
                () => plugin.OpenManageChecklistWindow(trackedGame.GameId, routine.Id));
            OpenRoutineChecklistWindowCommand = new RelayCommand(
                () => plugin.OpenChecklistWindow(trackedGame.GameId, routine.Id));
            MoveRoutineUpCommand = new RelayCommand(
                () => MoveRoutine(-1),
                () => CanMoveUp);
            MoveRoutineDownCommand = new RelayCommand(
                () => MoveRoutine(1),
                () => CanMoveDown);
            DeleteRoutineCommand = new RelayCommand(
                () => plugin.DeleteRoutine(trackedGame, routine.Id, true, true));

            routine.PropertyChanged += Routine_PropertyChanged;
            SubscribeToChecklist(routine.Checklist);
        }

        public void Dispose()
        {
            routine.PropertyChanged -= Routine_PropertyChanged;
            SubscribeToChecklist(null);
        }

        private void ToggleItem(ChecklistItemSettings item)
        {
            if (item != null && routine.Checklist.Contains(item))
            {
                plugin.SetChecklistItemChecked(trackedGame.GameId, routine.Id, item.Id, item.IsChecked);
            }
        }

        private void ToggleRoutineState()
        {
            var target = routine.CurrentState == TaskState.COMPLETE
                ? TaskState.INCOMPLETE
                : TaskState.COMPLETE;
            plugin.SetRoutineState(trackedGame.GameId, routine.Id, target);
            RefreshAll();
        }

        private void ToggleAutomaticCompletion()
        {
            plugin.SetChecklistAutoCompletion(
                trackedGame.GameId,
                routine.Id,
                !routine.AutomaticallyCompleteFromChecklist);
            RefreshAll();
        }

        private void MoveRoutine(int offset)
        {
            plugin.MoveRoutine(trackedGame, routine.Id, offset, true);
            RefreshAll();
        }

        private int GetRoutineIndex()
        {
            return trackedGame?.Routines?
                .Where(a => a != null)
                .OrderBy(a => a.Order)
                .ToList()
                .FindIndex(a => a.Id == routine.Id) ?? -1;
        }

        private void Routine_PropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (string.Equals(args.PropertyName, nameof(RoutineSettings.Checklist), StringComparison.Ordinal))
            {
                SubscribeToChecklist(routine.Checklist);
                OnPropertyChanged(nameof(Items));
            }

            RefreshAll();
        }

        private void SubscribeToChecklist(ObservableCollection<ChecklistItemSettings> checklist)
        {
            if (subscribedChecklist != null)
            {
                subscribedChecklist.CollectionChanged -= Checklist_CollectionChanged;
                foreach (var item in subscribedChecklist)
                {
                    if (item != null)
                    {
                        item.PropertyChanged -= ChecklistItem_PropertyChanged;
                    }
                }
            }

            subscribedChecklist = checklist;
            if (subscribedChecklist == null)
            {
                return;
            }

            subscribedChecklist.CollectionChanged += Checklist_CollectionChanged;
            foreach (var item in subscribedChecklist)
            {
                if (item != null)
                {
                    item.PropertyChanged += ChecklistItem_PropertyChanged;
                }
            }
        }

        private void Checklist_CollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if (args.OldItems != null)
            {
                foreach (ChecklistItemSettings item in args.OldItems)
                {
                    if (item != null)
                    {
                        item.PropertyChanged -= ChecklistItem_PropertyChanged;
                    }
                }
            }

            if (args.NewItems != null)
            {
                foreach (ChecklistItemSettings item in args.NewItems)
                {
                    if (item != null)
                    {
                        item.PropertyChanged += ChecklistItem_PropertyChanged;
                    }
                }
            }

            RefreshAll();
        }

        private void ChecklistItem_PropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (string.Equals(args.PropertyName, nameof(ChecklistItemSettings.IsChecked), StringComparison.Ordinal))
            {
                OnPropertyChanged(nameof(ProgressText));
            }
        }

        private void RefreshAll()
        {
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(ResetCadenceText));
            OnPropertyChanged(nameof(StateText));
            OnPropertyChanged(nameof(IsComplete));
            OnPropertyChanged(nameof(IsAutomaticCompletionEnabled));
            OnPropertyChanged(nameof(AutomaticCompletionToolTip));
            OnPropertyChanged(nameof(Items));
            OnPropertyChanged(nameof(HasItems));
            OnPropertyChanged(nameof(ProgressText));
            OnPropertyChanged(nameof(CanMoveUp));
            OnPropertyChanged(nameof(CanMoveDown));
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
