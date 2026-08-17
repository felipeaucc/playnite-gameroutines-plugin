using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace WeeklyManager
{
    public sealed class WeeklyChecklistViewModel : ObservableObject, IDisposable
    {
        private readonly WeeklyManager plugin;
        private readonly Guid gameId;
        private readonly TrackedGameSettings trackedGame;
        private ObservableCollection<ChecklistItemSettings> subscribedChecklist;
        private string currentState;
        private string progressText;

        public string GameName { get; }

        public ObservableCollection<ChecklistItemSettings> Items => trackedGame.Checklist;

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

        internal WeeklyChecklistViewModel(WeeklyManager plugin, Guid gameId)
        {
            this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            this.gameId = gameId;
            trackedGame = plugin.GetTrackedGameSettings(gameId) ??
                throw new ArgumentException("The game is not tracked by Weekly Manager.", nameof(gameId));

            var game = plugin.PlayniteApi.Database.Games.Get(gameId);
            GameName = !string.IsNullOrWhiteSpace(game?.Name)
                ? game.Name
                : trackedGame.CachedGameName ?? "Game";

            ToggleItemCommand = new RelayCommand<ChecklistItemSettings>(ToggleItem);
            ResetChecklistCommand = new RelayCommand(ResetChecklist);

            trackedGame.PropertyChanged += TrackedGame_PropertyChanged;
            SubscribeToChecklist(trackedGame.Checklist);
            RefreshStatus();
        }

        public void Dispose()
        {
            trackedGame.PropertyChanged -= TrackedGame_PropertyChanged;
            SubscribeToChecklist(null);
        }

        private void ToggleItem(ChecklistItemSettings item)
        {
            if (item == null || !Items.Contains(item))
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

        private void RefreshStatus()
        {
            CurrentState = plugin.GetTrackedGameState(gameId) ?? "INCOMPLETE";
            var progress = plugin.GetChecklistProgress(gameId);
            ProgressText = $"{progress.Completed} / {progress.Total} completed";
        }
    }
}
