using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;

namespace WeeklyManager
{
    internal sealed class ManageChecklistViewModel : ObservableObject, IDisposable
    {
        private readonly WeeklyManager plugin;
        private readonly Guid gameId;
        private readonly ObservableCollection<ChecklistItemSettings> emptyChecklist =
            new ObservableCollection<ChecklistItemSettings>();
        private TrackedGameSettings trackedGame;
        private ObservableCollection<ChecklistItemSettings> subscribedChecklist;
        private string gameName;
        private string newItemText = string.Empty;

        public string GameName
        {
            get => gameName;
            private set => SetValue(ref gameName, value);
        }

        public ObservableCollection<ChecklistItemSettings> Items =>
            trackedGame?.Checklist ?? emptyChecklist;

        public bool IsTracked => trackedGame != null;

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

        internal ManageChecklistViewModel(WeeklyManager plugin, Guid gameId)
        {
            this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            this.gameId = gameId;

            AddItemCommand = new RelayCommand(
                AddItem,
                () => IsTracked && !string.IsNullOrWhiteSpace(NewItemText));
            DeleteItemCommand = new RelayCommand<ChecklistItemSettings>(DeleteItem);
            MoveItemUpCommand = new RelayCommand<ChecklistItemSettings>(item => MoveItem(item, -1));
            MoveItemDownCommand = new RelayCommand<ChecklistItemSettings>(item => MoveItem(item, 1));

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

        internal bool CommitItemText(ChecklistItemSettings item, string text)
        {
            return item != null && Items.Contains(item) &&
                plugin.EditChecklistItem(gameId, item.Id, text);
        }

        private void AddItem()
        {
            if (plugin.AddChecklistItem(gameId, NewItemText))
            {
                NewItemText = string.Empty;
            }
        }

        private void DeleteItem(ChecklistItemSettings item)
        {
            if (item != null && Items.Contains(item))
            {
                plugin.DeleteChecklistItem(gameId, item.Id);
            }
        }

        private void MoveItem(ChecklistItemSettings item, int offset)
        {
            if (item != null && Items.Contains(item))
            {
                plugin.MoveChecklistItem(gameId, item.Id, offset);
            }
        }

        private void Plugin_UiStateChanged(object sender, WeeklyManagerUiStateChangedEventArgs args)
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
            }

            var game = plugin.PlayniteApi.Database.Games.Get(gameId);
            GameName = !string.IsNullOrWhiteSpace(game?.Name)
                ? game.Name
                : trackedGame?.CachedGameName ?? "Game";
            CommandManager.InvalidateRequerySuggested();
        }

        private void TrackedGame_PropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (string.Equals(args.PropertyName, nameof(TrackedGameSettings.Checklist), StringComparison.Ordinal))
            {
                SubscribeToChecklist(trackedGame.Checklist);
                OnPropertyChanged(nameof(Items));
                CommandManager.InvalidateRequerySuggested();
            }
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
