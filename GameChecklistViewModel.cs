using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;

namespace GameRoutines
{
    public sealed class GameChecklistViewModel : ObservableObject, IDisposable
    {
        private readonly GameRoutines plugin;
        private readonly Guid gameId;
        private readonly ObservableCollection<ChecklistItemSettings> emptyChecklist =
            new ObservableCollection<ChecklistItemSettings>();
        private TrackedGameSettings trackedGame;
        private ObservableCollection<ChecklistItemSettings> subscribedChecklist;
        private string gameName;
        private string currentState;
        private string progressText;

        public string GameName
        {
            get => gameName;
            private set => SetValue(ref gameName, value);
        }

        public ObservableCollection<ChecklistItemSettings> Items =>
            trackedGame?.Checklist ?? emptyChecklist;

        public bool IsTracked => trackedGame != null;

        public string CurrentState
        {
            get => currentState;
            private set => SetValue(ref currentState, value);
        }

        public string ProgressText
        {
            get => progressText;
            private set => SetValue(ref progressText, value);
        }

        public RelayCommand<ChecklistItemSettings> ToggleItemCommand { get; }

        public RelayCommand ResetChecklistCommand { get; }

        public RelayCommand OpenChecklistWindowCommand { get; }

        public RelayCommand OpenManageChecklistWindowCommand { get; }

        internal TrackedGameSettings TrackedGame => trackedGame;

        internal GameChecklistViewModel(GameRoutines plugin, Guid gameId)
        {
            this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            this.gameId = gameId;

            ToggleItemCommand = new RelayCommand<ChecklistItemSettings>(ToggleItem);
            ResetChecklistCommand = new RelayCommand(ResetChecklist, () => IsTracked);
            OpenChecklistWindowCommand = new RelayCommand(OpenChecklistWindow, () => IsTracked);
            OpenManageChecklistWindowCommand = new RelayCommand(OpenManageChecklistWindow, () => IsTracked);

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

            SubscribeToChecklist(null);
        }

        private void ToggleItem(ChecklistItemSettings item)
        {
            if (trackedGame == null || item == null || !Items.Contains(item))
            {
                return;
            }

            plugin.SetChecklistItemChecked(gameId, item.Id, item.IsChecked);
            RefreshStatus();
        }

        private void ResetChecklist()
        {
            plugin.ResetChecklist(gameId, true);
            RefreshStatus();
        }

        private void OpenChecklistWindow()
        {
            plugin.OpenChecklistWindow(gameId);
        }

        private void OpenManageChecklistWindow()
        {
            plugin.OpenManageChecklistWindow(gameId);
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
            if (!ReferenceEquals(trackedGame, latestTrackedGame))
            {
                if (trackedGame != null)
                {
                    trackedGame.PropertyChanged -= TrackedGame_PropertyChanged;
                }

                SubscribeToChecklist(null);
                trackedGame = latestTrackedGame;
                if (trackedGame != null)
                {
                    trackedGame.PropertyChanged += TrackedGame_PropertyChanged;
                    SubscribeToChecklist(trackedGame.Checklist);
                }

                OnPropertyChanged(nameof(Items));
                OnPropertyChanged(nameof(IsTracked));
                CommandManager.InvalidateRequerySuggested();
            }

            var game = plugin.PlayniteApi.Database.Games.Get(gameId);
            GameName = !string.IsNullOrWhiteSpace(game?.Name)
                ? game.Name
                : trackedGame?.CachedGameName ?? "Game";
            RefreshStatus();
        }

        private void TrackedGame_PropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (string.Equals(args.PropertyName, nameof(TrackedGameSettings.Checklist), StringComparison.Ordinal))
            {
                SubscribeToChecklist(trackedGame.Checklist);
                OnPropertyChanged(nameof(Items));
            }

            if (string.Equals(args.PropertyName, nameof(TrackedGameSettings.CurrentState), StringComparison.Ordinal) ||
                string.Equals(args.PropertyName, nameof(TrackedGameSettings.Checklist), StringComparison.Ordinal))
            {
                RefreshStatus();
            }
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

            RefreshStatus();
        }

        private void ChecklistItem_PropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (string.Equals(args.PropertyName, nameof(ChecklistItemSettings.IsChecked), StringComparison.Ordinal))
            {
                RefreshStatus();
            }
        }

        internal void RefreshStatus()
        {
            CurrentState = plugin.GetTrackedGameState(gameId) ?? string.Empty;
            var progress = plugin.GetChecklistProgress(gameId);
            ProgressText = $"{progress.Completed} / {progress.Total} completed";
        }
    }
}
