using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace GameRoutines
{
    public enum TaskState
    {
        INCOMPLETE,
        COMPLETE
    }

    public enum ResetCadence
    {
        Never,
        Daily,
        Weekly
    }

    public enum ReminderCadence
    {
        Daily,
        Weekly
    }

    public class ChecklistItemSettings : ObservableObject
    {
        public const int MaximumTextLength = 120;

        private Guid id = Guid.NewGuid();
        private string text = string.Empty;
        private bool isChecked;
        private int order;

        public Guid Id
        {
            get => id;
            set => SetValue(ref id, value);
        }

        public string Text
        {
            get => text;
            set => SetValue(ref text, NormalizeText(value));
        }

        public bool IsChecked
        {
            get => isChecked;
            set => SetValue(ref isChecked, value);
        }

        public int Order
        {
            get => order;
            set => SetValue(ref order, Math.Max(0, value));
        }

        public static string NormalizeText(string value)
        {
            var normalized = (value ?? string.Empty).Trim();
            return normalized.Length > MaximumTextLength
                ? normalized.Substring(0, MaximumTextLength)
                : normalized;
        }
    }

    public class TrackedGameSettings : ObservableObject
    {
        public const int MaximumCustomReminderTitleLength = 40;
        public const int MaximumCustomReminderMessageLength = 160;

        private Guid gameId;
        private string cachedGameName;
        private bool enabled = true;
        private ResetCadence resetCadence = ResetCadence.Never;
        private DayOfWeek resetDay = DayOfWeek.Monday;
        private string resetTime = "00:00";
        private TaskState currentState = TaskState.COMPLETE;
        private DateTime? lastResetProcessedLocal;
        private bool customReminderEnabled;
        private ReminderCadence reminderCadence = ReminderCadence.Weekly;
        private DayOfWeek reminderDay = DayOfWeek.Monday;
        private string reminderTime = "00:00";
        private string customReminderTitle = "Game Tasks Reminder";
        private string customReminderMessage = "A game task may be available now.";
        private DateTime? lastReminderProcessedLocal;
        private ObservableCollection<ChecklistItemSettings> checklist =
            new ObservableCollection<ChecklistItemSettings>();
        private bool automaticallyCompleteFromChecklist;
        private bool completedAutomaticallyByChecklist;
        private bool showIncompleteCoverIndicator = true;

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

        public ResetCadence ResetCadence
        {
            get => resetCadence;
            set => SetValue(ref resetCadence, value);
        }

        public DayOfWeek ResetDay
        {
            get => resetDay;
            set => SetValue(ref resetDay, value);
        }

        public string ResetTime
        {
            get => resetTime;
            set
            {
                var normalizedValue = NormalizeTime(value);
                if (string.Equals(resetTime, normalizedValue, StringComparison.Ordinal))
                {
                    return;
                }

                resetTime = normalizedValue;
                OnPropertyChanged();
            }
        }

        public TaskState CurrentState
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
        public string DisplayState => CurrentState == TaskState.COMPLETE ? "COMPLETE" : "INCOMPLETE";

        public DateTime? LastResetProcessedLocal
        {
            get => lastResetProcessedLocal;
            set => SetValue(ref lastResetProcessedLocal, value);
        }

        public bool CustomReminderEnabled
        {
            get => customReminderEnabled;
            set => SetValue(ref customReminderEnabled, value);
        }

        public ReminderCadence ReminderCadence
        {
            get => reminderCadence;
            set => SetValue(ref reminderCadence, value);
        }

        public DayOfWeek ReminderDay
        {
            get => reminderDay;
            set => SetValue(ref reminderDay, value);
        }

        public string ReminderTime
        {
            get => reminderTime;
            set
            {
                var normalizedValue = NormalizeTime(value);
                if (string.Equals(reminderTime, normalizedValue, StringComparison.Ordinal))
                {
                    return;
                }

                reminderTime = normalizedValue;
                OnPropertyChanged();
            }
        }

        public string CustomReminderTitle
        {
            get => customReminderTitle;
            set
            {
                var boundedValue = value != null && value.Length > MaximumCustomReminderTitleLength
                    ? value.Substring(0, MaximumCustomReminderTitleLength)
                    : value;
                SetValue(ref customReminderTitle, boundedValue);
            }
        }

        public string CustomReminderMessage
        {
            get => customReminderMessage;
            set
            {
                var boundedValue = value != null && value.Length > MaximumCustomReminderMessageLength
                    ? value.Substring(0, MaximumCustomReminderMessageLength)
                    : value;
                SetValue(ref customReminderMessage, boundedValue);
            }
        }

        public DateTime? LastReminderProcessedLocal
        {
            get => lastReminderProcessedLocal;
            set => SetValue(ref lastReminderProcessedLocal, value);
        }

        public ObservableCollection<ChecklistItemSettings> Checklist
        {
            get => checklist;
            set => SetValue(ref checklist, value ?? new ObservableCollection<ChecklistItemSettings>());
        }

        public bool AutomaticallyCompleteFromChecklist
        {
            get => automaticallyCompleteFromChecklist;
            set => SetValue(ref automaticallyCompleteFromChecklist, value);
        }

        public bool CompletedAutomaticallyByChecklist
        {
            get => completedAutomaticallyByChecklist;
            set => SetValue(ref completedAutomaticallyByChecklist, value);
        }

        public bool ShowIncompleteCoverIndicator
        {
            get => showIncompleteCoverIndicator;
            set => SetValue(ref showIncompleteCoverIndicator, value);
        }

        private static string NormalizeTime(string value)
        {
            return ScheduleCalculator.TryNormalizeTimeInput(value, out var normalizedValue)
                ? normalizedValue
                : "00:00";
        }
    }

    public class GameRoutinesSettings : ObservableObject
    {
        public const int CurrentSchemaVersion = 1;

        private int schemaVersion;
        private bool useTasksAvailableTag;
        private bool showBlockedManualStateWarning = true;
        private bool showIncompleteCoverIndicator = true;
        private ObservableCollection<TrackedGameSettings> trackedGames =
            new ObservableCollection<TrackedGameSettings>();

        public int SchemaVersion
        {
            get => schemaVersion;
            set => SetValue(ref schemaVersion, value);
        }

        public bool UseTasksAvailableTag
        {
            get => useTasksAvailableTag;
            set => SetValue(ref useTasksAvailableTag, value);
        }

        public bool ShowBlockedManualStateWarning
        {
            get => showBlockedManualStateWarning;
            set => SetValue(ref showBlockedManualStateWarning, value);
        }

        public bool ShowIncompleteCoverIndicator
        {
            get => showIncompleteCoverIndicator;
            set => SetValue(ref showIncompleteCoverIndicator, value);
        }

        public ObservableCollection<TrackedGameSettings> TrackedGames
        {
            get => trackedGames;
            set => SetValue(ref trackedGames, value ?? new ObservableCollection<TrackedGameSettings>());
        }
    }

    internal sealed class LegacySettingsV0
    {
        public bool UseReadyTag { get; set; }

        public bool ShowBlockedManualStateWarning { get; set; } = true;

        public bool ShowIncompleteCoverIndicator { get; set; } = true;

        public ObservableCollection<LegacyTrackedGameSettings> TrackedGames { get; set; } =
            new ObservableCollection<LegacyTrackedGameSettings>();
    }

    internal sealed class LegacyTrackedGameSettings
    {
        public Guid GameId { get; set; }

        public string CachedGameName { get; set; }

        public bool Enabled { get; set; } = true;

        public DayOfWeek WeeklyResetDay { get; set; } = DayOfWeek.Monday;

        public string WeeklyResetTime { get; set; } = "00:00";

        public int CurrentState { get; set; }

        public DateTime? LastResetProcessedLocal { get; set; }

        public bool SecondaryReminderEnabled { get; set; }

        public DayOfWeek SecondaryReminderDay { get; set; } = DayOfWeek.Monday;

        public string SecondaryReminderTime { get; set; } = "00:00";

        public string SecondaryNotificationTitle { get; set; }

        public string SecondaryNotificationMessage { get; set; }

        public DateTime? LastSecondaryReminderProcessedLocal { get; set; }

        public ObservableCollection<ChecklistItemSettings> Checklist { get; set; } =
            new ObservableCollection<ChecklistItemSettings>();

        public bool AutomaticallyCompleteFromChecklist { get; set; }

        public bool CompletedAutomaticallyByChecklist { get; set; }

        public bool ShowIncompleteCoverIndicator { get; set; } = true;
    }

    public class LibraryGameOption
    {
        public Guid Id { get; set; }

        public string Name { get; set; }

        public string DisplayName { get; set; }
    }

    public class GameRoutinesSettingsViewModel : ObservableObject, ISettings
    {
        private const int MaximumSearchResults = 50;
        private readonly GameRoutines plugin;
        private readonly List<LibraryGameOption> availableGames = new List<LibraryGameOption>();
        private GameRoutinesSettings editingClone;
        private GameRoutinesSettings settings;
        private LibraryGameOption selectedLibraryGame;
        private LibraryGameOption selectedSearchResult;
        private TrackedGameSettings selectedTrackedGame;
        private string gameSearchText = string.Empty;
        private string newChecklistItemText = string.Empty;
        private bool isGameSearchOpen;
        private bool isApplyingSearchSelection;

        public GameRoutinesSettings Settings
        {
            get => settings;
            set
            {
                if (settings != null)
                {
                    settings.PropertyChanged -= Settings_PropertyChanged;
                }

                settings = value ?? CreateEmptySettings();
                settings.PropertyChanged += Settings_PropertyChanged;
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

        public IReadOnlyList<ResetCadence> ResetCadences { get; } =
            Enum.GetValues(typeof(ResetCadence)).Cast<ResetCadence>().ToList();

        public IReadOnlyList<ReminderCadence> ReminderCadences { get; } =
            Enum.GetValues(typeof(ReminderCadence)).Cast<ReminderCadence>().ToList();

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
                    !string.Equals(SelectedLibraryGame.DisplayName, gameSearchText, StringComparison.CurrentCulture))
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
            set
            {
                if (ReferenceEquals(selectedTrackedGame, value))
                {
                    return;
                }

                selectedTrackedGame = value;
                OnPropertyChanged();
                NewChecklistItemText = string.Empty;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string NewChecklistItemText
        {
            get => newChecklistItemText;
            set
            {
                var boundedValue = value ?? string.Empty;
                if (boundedValue.Length > ChecklistItemSettings.MaximumTextLength)
                {
                    boundedValue = boundedValue.Substring(0, ChecklistItemSettings.MaximumTextLength);
                }

                if (string.Equals(newChecklistItemText, boundedValue, StringComparison.Ordinal))
                {
                    return;
                }

                newChecklistItemText = boundedValue;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public RelayCommand AddGameCommand { get; }

        public RelayCommand RemoveGameCommand { get; }

        public RelayCommand AddChecklistItemCommand { get; }

        public RelayCommand<ChecklistItemSettings> DeleteChecklistItemCommand { get; }

        public RelayCommand<ChecklistItemSettings> MoveChecklistItemUpCommand { get; }

        public RelayCommand<ChecklistItemSettings> MoveChecklistItemDownCommand { get; }

        public RelayCommand ResetChecklistCommand { get; }

        public GameRoutinesSettingsViewModel(GameRoutines plugin)
        {
            this.plugin = plugin;
            Settings = LoadSettings(out var migrated);
            if (migrated)
            {
                plugin.SavePluginSettings(Settings);
            }

            AddGameCommand = new RelayCommand(AddSelectedGame, () => CanAddSelectedGame);
            RemoveGameCommand = new RelayCommand(RemoveSelectedGame, () => SelectedTrackedGame != null);
            AddChecklistItemCommand = new RelayCommand(
                AddChecklistItem,
                () => SelectedTrackedGame != null &&
                      !string.IsNullOrWhiteSpace(NewChecklistItemText));
            DeleteChecklistItemCommand = new RelayCommand<ChecklistItemSettings>(DeleteChecklistItem);
            MoveChecklistItemUpCommand = new RelayCommand<ChecklistItemSettings>(
                item => MoveChecklistItem(item, -1));
            MoveChecklistItemDownCommand = new RelayCommand<ChecklistItemSettings>(
                item => MoveChecklistItem(item, 1));
            ResetChecklistCommand = new RelayCommand(
                () => plugin.ResetChecklist(SelectedTrackedGame, true, false),
                () => SelectedTrackedGame != null);
        }

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(Settings);
            plugin.SetSettingsEditing(true);
            RefreshLibraryGames();
        }

        public void CancelEdit()
        {
            Settings = editingClone ?? CreateEmptySettings();
            editingClone = null;
            SelectedTrackedGame = null;
            plugin.SetSettingsEditing(false);
            plugin.NotifyUiStateChanged();
            RefreshLibraryGames();
        }

        public void EndEdit()
        {
            var previousSettings = editingClone;
            NormalizePersistedValues();
            plugin.PrepareSettingsForSave(previousSettings, Settings);
            plugin.SavePluginSettings(Settings);
            editingClone = null;
            plugin.SetSettingsEditing(false);
            plugin.ApplySettingsChanges(previousSettings, Settings);
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

                var duplicateChecklistIds = trackedGame.Checklist
                    .Where(a => a != null)
                    .GroupBy(a => a.Id)
                    .Where(a => a.Key == Guid.Empty || a.Count() > 1)
                    .Select(a => a.Key)
                    .ToList();
                if (duplicateChecklistIds.Count > 0)
                {
                    errors.Add($"{gameName}: checklist item IDs must be unique and valid.");
                }

                foreach (var item in trackedGame.Checklist.Where(a => a != null))
                {
                    if (string.IsNullOrWhiteSpace(item.Text))
                    {
                        errors.Add($"{gameName}: checklist item text cannot be empty.");
                    }
                    else if (item.Text.Length > ChecklistItemSettings.MaximumTextLength)
                    {
                        errors.Add($"{gameName}: checklist items cannot exceed 120 characters.");
                    }
                }

                if (!Enum.IsDefined(typeof(ResetCadence), trackedGame.ResetCadence))
                {
                    errors.Add($"{gameName}: reset schedule is invalid.");
                }

                if (trackedGame.ResetCadence != ResetCadence.Never &&
                    (!ScheduleCalculator.TryNormalizeTimeInput(
                        trackedGame.ResetTime, out var normalizedResetTime) ||
                     !string.Equals(
                        trackedGame.ResetTime, normalizedResetTime, StringComparison.Ordinal)))
                {
                    errors.Add($"{gameName}: reset time must use 24-hour HH:mm format.");
                }

                if (!Enum.IsDefined(typeof(ReminderCadence), trackedGame.ReminderCadence))
                {
                    errors.Add($"{gameName}: reminder frequency is invalid.");
                }

                if (trackedGame.CustomReminderTitle != null &&
                    trackedGame.CustomReminderTitle.Length >
                    TrackedGameSettings.MaximumCustomReminderTitleLength)
                {
                    errors.Add($"{gameName}: custom reminder title cannot exceed 40 characters.");
                }

                if (trackedGame.CustomReminderMessage != null &&
                    trackedGame.CustomReminderMessage.Length >
                    TrackedGameSettings.MaximumCustomReminderMessageLength)
                {
                    errors.Add($"{gameName}: custom reminder message cannot exceed 160 characters.");
                }

                if (trackedGame.CustomReminderEnabled)
                {
                    if (!ScheduleCalculator.TryNormalizeTimeInput(
                            trackedGame.ReminderTime, out var normalizedReminderTime) ||
                        !string.Equals(
                            trackedGame.ReminderTime,
                            normalizedReminderTime,
                            StringComparison.Ordinal))
                    {
                        errors.Add($"{gameName}: custom reminder time must use 24-hour HH:mm format.");
                    }

                    if (string.IsNullOrWhiteSpace(trackedGame.CustomReminderTitle))
                    {
                        errors.Add($"{gameName}: custom reminder title cannot be empty when the custom reminder is enabled.");
                    }

                    if (string.IsNullOrWhiteSpace(trackedGame.CustomReminderMessage))
                    {
                        errors.Add($"{gameName}: custom reminder message cannot be empty when the custom reminder is enabled.");
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
                    .ToList();
                var duplicateNames = new HashSet<string>(
                    games.GroupBy(a => a.Name ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
                        .Where(a => a.Count() > 1)
                        .Select(a => a.Key),
                    StringComparer.CurrentCultureIgnoreCase);

                foreach (var game in games)
                {
                    availableGames.Add(new LibraryGameOption
                    {
                        Id = game.Id,
                        Name = game.Name,
                        DisplayName = duplicateNames.Contains(game.Name ?? string.Empty)
                            ? $"{game.Name} \u2014 {GetLibraryContext(game)}"
                            : game.Name
                    });
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
                GameSearchText = result.DisplayName;
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

            var game = plugin.PlayniteApi.Database.Games.Get(selected.Id);
            SelectedTrackedGame = plugin.TrackGames(new[] { game }, false).FirstOrDefault();
            RefreshLibraryGames();
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (string.Equals(
                    args.PropertyName,
                    nameof(GameRoutinesSettings.ShowIncompleteCoverIndicator),
                    StringComparison.Ordinal))
            {
                plugin.NotifyUiStateChanged();
            }
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

        public void ChecklistItemChecked(ChecklistItemSettings item, bool isChecked)
        {
            if (SelectedTrackedGame == null || item == null ||
                !SelectedTrackedGame.Checklist.Contains(item))
            {
                return;
            }

            item.IsChecked = isChecked;
            plugin.ChecklistItemStateChanged(SelectedTrackedGame, false);
        }

        public void CommitChecklistItemText(ChecklistItemSettings item, string text)
        {
            if (SelectedTrackedGame == null || item == null ||
                !SelectedTrackedGame.Checklist.Contains(item))
            {
                return;
            }

            plugin.EditChecklistItem(SelectedTrackedGame, item.Id, text, false);
        }

        public void ChecklistAutoCompletionChanged(bool enabled)
        {
            if (SelectedTrackedGame != null)
            {
                SelectedTrackedGame.AutomaticallyCompleteFromChecklist = enabled;
            }

            plugin.ChecklistAutoCompletionChanged(SelectedTrackedGame, false);
        }

        private void AddChecklistItem()
        {
            if (plugin.AddChecklistItem(SelectedTrackedGame, NewChecklistItemText, false))
            {
                NewChecklistItemText = string.Empty;
            }
        }

        private void DeleteChecklistItem(ChecklistItemSettings item)
        {
            plugin.DeleteChecklistItem(SelectedTrackedGame, item?.Id ?? Guid.Empty, false);
        }

        private void MoveChecklistItem(ChecklistItemSettings item, int offset)
        {
            plugin.MoveChecklistItem(SelectedTrackedGame, item?.Id ?? Guid.Empty, offset, false);
        }

        private void UpdateGameSearchResults()
        {
            var query = GameSearchText.Trim();
            var matches = availableGames
                .Where(a => string.IsNullOrEmpty(query) ||
                    (!string.IsNullOrEmpty(a.DisplayName) &&
                     a.DisplayName.IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0))
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
                trackedGame.ResetTime = trackedGame.ResetTime;
                trackedGame.ReminderTime = trackedGame.ReminderTime;
                trackedGame.CustomReminderTitle = trackedGame.CustomReminderTitle;
                trackedGame.CustomReminderMessage = trackedGame.CustomReminderMessage;
                ChecklistService.Normalize(trackedGame);
            }
        }

        private GameRoutinesSettings LoadSettings(out bool migrated)
        {
            var loaded = plugin.LoadPluginSettings<GameRoutinesSettings>();
            if (loaded != null &&
                loaded.SchemaVersion >= GameRoutinesSettings.CurrentSchemaVersion)
            {
                migrated = false;
                return loaded;
            }

            var legacy = plugin.LoadPluginSettings<LegacySettingsV0>();
            migrated = true;
            return legacy == null ? CreateEmptySettings() : MigrateLegacySettings(legacy);
        }

        private static GameRoutinesSettings MigrateLegacySettings(LegacySettingsV0 legacy)
        {
            var migrated = new GameRoutinesSettings
            {
                SchemaVersion = GameRoutinesSettings.CurrentSchemaVersion,
                UseTasksAvailableTag = legacy.UseReadyTag,
                ShowBlockedManualStateWarning = legacy.ShowBlockedManualStateWarning,
                ShowIncompleteCoverIndicator = legacy.ShowIncompleteCoverIndicator
            };

            foreach (var oldGame in legacy.TrackedGames ??
                new ObservableCollection<LegacyTrackedGameSettings>())
            {
                migrated.TrackedGames.Add(new TrackedGameSettings
                {
                    GameId = oldGame.GameId,
                    CachedGameName = oldGame.CachedGameName,
                    Enabled = oldGame.Enabled,
                    ResetCadence = ResetCadence.Weekly,
                    ResetDay = oldGame.WeeklyResetDay,
                    ResetTime = oldGame.WeeklyResetTime,
                    CurrentState = oldGame.CurrentState == 1
                        ? TaskState.COMPLETE
                        : TaskState.INCOMPLETE,
                    LastResetProcessedLocal = oldGame.LastResetProcessedLocal,
                    CustomReminderEnabled = oldGame.SecondaryReminderEnabled,
                    ReminderCadence = ReminderCadence.Weekly,
                    ReminderDay = oldGame.SecondaryReminderDay,
                    ReminderTime = oldGame.SecondaryReminderTime,
                    CustomReminderTitle = oldGame.SecondaryNotificationTitle,
                    CustomReminderMessage = oldGame.SecondaryNotificationMessage,
                    LastReminderProcessedLocal = oldGame.LastSecondaryReminderProcessedLocal,
                    Checklist = oldGame.Checklist ?? new ObservableCollection<ChecklistItemSettings>(),
                    AutomaticallyCompleteFromChecklist = oldGame.AutomaticallyCompleteFromChecklist,
                    CompletedAutomaticallyByChecklist = oldGame.CompletedAutomaticallyByChecklist,
                    ShowIncompleteCoverIndicator = oldGame.ShowIncompleteCoverIndicator
                });
            }

            return migrated;
        }

        private static GameRoutinesSettings CreateEmptySettings()
        {
            return new GameRoutinesSettings
            {
                SchemaVersion = GameRoutinesSettings.CurrentSchemaVersion
            };
        }

        private static string GetLibraryContext(Game game)
        {
            if (!string.IsNullOrWhiteSpace(game.Source?.Name))
            {
                return game.Source.Name;
            }

            var platforms = game.Platforms?
                .Where(a => !string.IsNullOrWhiteSpace(a?.Name))
                .Select(a => a.Name)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            if (platforms?.Count > 0)
            {
                return string.Join(", ", platforms);
            }

            return game.Id.ToString("N").Substring(0, 8);
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
