using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;

namespace WeeklyManager
{
    public enum WeeklyState
    {
        READY,
        COMPLETE
    }

    public class TrackedGameSettings : ObservableObject
    {
        public const int MaximumSecondaryNotificationTitleLength = 40;
        public const int MaximumSecondaryNotificationMessageLength = 160;

        private Guid gameId;
        private string cachedGameName;
        private bool enabled = true;
        private DayOfWeek weeklyResetDay = DayOfWeek.Monday;
        private string weeklyResetTime = "00:00";
        private WeeklyState currentState = WeeklyState.READY;
        private DateTime? lastResetProcessedLocal;
        private bool secondaryReminderEnabled;
        private DayOfWeek secondaryReminderDay = DayOfWeek.Monday;
        private string secondaryReminderTime = "00:00";
        private string secondaryNotificationTitle = "Weekly Reminder";
        private string secondaryNotificationMessage = "A weekly activity may be available now.";
        private DateTime? lastSecondaryReminderProcessedLocal;

        public Guid GameId
        {
            get => gameId;
            set => SetValue(ref gameId, value);
        }

        public string CachedGameName
        {
            get => cachedGameName;
            set => SetValue(ref cachedGameName, value);
        }

        public bool Enabled
        {
            get => enabled;
            set => SetValue(ref enabled, value);
        }

        public DayOfWeek WeeklyResetDay
        {
            get => weeklyResetDay;
            set => SetValue(ref weeklyResetDay, value);
        }

        public string WeeklyResetTime
        {
            get => weeklyResetTime;
            set
            {
                var normalizedValue = NormalizeTime(value);
                if (string.Equals(weeklyResetTime, normalizedValue, StringComparison.Ordinal))
                {
                    return;
                }

                weeklyResetTime = normalizedValue;
                OnPropertyChanged();
            }
        }

        public WeeklyState CurrentState
        {
            get => currentState;
            set
            {
                if (currentState == value)
                {
                    return;
                }

                currentState = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayState));
            }
        }

        [DontSerialize]
        public string DisplayState => CurrentState == WeeklyState.COMPLETE ? "COMPLETE" : "INCOMPLETE";

        public DateTime? LastResetProcessedLocal
        {
            get => lastResetProcessedLocal;
            set => SetValue(ref lastResetProcessedLocal, value);
        }

        public bool SecondaryReminderEnabled
        {
            get => secondaryReminderEnabled;
            set => SetValue(ref secondaryReminderEnabled, value);
        }

        public DayOfWeek SecondaryReminderDay
        {
            get => secondaryReminderDay;
            set => SetValue(ref secondaryReminderDay, value);
        }

        public string SecondaryReminderTime
        {
            get => secondaryReminderTime;
            set
            {
                var normalizedValue = NormalizeTime(value);
                if (string.Equals(secondaryReminderTime, normalizedValue, StringComparison.Ordinal))
                {
                    return;
                }

                secondaryReminderTime = normalizedValue;
                OnPropertyChanged();
            }
        }

        public string SecondaryNotificationTitle
        {
            get => secondaryNotificationTitle;
            set
            {
                var boundedValue = value != null && value.Length > MaximumSecondaryNotificationTitleLength
                    ? value.Substring(0, MaximumSecondaryNotificationTitleLength)
                    : value;
                SetValue(ref secondaryNotificationTitle, boundedValue);
            }
        }

        public string SecondaryNotificationMessage
        {
            get => secondaryNotificationMessage;
            set
            {
                var boundedValue = value != null && value.Length > MaximumSecondaryNotificationMessageLength
                    ? value.Substring(0, MaximumSecondaryNotificationMessageLength)
                    : value;
                SetValue(ref secondaryNotificationMessage, boundedValue);
            }
        }

        public DateTime? LastSecondaryReminderProcessedLocal
        {
            get => lastSecondaryReminderProcessedLocal;
            set => SetValue(ref lastSecondaryReminderProcessedLocal, value);
        }

        private static string NormalizeTime(string value)
        {
            return WeeklyScheduleCalculator.TryNormalizeTimeInput(value, out var normalizedValue)
                ? normalizedValue
                : "00:00";
        }
    }

    public class WeeklyManagerSettings : ObservableObject
    {
        private bool useReadyTag;
        private ObservableCollection<TrackedGameSettings> trackedGames =
            new ObservableCollection<TrackedGameSettings>();

        public bool UseReadyTag
        {
            get => useReadyTag;
            set => SetValue(ref useReadyTag, value);
        }

        public ObservableCollection<TrackedGameSettings> TrackedGames
        {
            get => trackedGames;
            set => SetValue(ref trackedGames, value ?? new ObservableCollection<TrackedGameSettings>());
        }
    }

    public class LibraryGameOption
    {
        public Guid Id { get; set; }

        public string Name { get; set; }
    }

    public class WeeklyManagerSettingsViewModel : ObservableObject, ISettings
    {
        private const int MaximumSearchResults = 50;
        private readonly WeeklyManager plugin;
        private readonly List<LibraryGameOption> availableGames = new List<LibraryGameOption>();
        private WeeklyManagerSettings editingClone;
        private WeeklyManagerSettings settings;
        private LibraryGameOption selectedLibraryGame;
        private LibraryGameOption selectedSearchResult;
        private TrackedGameSettings selectedTrackedGame;
        private string gameSearchText = string.Empty;
        private bool isGameSearchOpen;
        private bool isApplyingSearchSelection;

        public WeeklyManagerSettings Settings
        {
            get => settings;
            set
            {
                settings = value ?? new WeeklyManagerSettings();
                EnsureCollections();
                OnPropertyChanged();
                OnPropertyChanged(nameof(TrackedGames));
            }
        }

        public ObservableCollection<TrackedGameSettings> TrackedGames => Settings.TrackedGames;

        public ObservableCollection<LibraryGameOption> GameSearchResults { get; } =
            new ObservableCollection<LibraryGameOption>();

        public IReadOnlyList<DayOfWeek> DaysOfWeek { get; } =
            Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().ToList();

        public LibraryGameOption SelectedLibraryGame
        {
            get => selectedLibraryGame;
            set
            {
                if (ReferenceEquals(selectedLibraryGame, value))
                {
                    return;
                }

                selectedLibraryGame = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanAddSelectedGame));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public LibraryGameOption SelectedSearchResult
        {
            get => selectedSearchResult;
            set => SetValue(ref selectedSearchResult, value);
        }

        public string GameSearchText
        {
            get => gameSearchText;
            set
            {
                var normalizedValue = value ?? string.Empty;
                if (string.Equals(gameSearchText, normalizedValue, StringComparison.Ordinal))
                {
                    return;
                }

                gameSearchText = normalizedValue;
                OnPropertyChanged();

                if (!isApplyingSearchSelection &&
                    SelectedLibraryGame != null &&
                    !string.Equals(SelectedLibraryGame.Name, gameSearchText, StringComparison.CurrentCulture))
                {
                    SelectedLibraryGame = null;
                }

                UpdateGameSearchResults();
            }
        }

        public bool IsGameSearchOpen
        {
            get => isGameSearchOpen;
            set => SetValue(ref isGameSearchOpen, value);
        }

        public bool CanAddSelectedGame =>
            SelectedLibraryGame != null &&
            availableGames.Any(a => a.Id == SelectedLibraryGame.Id) &&
            !TrackedGames.Any(a => a.GameId == SelectedLibraryGame.Id);

        public TrackedGameSettings SelectedTrackedGame
        {
            get => selectedTrackedGame;
            set => SetValue(ref selectedTrackedGame, value);
        }

        public RelayCommand AddGameCommand { get; }

        public RelayCommand RemoveGameCommand { get; }

        public WeeklyManagerSettingsViewModel(WeeklyManager plugin)
        {
            this.plugin = plugin;
            Settings = plugin.LoadPluginSettings<WeeklyManagerSettings>() ?? new WeeklyManagerSettings();
            AddGameCommand = new RelayCommand(AddSelectedGame, () => CanAddSelectedGame);
            RemoveGameCommand = new RelayCommand(RemoveSelectedGame, () => SelectedTrackedGame != null);
        }

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(Settings);
            plugin.SetSettingsEditing(true);
            RefreshLibraryGames();
        }

        public void CancelEdit()
        {
            Settings = editingClone ?? new WeeklyManagerSettings();
            editingClone = null;
            SelectedTrackedGame = null;
            plugin.SetSettingsEditing(false);
            RefreshLibraryGames();
        }

        public void EndEdit()
        {
            NormalizePersistedValues();
            plugin.PrepareSettingsForSave(editingClone, Settings);
            plugin.SavePluginSettings(Settings);
            plugin.ApplySettingsChanges(editingClone, Settings);
            editingClone = null;
            plugin.SetSettingsEditing(false);
            RefreshLibraryGames();
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            var duplicateIds = TrackedGames.GroupBy(a => a.GameId).Where(a => a.Count() > 1).Select(a => a.Key);
            foreach (var duplicateId in duplicateIds)
            {
                errors.Add($"Game {duplicateId} is configured more than once.");
            }

            foreach (var trackedGame in TrackedGames)
            {
                var gameName = string.IsNullOrWhiteSpace(trackedGame.CachedGameName)
                    ? trackedGame.GameId.ToString()
                    : trackedGame.CachedGameName;

                if (trackedGame.GameId == Guid.Empty)
                {
                    errors.Add($"{gameName} has an invalid Playnite game ID.");
                }

                if (!WeeklyScheduleCalculator.TryNormalizeTimeInput(
                        trackedGame.WeeklyResetTime, out var normalizedResetTime) ||
                    !string.Equals(
                        trackedGame.WeeklyResetTime, normalizedResetTime, StringComparison.Ordinal))
                {
                    errors.Add($"{gameName}: reset time must use 24-hour HH:mm format.");
                }

                if (trackedGame.SecondaryNotificationTitle != null &&
                    trackedGame.SecondaryNotificationTitle.Length >
                    TrackedGameSettings.MaximumSecondaryNotificationTitleLength)
                {
                    errors.Add($"{gameName}: reminder title cannot exceed 40 characters.");
                }

                if (trackedGame.SecondaryNotificationMessage != null &&
                    trackedGame.SecondaryNotificationMessage.Length >
                    TrackedGameSettings.MaximumSecondaryNotificationMessageLength)
                {
                    errors.Add($"{gameName}: reminder message cannot exceed 160 characters.");
                }

                if (trackedGame.SecondaryReminderEnabled)
                {
                    if (!WeeklyScheduleCalculator.TryNormalizeTimeInput(
                            trackedGame.SecondaryReminderTime, out var normalizedReminderTime) ||
                        !string.Equals(
                            trackedGame.SecondaryReminderTime,
                            normalizedReminderTime,
                            StringComparison.Ordinal))
                    {
                        errors.Add($"{gameName}: reminder time must use 24-hour HH:mm format.");
                    }

                    if (string.IsNullOrWhiteSpace(trackedGame.SecondaryNotificationTitle))
                    {
                        errors.Add($"{gameName}: reminder title cannot be empty when the reminder is enabled.");
                    }

                    if (string.IsNullOrWhiteSpace(trackedGame.SecondaryNotificationMessage))
                    {
                        errors.Add($"{gameName}: reminder message cannot be empty when the reminder is enabled.");
                    }
                }
            }

            return errors.Count == 0;
        }

        public void RefreshLibraryGames()
        {
            availableGames.Clear();
            ClearGameSearch();

            try
            {
                if (!plugin.PlayniteApi.Database.IsOpen)
                {
                    return;
                }

                var trackedIds = new HashSet<Guid>(TrackedGames.Select(a => a.GameId));
                var games = plugin.PlayniteApi.Database.Games
                    .Where(a => !trackedIds.Contains(a.Id))
                    .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Select(a => new LibraryGameOption { Id = a.Id, Name = a.Name });

                foreach (var game in games)
                {
                    availableGames.Add(game);
                }

                UpdateGameSearchResults();
            }
            catch (Exception exception)
            {
                plugin.LogException(exception, "Failed to load Playnite games for the settings view.");
            }
        }

        public void OpenGameSearch()
        {
            UpdateGameSearchResults();
            if (SelectedSearchResult == null)
            {
                SelectedSearchResult = GameSearchResults.FirstOrDefault();
            }

            IsGameSearchOpen = GameSearchResults.Count > 0;
        }

        public void CloseGameSearch()
        {
            IsGameSearchOpen = false;
        }

        public void MoveGameSearchSelection(int offset)
        {
            if (GameSearchResults.Count == 0)
            {
                SelectedSearchResult = null;
                return;
            }

            var currentIndex = SelectedSearchResult == null
                ? -1
                : GameSearchResults.IndexOf(SelectedSearchResult);
            var nextIndex = Math.Max(0, Math.Min(GameSearchResults.Count - 1, currentIndex + offset));
            SelectedSearchResult = GameSearchResults[nextIndex];
        }

        public bool ConfirmGameSearchSelection()
        {
            var result = SelectedSearchResult;
            if (result == null ||
                !availableGames.Any(a => a.Id == result.Id) ||
                TrackedGames.Any(a => a.GameId == result.Id))
            {
                SelectedLibraryGame = null;
                return false;
            }

            isApplyingSearchSelection = true;
            try
            {
                SelectedLibraryGame = result;
                GameSearchText = result.Name;
            }
            finally
            {
                isApplyingSearchSelection = false;
            }

            IsGameSearchOpen = false;
            return true;
        }

        private void AddSelectedGame()
        {
            var selected = SelectedLibraryGame;
            if (selected == null || TrackedGames.Any(a => a.GameId == selected.Id))
            {
                return;
            }

            var now = DateTime.Now;
            var trackedGame = new TrackedGameSettings
            {
                GameId = selected.Id,
                CachedGameName = selected.Name,
                Enabled = true,
                CurrentState = WeeklyState.READY,
                LastResetProcessedLocal = WeeklyScheduleCalculator.GetMostRecentOccurrence(
                    now, DayOfWeek.Monday, TimeSpan.Zero),
                LastSecondaryReminderProcessedLocal = WeeklyScheduleCalculator.GetMostRecentOccurrence(
                    now, DayOfWeek.Monday, TimeSpan.Zero)
            };

            TrackedGames.Add(trackedGame);
            SelectedTrackedGame = trackedGame;
            RefreshLibraryGames();
        }

        private void RemoveSelectedGame()
        {
            var selected = SelectedTrackedGame;
            if (selected == null)
            {
                return;
            }

            TrackedGames.Remove(selected);
            SelectedTrackedGame = TrackedGames.FirstOrDefault();
            RefreshLibraryGames();
        }

        private void UpdateGameSearchResults()
        {
            var query = GameSearchText.Trim();
            var matches = availableGames
                .Where(a => string.IsNullOrEmpty(query) ||
                    (!string.IsNullOrEmpty(a.Name) &&
                     a.Name.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0))
                .Take(MaximumSearchResults)
                .ToList();

            var selectedId = SelectedSearchResult?.Id;
            GameSearchResults.Clear();
            foreach (var match in matches)
            {
                GameSearchResults.Add(match);
            }

            SelectedSearchResult = selectedId.HasValue
                ? GameSearchResults.FirstOrDefault(a => a.Id == selectedId.Value)
                : GameSearchResults.FirstOrDefault();

            if (IsGameSearchOpen && GameSearchResults.Count == 0)
            {
                IsGameSearchOpen = false;
            }
        }

        private void ClearGameSearch()
        {
            isApplyingSearchSelection = true;
            try
            {
                SelectedLibraryGame = null;
                SelectedSearchResult = null;
                GameSearchText = string.Empty;
            }
            finally
            {
                isApplyingSearchSelection = false;
            }

            IsGameSearchOpen = false;
            GameSearchResults.Clear();
        }

        private void NormalizePersistedValues()
        {
            foreach (var trackedGame in TrackedGames)
            {
                trackedGame.WeeklyResetTime = trackedGame.WeeklyResetTime;
                trackedGame.SecondaryReminderTime = trackedGame.SecondaryReminderTime;
                trackedGame.SecondaryNotificationTitle = trackedGame.SecondaryNotificationTitle;
                trackedGame.SecondaryNotificationMessage = trackedGame.SecondaryNotificationMessage;
            }
        }

        private void EnsureCollections()
        {
            if (settings.TrackedGames == null)
            {
                settings.TrackedGames = new ObservableCollection<TrackedGameSettings>();
            }

            NormalizePersistedValues();
        }
    }
}
