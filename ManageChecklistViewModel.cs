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
    internal sealed class ManageChecklistViewModel : ObservableObject, IDisposable
    {
        private readonly GameRoutines plugin;
        private readonly Guid gameId;
        private readonly ObservableCollection<RoutineSettings> emptyRoutines =
            new ObservableCollection<RoutineSettings>();
        private readonly ObservableCollection<ChecklistItemSettings> emptyChecklist =
            new ObservableCollection<ChecklistItemSettings>();
        private TrackedGameSettings trackedGame;
        private RoutineSettings selectedRoutine;
        private ObservableCollection<RoutineSettings> subscribedRoutines;
        private ObservableCollection<ChecklistItemSettings> subscribedChecklist;
        private string gameName;
        private string newItemText = string.Empty;
        private ResetCadence baselineResetCadence;
        private DayOfWeek baselineResetDay;
        private string baselineResetTime;
        private DateTime? baselineBiWeeklyResetAnchorLocal;

        public string GameName
        {
            get => gameName;
            private set => SetValue(ref gameName, value);
        }

        public ObservableCollection<RoutineSettings> Routines =>
            trackedGame?.Routines ?? emptyRoutines;

        public RoutineSettings SelectedRoutine
        {
            get => selectedRoutine;
            set
            {
                var normalized = value != null && Routines.Contains(value) ? value : null;
                if (ReferenceEquals(selectedRoutine, normalized))
                {
                    return;
                }

                SubscribeToChecklist(null);
                selectedRoutine = normalized;
                CaptureScheduleBaseline();
                SubscribeToChecklist(selectedRoutine?.Checklist);
                OnPropertyChanged();
                OnPropertyChanged(nameof(CountTowardOverallTaskStatus));
                OnPropertyChanged(nameof(Items));
                NewItemText = string.Empty;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public ObservableCollection<ChecklistItemSettings> Items =>
            SelectedRoutine?.Checklist ?? emptyChecklist;

        public bool IsTracked => trackedGame != null;
        public bool HasRoutines => Routines.Count > 0;

        public bool CountTowardOverallTaskStatus
        {
            get => SelectedRoutine?.CountTowardOverallTaskStatus == true;
            set
            {
                if (SelectedRoutine == null ||
                    SelectedRoutine.CountTowardOverallTaskStatus == value)
                {
                    return;
                }

                plugin.SetRoutineCountTowardOverallTaskStatus(
                    gameId,
                    SelectedRoutine.Id,
                    value);
                OnPropertyChanged();
            }
        }

        public string NewItemText
        {
            get => newItemText;
            set
            {
                var boundedValue = value ?? string.Empty;
                if (boundedValue.Length > ChecklistItemSettings.MaximumTextLength)
                {
                    boundedValue = boundedValue.Substring(0, ChecklistItemSettings.MaximumTextLength);
                }

                if (string.Equals(newItemText, boundedValue, StringComparison.Ordinal))
                {
                    return;
                }

                newItemText = boundedValue;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public RelayCommand AddItemCommand { get; }
        public RelayCommand<ChecklistItemSettings> DeleteItemCommand { get; }
        public RelayCommand<ChecklistItemSettings> MoveItemUpCommand { get; }
        public RelayCommand<ChecklistItemSettings> MoveItemDownCommand { get; }

        internal ManageChecklistViewModel(GameRoutines plugin, Guid gameId, Guid? routineId = null)
        {
            this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            this.gameId = gameId;

            AddItemCommand = new RelayCommand(
                AddItem,
                () => SelectedRoutine != null && !string.IsNullOrWhiteSpace(NewItemText));
            DeleteItemCommand = new RelayCommand<ChecklistItemSettings>(DeleteItem);
            MoveItemUpCommand = new RelayCommand<ChecklistItemSettings>(item => MoveItem(item, -1));
            MoveItemDownCommand = new RelayCommand<ChecklistItemSettings>(item => MoveItem(item, 1));

            plugin.UiStateChanged += Plugin_UiStateChanged;
            RebindTrackedGame(routineId);
        }

        public void Dispose()
        {
            plugin.UiStateChanged -= Plugin_UiStateChanged;
            if (trackedGame != null)
            {
                trackedGame.PropertyChanged -= TrackedGame_PropertyChanged;
            }
            SubscribeToRoutines(null);
            SubscribeToChecklist(null);
        }

        internal void SelectRoutine(Guid routineId)
        {
            SelectedRoutine = Routines.FirstOrDefault(a => a != null && a.Id == routineId);
        }

        internal bool CommitItemText(ChecklistItemSettings item, string text)
        {
            return SelectedRoutine != null && item != null && Items.Contains(item) &&
                plugin.EditChecklistItem(gameId, SelectedRoutine.Id, item.Id, text);
        }

        internal void CommitScheduleChanges()
        {
            if (SelectedRoutine != null && HasScheduleChanged())
            {
                plugin.CommitRoutineScheduleChange(gameId, SelectedRoutine.Id);
                CaptureScheduleBaseline();
            }
        }

        private bool HasScheduleChanged()
        {
            return SelectedRoutine != null &&
                (baselineResetCadence != SelectedRoutine.ResetCadence ||
                 baselineResetDay != SelectedRoutine.ResetDay ||
                 !string.Equals(baselineResetTime, SelectedRoutine.ResetTime, StringComparison.Ordinal) ||
                 baselineBiWeeklyResetAnchorLocal != SelectedRoutine.BiWeeklyResetAnchorLocal);
        }

        private void CaptureScheduleBaseline()
        {
            baselineResetCadence = SelectedRoutine?.ResetCadence ?? ResetCadence.Never;
            baselineResetDay = SelectedRoutine?.ResetDay ?? DayOfWeek.Monday;
            baselineResetTime = SelectedRoutine?.ResetTime;
            baselineBiWeeklyResetAnchorLocal = SelectedRoutine?.BiWeeklyResetAnchorLocal;
        }

        private void AddItem()
        {
            if (SelectedRoutine != null &&
                plugin.AddChecklistItem(gameId, SelectedRoutine.Id, NewItemText))
            {
                NewItemText = string.Empty;
            }
        }

        private void DeleteItem(ChecklistItemSettings item)
        {
            if (SelectedRoutine != null && item != null && Items.Contains(item))
            {
                plugin.DeleteChecklistItem(gameId, SelectedRoutine.Id, item.Id);
            }
        }

        private void MoveItem(ChecklistItemSettings item, int offset)
        {
            if (SelectedRoutine != null && item != null && Items.Contains(item))
            {
                plugin.MoveChecklistItem(gameId, SelectedRoutine.Id, item.Id, offset);
            }
        }

        private void Plugin_UiStateChanged(object sender, GameRoutinesUiStateChangedEventArgs args)
        {
            if (args.Affects(gameId))
            {
                RebindTrackedGame(SelectedRoutine?.Id);
            }
        }

        private void RebindTrackedGame(Guid? preferredRoutineId)
        {
            var latestTrackedGame = plugin.GetTrackedGameSettings(gameId);
            if (!ReferenceEquals(trackedGame, latestTrackedGame))
            {
                if (trackedGame != null)
                {
                    trackedGame.PropertyChanged -= TrackedGame_PropertyChanged;
                }
                SubscribeToRoutines(null);
                SubscribeToChecklist(null);
                trackedGame = latestTrackedGame;
                if (trackedGame != null)
                {
                    trackedGame.PropertyChanged += TrackedGame_PropertyChanged;
                    SubscribeToRoutines(trackedGame.Routines);
                }
                OnPropertyChanged(nameof(Routines));
                OnPropertyChanged(nameof(IsTracked));
            }

            var selectedId = preferredRoutineId ?? selectedRoutine?.Id;
            SelectedRoutine = selectedId.HasValue
                ? Routines.FirstOrDefault(a => a != null && a.Id == selectedId.Value) ??
                  Routines.OrderBy(a => a.Order).FirstOrDefault()
                : Routines.OrderBy(a => a.Order).FirstOrDefault();

            var game = plugin.PlayniteApi.Database.Games.Get(gameId);
            GameName = !string.IsNullOrWhiteSpace(game?.Name)
                ? game.Name
                : trackedGame?.CachedGameName ?? "Game";
            OnPropertyChanged(nameof(HasRoutines));
            OnPropertyChanged(nameof(CountTowardOverallTaskStatus));
            CommandManager.InvalidateRequerySuggested();
        }

        private void TrackedGame_PropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (string.Equals(args.PropertyName, nameof(TrackedGameSettings.Routines), StringComparison.Ordinal))
            {
                SubscribeToRoutines(trackedGame.Routines);
                RebindTrackedGame(SelectedRoutine?.Id);
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
            OnPropertyChanged(nameof(Routines));
            OnPropertyChanged(nameof(HasRoutines));
            var selectedId = SelectedRoutine?.Id;
            SelectedRoutine = selectedId.HasValue
                ? Routines.FirstOrDefault(a => a != null && a.Id == selectedId.Value) ??
                  Routines.OrderBy(a => a.Order).FirstOrDefault()
                : Routines.OrderBy(a => a.Order).FirstOrDefault();
            OnPropertyChanged(nameof(CountTowardOverallTaskStatus));
            CommandManager.InvalidateRequerySuggested();
        }

        private void SubscribeToChecklist(ObservableCollection<ChecklistItemSettings> checklist)
        {
            if (subscribedChecklist != null)
            {
                subscribedChecklist.CollectionChanged -= Checklist_CollectionChanged;
            }
            subscribedChecklist = checklist;
            if (subscribedChecklist != null)
            {
                subscribedChecklist.CollectionChanged += Checklist_CollectionChanged;
            }
        }

        private void Checklist_CollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
