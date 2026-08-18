using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;

namespace GameRoutines
{
    internal sealed class GameTaskStateViewModel : ObservableObject, IDisposable
    {
        private readonly GameRoutines plugin;
        private readonly Guid gameId;
        private TrackedGameSettings trackedGame;
        private bool isTracked;
        private bool isIncomplete;
        private bool isIncompleteIndicatorVisible;
        private bool isAutomaticCompletionEnabled;
        private string stateText = string.Empty;
        private string toggleToolTip = string.Empty;
        private string automaticCompletionToolTip = string.Empty;

        public bool IsTracked
        {
            get => isTracked;
            private set => SetValue(ref isTracked, value);
        }

        public bool IsIncomplete
        {
            get => isIncomplete;
            private set => SetValue(ref isIncomplete, value);
        }

        public bool IsComplete => IsTracked && !IsIncomplete;

        public bool IsIncompleteIndicatorVisible
        {
            get => isIncompleteIndicatorVisible;
            private set => SetValue(ref isIncompleteIndicatorVisible, value);
        }

        public bool IsAutomaticCompletionEnabled
        {
            get => isAutomaticCompletionEnabled;
            private set => SetValue(ref isAutomaticCompletionEnabled, value);
        }

        public string StateText
        {
            get => stateText;
            private set => SetValue(ref stateText, value);
        }

        public string ToggleToolTip
        {
            get => toggleToolTip;
            private set => SetValue(ref toggleToolTip, value);
        }

        public string AutomaticCompletionToolTip
        {
            get => automaticCompletionToolTip;
            private set => SetValue(ref automaticCompletionToolTip, value);
        }

        public RelayCommand ToggleStateCommand { get; }

        public RelayCommand ToggleAutomaticCompletionCommand { get; }

        public GameTaskStateViewModel(GameRoutines plugin, Guid gameId)
        {
            this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            this.gameId = gameId;
            ToggleStateCommand = new RelayCommand(ToggleState, () => IsTracked);
            ToggleAutomaticCompletionCommand = new RelayCommand(
                ToggleAutomaticCompletion,
                () => IsTracked);
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
        }

        private void ToggleState()
        {
            if (trackedGame == null)
            {
                return;
            }

            if (trackedGame.AutomaticallyCompleteFromChecklist)
            {
                // Reset the switch before the modal opens; the shared plugin guard
                // remains authoritative for whether the state can change.
                OnPropertyChanged(nameof(IsComplete));
            }

            if (trackedGame.CurrentState == TaskState.COMPLETE)
            {
                plugin.MarkTrackedGameIncomplete(gameId);
            }
            else
            {
                plugin.MarkTrackedGameComplete(gameId);
            }

            // A ToggleButton updates its visual state before executing its command.
            // Reassert the authoritative value when a manual change is blocked.
            OnPropertyChanged(nameof(IsComplete));
        }

        private void ToggleAutomaticCompletion()
        {
            if (trackedGame == null)
            {
                return;
            }

            plugin.SetChecklistAutoCompletion(
                gameId,
                !trackedGame.AutomaticallyCompleteFromChecklist);
            OnPropertyChanged(nameof(IsAutomaticCompletionEnabled));
        }

        private void Plugin_UiStateChanged(object sender, GameRoutinesUiStateChangedEventArgs args)
        {
            if (args.Affects(gameId))
            {
                RebindTrackedGame();
            }
        }

        private void TrackedGame_PropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (string.Equals(args.PropertyName, nameof(TrackedGameSettings.CurrentState), StringComparison.Ordinal) ||
                string.Equals(
                    args.PropertyName,
                    nameof(TrackedGameSettings.AutomaticallyCompleteFromChecklist),
                    StringComparison.Ordinal) ||
                string.Equals(
                    args.PropertyName,
                    nameof(TrackedGameSettings.ShowIncompleteCoverIndicator),
                    StringComparison.Ordinal))
            {
                RefreshValues();
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

                trackedGame = latestTrackedGame;
                if (trackedGame != null)
                {
                    trackedGame.PropertyChanged += TrackedGame_PropertyChanged;
                }
            }

            RefreshValues();
        }

        private void RefreshValues()
        {
            IsTracked = trackedGame != null;
            IsIncomplete = trackedGame != null && trackedGame.CurrentState == TaskState.INCOMPLETE;
            IsIncompleteIndicatorVisible = plugin.ShouldShowIncompleteCoverIndicator(gameId);
            IsAutomaticCompletionEnabled =
                trackedGame?.AutomaticallyCompleteFromChecklist == true;
            OnPropertyChanged(nameof(IsComplete));
            StateText = trackedGame == null ? string.Empty : "TASKS";
            if (trackedGame == null)
            {
                ToggleToolTip = string.Empty;
                AutomaticCompletionToolTip = string.Empty;
            }
            else
            {
                var statusToolTip = IsIncomplete
                    ? "Task status: INCOMPLETE"
                    : "Task status: COMPLETE";
                ToggleToolTip = trackedGame.AutomaticallyCompleteFromChecklist
                    ? statusToolTip + Environment.NewLine +
                      "Automatic completion is enabled. Task status is controlled by the checklist."
                    : statusToolTip;
                AutomaticCompletionToolTip = IsAutomaticCompletionEnabled
                    ? "Automatic completion of tasks: ON"
                    : "Automatic completion of tasks: OFF";
            }

            CommandManager.InvalidateRequerySuggested();
        }
    }
}
