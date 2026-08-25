using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
        private bool isAutomaticCompletionMixed;
        private int participatingRoutineCount;
        private string stateText = string.Empty;
        private string toggleToolTip = string.Empty;
        private string automaticCompletionToolTip = string.Empty;
        private IReadOnlyList<RoutineSettings> participatingRoutines =
            new List<RoutineSettings>();

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

        public bool IsAutomaticCompletionMixed
        {
            get => isAutomaticCompletionMixed;
            private set => SetValue(ref isAutomaticCompletionMixed, value);
        }

        public int ParticipatingRoutineCount
        {
            get => participatingRoutineCount;
            private set => SetValue(ref participatingRoutineCount, value);
        }

        public bool HasParticipatingRoutines => ParticipatingRoutineCount > 0;
        public bool HasMultipleParticipatingRoutines => ParticipatingRoutineCount > 1;

        public IReadOnlyList<RoutineSettings> ParticipatingRoutines
        {
            get => participatingRoutines;
            private set => SetValue(ref participatingRoutines, value);
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
        public RelayCommand<RoutineSettings> ToggleRoutineAutomaticCompletionCommand { get; }
        public RelayCommand OpenSettingsCommand { get; }
        public RelayCommand OpenCustomReminderCommand { get; }

        public GameTaskStateViewModel(GameRoutines plugin, Guid gameId)
        {
            this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            this.gameId = gameId;
            ToggleStateCommand = new RelayCommand(ToggleState, () => IsTracked && HasParticipatingRoutines);
            ToggleAutomaticCompletionCommand = new RelayCommand(
                ToggleAutomaticCompletion,
                () => IsTracked && ParticipatingRoutineCount == 1);
            ToggleRoutineAutomaticCompletionCommand =
                new RelayCommand<RoutineSettings>(ToggleRoutineAutomaticCompletion);
            OpenSettingsCommand = new RelayCommand(
                () => plugin.OpenSettingsForGame(gameId),
                () => IsTracked);
            OpenCustomReminderCommand = new RelayCommand(
                () => plugin.OpenCustomReminderWindow(gameId),
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
            if (trackedGame == null || !HasParticipatingRoutines)
            {
                return;
            }

            if (trackedGame.CurrentState == TaskState.COMPLETE)
            {
                plugin.MarkTrackedGameIncomplete(gameId);
            }
            else
            {
                plugin.MarkTrackedGameComplete(gameId);
            }

            OnPropertyChanged(nameof(IsComplete));
        }

        private void ToggleAutomaticCompletion()
        {
            var routine = ParticipatingRoutines.SingleOrDefault();
            if (routine != null)
            {
                plugin.SetChecklistAutoCompletion(
                    gameId,
                    routine.Id,
                    !routine.AutomaticallyCompleteFromChecklist);
            }
        }

        private void ToggleRoutineAutomaticCompletion(RoutineSettings routine)
        {
            if (routine != null && ParticipatingRoutines.Any(a => a.Id == routine.Id))
            {
                plugin.SetChecklistAutoCompletion(
                    gameId,
                    routine.Id,
                    !routine.AutomaticallyCompleteFromChecklist);
            }
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
                string.Equals(args.PropertyName, nameof(TrackedGameSettings.Routines), StringComparison.Ordinal) ||
                string.Equals(args.PropertyName, nameof(TrackedGameSettings.ShowIncompleteCoverIndicator), StringComparison.Ordinal) ||
                string.Equals(args.PropertyName, nameof(TrackedGameSettings.Enabled), StringComparison.Ordinal))
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

            var participating = RoutineService.GetParticipatingRoutines(trackedGame);
            ParticipatingRoutines = participating;
            ParticipatingRoutineCount = participating.Count;
            var enabledCount = participating.Count(a => a.AutomaticallyCompleteFromChecklist);
            IsAutomaticCompletionEnabled = participating.Count > 0 && enabledCount == participating.Count;
            IsAutomaticCompletionMixed = enabledCount > 0 && enabledCount < participating.Count;

            OnPropertyChanged(nameof(IsComplete));
            OnPropertyChanged(nameof(HasParticipatingRoutines));
            OnPropertyChanged(nameof(HasMultipleParticipatingRoutines));
            StateText = trackedGame == null ? string.Empty : "TASKS";
            if (trackedGame == null)
            {
                ToggleToolTip = string.Empty;
                AutomaticCompletionToolTip = string.Empty;
            }
            else if (!HasParticipatingRoutines)
            {
                ToggleToolTip = "No routines are included in overall task status.";
                AutomaticCompletionToolTip = "No routines are included in overall task status.";
            }
            else
            {
                ToggleToolTip = IsIncomplete
                    ? "Overall task status: INCOMPLETE"
                    : "Overall task status: COMPLETE";
                if (ParticipatingRoutineCount == 1)
                {
                    AutomaticCompletionToolTip = IsAutomaticCompletionEnabled
                        ? $"Automatic completion for {participating[0].Name}: ON"
                        : $"Automatic completion for {participating[0].Name}: OFF";
                }
                else if (IsAutomaticCompletionMixed)
                {
                    AutomaticCompletionToolTip = "Automatic completion: MIXED. Click to manage each counted routine.";
                }
                else
                {
                    AutomaticCompletionToolTip = IsAutomaticCompletionEnabled
                        ? "Automatic completion: ON for all counted routines. Click to manage each routine."
                        : "Automatic completion: OFF for all counted routines. Click to manage each routine.";
                }
            }

            CommandManager.InvalidateRequerySuggested();
        }
    }
}
