using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
        Weekly,
        BiWeekly
    }

    public enum ReminderCadence
    {
        Daily,
        Weekly,
        BiWeekly
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

    public class RoutineSettings : ObservableObject
    {
        public const int MaximumNameLength = 40;

        private Guid id = Guid.NewGuid();
        private string name = string.Empty;
        private int order;
        private TaskState currentState = TaskState.COMPLETE;
        private ResetCadence resetCadence = ResetCadence.Never;
        private DayOfWeek resetDay = DayOfWeek.Monday;
        private string resetTime = "00:00";
        private DateTime? lastResetProcessedLocal;
        private DateTime? biWeeklyResetAnchorLocal;
        private ObservableCollection<ChecklistItemSettings> checklist =
            new ObservableCollection<ChecklistItemSettings>();
        private bool automaticallyCompleteFromChecklist;
        private bool countTowardOverallTaskStatus;
        private bool completedAutomaticallyByChecklist;

        public Guid Id
        {
            get => id;
            set => SetValue(ref id, value);
        }

        public string Name
        {
            get => name;
            set => SetValue(ref name, NormalizeName(value));
        }

        public int Order
        {
            get => order;
            set => SetValue(ref order, Math.Max(0, value));
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
                OnPropertyChanged(nameof(IsComplete));
            }
        }

        [DontSerialize]
        public string DisplayState => CurrentState == TaskState.COMPLETE ? "COMPLETE" : "INCOMPLETE";

        [DontSerialize]
        public bool IsComplete => CurrentState == TaskState.COMPLETE;

        public ResetCadence ResetCadence
        {
            get => resetCadence;
            set
            {
                if (resetCadence == value)
                {
                    return;
                }

                resetCadence = value;
                OnPropertyChanged();
                if (resetCadence == ResetCadence.BiWeekly &&
                    !BiWeeklyResetAnchorLocal.HasValue &&
                    ScheduleCalculator.TryParseLocalTime(ResetTime, out var resetTime))
                {
                    BiWeeklyResetAnchorLocal = ScheduleCalculator.GetFirstFutureWeeklyOccurrence(
                        DateTime.Now,
                        ResetDay,
                        resetTime);
                }
            }
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
                if (BiWeeklyResetAnchorLocal.HasValue &&
                    ScheduleCalculator.TryParseLocalTime(resetTime, out var anchorTime))
                {
                    BiWeeklyResetAnchorLocal = DateTime.SpecifyKind(
                        BiWeeklyResetAnchorLocal.Value.Date.Add(anchorTime),
                        DateTimeKind.Local);
                }
            }
        }

        public DateTime? LastResetProcessedLocal
        {
            get => lastResetProcessedLocal;
            set => SetValue(ref lastResetProcessedLocal, value);
        }

        public DateTime? BiWeeklyResetAnchorLocal
        {
            get => biWeeklyResetAnchorLocal;
            set
            {
                var normalized = value.HasValue
                    ? DateTime.SpecifyKind(value.Value, DateTimeKind.Local)
                    : (DateTime?)null;
                if (biWeeklyResetAnchorLocal == normalized)
                {
                    return;
                }

                biWeeklyResetAnchorLocal = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BiWeeklyResetStartingDate));
                if (normalized.HasValue && resetDay != normalized.Value.DayOfWeek)
                {
                    resetDay = normalized.Value.DayOfWeek;
                    OnPropertyChanged(nameof(ResetDay));
                }
            }
        }

        [DontSerialize]
        public DateTime? BiWeeklyResetStartingDate
        {
            get => BiWeeklyResetAnchorLocal?.Date;
            set
            {
                if (!value.HasValue)
                {
                    BiWeeklyResetAnchorLocal = null;
                    return;
                }

                var time = ScheduleCalculator.TryParseLocalTime(ResetTime, out var resetTime)
                    ? resetTime
                    : TimeSpan.Zero;
                BiWeeklyResetAnchorLocal = DateTime.SpecifyKind(
                    value.Value.Date.Add(time),
                    DateTimeKind.Local);
            }
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

        public bool CountTowardOverallTaskStatus
        {
            get => countTowardOverallTaskStatus;
            set => SetValue(ref countTowardOverallTaskStatus, value);
        }

        public bool CompletedAutomaticallyByChecklist
        {
            get => completedAutomaticallyByChecklist;
            set => SetValue(ref completedAutomaticallyByChecklist, value);
        }

        public static string NormalizeName(string value)
        {
            var normalized = (value ?? string.Empty).Trim();
            return normalized.Length > MaximumNameLength
                ? normalized.Substring(0, MaximumNameLength)
                : normalized;
        }

        private static string NormalizeTime(string value)
        {
            return ScheduleCalculator.TryNormalizeTimeInput(value, out var normalizedValue)
                ? normalizedValue
                : "00:00";
        }
    }

    public class TrackedGameSettings : ObservableObject
    {
        public const int MaximumCustomReminderTitleLength = 40;
        public const int MaximumCustomReminderMessageLength = 160;

        private Guid gameId;
        private string cachedGameName;
        private bool enabled = true;
        private ObservableCollection<RoutineSettings> routines =
            new ObservableCollection<RoutineSettings>();
        private bool customReminderEnabled;
        private ReminderCadence reminderCadence = ReminderCadence.Weekly;
        private DayOfWeek reminderDay = DayOfWeek.Monday;
        private string reminderTime = "00:00";
        private string customReminderTitle = "Game Tasks Reminder";
        private string customReminderMessage = "A game task may be available now.";
        private DateTime? lastReminderProcessedLocal;
        private DateTime? biWeeklyReminderAnchorLocal;
        private bool showIncompleteCoverIndicator = true;

        public TrackedGameSettings()
        {
            SubscribeToRoutines(routines);
        }

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

        public ObservableCollection<RoutineSettings> Routines
        {
            get => routines;
            set
            {
                var normalized = value ?? new ObservableCollection<RoutineSettings>();
                if (ReferenceEquals(routines, normalized))
                {
                    return;
                }

                SubscribeToRoutines(null);
                routines = normalized;
                SubscribeToRoutines(routines);
                OnPropertyChanged();
                NotifyOverallStateChanged();
            }
        }

        [DontSerialize]
        public TaskState CurrentState => RoutineService.GetOverallState(this);

        [DontSerialize]
        public string DisplayState => CurrentState == TaskState.COMPLETE ? "COMPLETE" : "INCOMPLETE";

        [DontSerialize]
        public int ParticipatingRoutineCount =>
            Routines.Count(a => a != null && a.CountTowardOverallTaskStatus);

        public bool CustomReminderEnabled
        {
            get => customReminderEnabled;
            set => SetValue(ref customReminderEnabled, value);
        }

        public ReminderCadence ReminderCadence
        {
            get => reminderCadence;
            set
            {
                if (reminderCadence == value)
                {
                    return;
                }

                reminderCadence = value;
                OnPropertyChanged();
                if (reminderCadence == ReminderCadence.BiWeekly &&
                    !BiWeeklyReminderAnchorLocal.HasValue &&
                    ScheduleCalculator.TryParseLocalTime(ReminderTime, out var reminderTime))
                {
                    BiWeeklyReminderAnchorLocal = ScheduleCalculator.GetFirstFutureWeeklyOccurrence(
                        DateTime.Now,
                        ReminderDay,
                        reminderTime);
                }
            }
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
                if (BiWeeklyReminderAnchorLocal.HasValue &&
                    ScheduleCalculator.TryParseLocalTime(reminderTime, out var anchorTime))
                {
                    BiWeeklyReminderAnchorLocal = DateTime.SpecifyKind(
                        BiWeeklyReminderAnchorLocal.Value.Date.Add(anchorTime),
                        DateTimeKind.Local);
                }
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

        public DateTime? BiWeeklyReminderAnchorLocal
        {
            get => biWeeklyReminderAnchorLocal;
            set
            {
                var normalized = value.HasValue
                    ? DateTime.SpecifyKind(value.Value, DateTimeKind.Local)
                    : (DateTime?)null;
                if (biWeeklyReminderAnchorLocal == normalized)
                {
                    return;
                }

                biWeeklyReminderAnchorLocal = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(BiWeeklyReminderStartingDate));
                if (normalized.HasValue && reminderDay != normalized.Value.DayOfWeek)
                {
                    reminderDay = normalized.Value.DayOfWeek;
                    OnPropertyChanged(nameof(ReminderDay));
                }
            }
        }

        [DontSerialize]
        public DateTime? BiWeeklyReminderStartingDate
        {
            get => BiWeeklyReminderAnchorLocal?.Date;
            set
            {
                if (!value.HasValue)
                {
                    BiWeeklyReminderAnchorLocal = null;
                    return;
                }

                var time = ScheduleCalculator.TryParseLocalTime(ReminderTime, out var reminderTime)
                    ? reminderTime
                    : TimeSpan.Zero;
                BiWeeklyReminderAnchorLocal = DateTime.SpecifyKind(
                    value.Value.Date.Add(time),
                    DateTimeKind.Local);
            }
        }

        public bool ShowIncompleteCoverIndicator
        {
            get => showIncompleteCoverIndicator;
            set => SetValue(ref showIncompleteCoverIndicator, value);
        }

        internal void NotifyOverallStateChanged()
        {
            OnPropertyChanged(nameof(CurrentState));
            OnPropertyChanged(nameof(DisplayState));
            OnPropertyChanged(nameof(ParticipatingRoutineCount));
        }

        private void SubscribeToRoutines(ObservableCollection<RoutineSettings> collection)
        {
            if (routines != null)
            {
                routines.CollectionChanged -= Routines_CollectionChanged;
                foreach (var routine in routines)
                {
                    if (routine != null)
                    {
                        routine.PropertyChanged -= Routine_PropertyChanged;
                    }
                }
            }

            if (collection == null)
            {
                return;
            }

            collection.CollectionChanged += Routines_CollectionChanged;
            foreach (var routine in collection)
            {
                if (routine != null)
                {
                    routine.PropertyChanged += Routine_PropertyChanged;
                }
            }
        }

        private void Routines_CollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if (args.OldItems != null)
            {
                foreach (RoutineSettings routine in args.OldItems)
                {
                    if (routine != null)
                    {
                        routine.PropertyChanged -= Routine_PropertyChanged;
                    }
                }
            }

            if (args.NewItems != null)
            {
                foreach (RoutineSettings routine in args.NewItems)
                {
                    if (routine != null)
                    {
                        routine.PropertyChanged += Routine_PropertyChanged;
                    }
                }
            }

            NotifyOverallStateChanged();
        }

        private void Routine_PropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (string.Equals(args.PropertyName, nameof(RoutineSettings.CurrentState), StringComparison.Ordinal) ||
                string.Equals(args.PropertyName, nameof(RoutineSettings.CountTowardOverallTaskStatus), StringComparison.Ordinal))
            {
                NotifyOverallStateChanged();
            }
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
        public const int CurrentSchemaVersion = 2;

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
        private RoutineSettings selectedRoutine;
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

                settings = value ?? SettingsMigrationService.CreateEmpty();
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

        public bool IsIncompleteIndicatorSupportedByCurrentTheme =>
            plugin.IsIncompleteIndicatorSupportedByCurrentTheme;

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
                SelectedRoutine = value?.Routines?.OrderBy(a => a.Order).FirstOrDefault();
                NewChecklistItemText = string.Empty;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public RoutineSettings SelectedRoutine
        {
            get => selectedRoutine;
            set
            {
                if (ReferenceEquals(selectedRoutine, value))
                {
                    return;
                }

                selectedRoutine = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanMoveSelectedRoutineUp));
                OnPropertyChanged(nameof(CanMoveSelectedRoutineDown));
                NewChecklistItemText = string.Empty;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool CanMoveSelectedRoutineUp => CanMoveRoutine(SelectedRoutine, -1);

        public bool CanMoveSelectedRoutineDown
        {
            get
            {
                return CanMoveRoutine(SelectedRoutine, 1);
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
        public RelayCommand AddRoutineCommand { get; }
        public RelayCommand DeleteRoutineCommand { get; }
        public RelayCommand<RoutineSettings> MoveRoutineUpCommand { get; }
        public RelayCommand<RoutineSettings> MoveRoutineDownCommand { get; }
        public RelayCommand ToggleSelectedRoutineStateCommand { get; }
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
            AddRoutineCommand = new RelayCommand(AddRoutine, () => SelectedTrackedGame != null);
            DeleteRoutineCommand = new RelayCommand(DeleteRoutine, () => SelectedRoutine != null);
            MoveRoutineUpCommand = new RelayCommand<RoutineSettings>(
                routine => MoveRoutine(routine, -1),
                routine => CanMoveRoutine(routine, -1));
            MoveRoutineDownCommand = new RelayCommand<RoutineSettings>(
                routine => MoveRoutine(routine, 1),
                routine => CanMoveRoutine(routine, 1));
            ToggleSelectedRoutineStateCommand = new RelayCommand(
                () => SetSelectedRoutineState(SelectedRoutine?.CurrentState != TaskState.COMPLETE),
                () => SelectedRoutine != null);
            AddChecklistItemCommand = new RelayCommand(
                AddChecklistItem,
                () => SelectedRoutine != null && !string.IsNullOrWhiteSpace(NewChecklistItemText));
            DeleteChecklistItemCommand = new RelayCommand<ChecklistItemSettings>(DeleteChecklistItem);
            MoveChecklistItemUpCommand = new RelayCommand<ChecklistItemSettings>(item => MoveChecklistItem(item, -1));
            MoveChecklistItemDownCommand = new RelayCommand<ChecklistItemSettings>(item => MoveChecklistItem(item, 1));
            ResetChecklistCommand = new RelayCommand(
                () => plugin.ResetChecklist(SelectedTrackedGame, SelectedRoutine, true, false),
                () => SelectedTrackedGame != null && SelectedRoutine != null);
        }

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(Settings);
            plugin.SetSettingsEditing(true);
            RefreshLibraryGames();
        }

        public void CancelEdit()
        {
            var discardedSettings = Settings;
            Settings = editingClone ?? SettingsMigrationService.CreateEmpty();
            editingClone = null;
            SelectedTrackedGame = null;
            plugin.SetSettingsEditing(false);
            plugin.ApplySettingsChanges(discardedSettings, Settings);
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
            foreach (var duplicateId in TrackedGames.GroupBy(a => a.GameId).Where(a => a.Count() > 1).Select(a => a.Key))
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

                var routines = trackedGame.Routines?.Where(a => a != null).ToList() ??
                    new List<RoutineSettings>();
                if (routines.GroupBy(a => a.Id).Any(a => a.Key == Guid.Empty || a.Count() > 1))
                {
                    errors.Add($"{gameName}: routine IDs must be unique and valid.");
                }

                foreach (var duplicateName in routines
                    .Where(a => !string.IsNullOrWhiteSpace(a.Name))
                    .GroupBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                    .Where(a => a.Count() > 1)
                    .Select(a => a.Key))
                {
                    errors.Add($"{gameName}: routine name \"{duplicateName}\" is used more than once.");
                }

                foreach (var routine in routines)
                {
                    var routineLabel = string.IsNullOrWhiteSpace(routine.Name) ? "Routine" : routine.Name;
                    if (string.IsNullOrWhiteSpace(routine.Name))
                    {
                        errors.Add($"{gameName}: routine name is required.");
                    }
                    else if (routine.Name.Length > RoutineSettings.MaximumNameLength)
                    {
                        errors.Add($"{gameName}: routine names cannot exceed 40 characters.");
                    }

                    if (!Enum.IsDefined(typeof(TaskState), routine.CurrentState))
                    {
                        errors.Add($"{gameName} / {routineLabel}: routine status is invalid.");
                    }

                    if (!Enum.IsDefined(typeof(ResetCadence), routine.ResetCadence))
                    {
                        errors.Add($"{gameName} / {routineLabel}: reset schedule is invalid.");
                    }

                    if (routine.ResetCadence != ResetCadence.Never &&
                        (!ScheduleCalculator.TryNormalizeTimeInput(routine.ResetTime, out var normalizedResetTime) ||
                         !string.Equals(routine.ResetTime, normalizedResetTime, StringComparison.Ordinal)))
                    {
                        errors.Add($"{gameName} / {routineLabel}: reset time must use 24-hour HH:mm format.");
                    }

                    if (routine.ResetCadence == ResetCadence.BiWeekly &&
                        !routine.BiWeeklyResetAnchorLocal.HasValue)
                    {
                        errors.Add($"{gameName} / {routineLabel}: a Biweekly start date is required.");
                    }

                    var checklist = routine.Checklist?.Where(a => a != null).ToList() ??
                        new List<ChecklistItemSettings>();
                    if (checklist.GroupBy(a => a.Id).Any(a => a.Key == Guid.Empty || a.Count() > 1))
                    {
                        errors.Add($"{gameName} / {routineLabel}: checklist item IDs must be unique and valid.");
                    }

                    foreach (var item in checklist)
                    {
                        if (string.IsNullOrWhiteSpace(item.Text))
                        {
                            errors.Add($"{gameName} / {routineLabel}: checklist item text cannot be empty.");
                        }
                        else if (item.Text.Length > ChecklistItemSettings.MaximumTextLength)
                        {
                            errors.Add($"{gameName} / {routineLabel}: checklist items cannot exceed 120 characters.");
                        }
                    }
                }

                CustomReminderService.Validate(trackedGame, gameName, errors);
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

        internal void SelectTrackedGame(Guid gameId)
        {
            SelectedTrackedGame = TrackedGames.FirstOrDefault(a => a.GameId == gameId);
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

            var currentIndex = SelectedSearchResult == null ? -1 : GameSearchResults.IndexOf(SelectedSearchResult);
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

        public void ChecklistItemChecked(ChecklistItemSettings item, bool isChecked)
        {
            if (SelectedRoutine == null || item == null || !SelectedRoutine.Checklist.Contains(item))
            {
                return;
            }

            item.IsChecked = isChecked;
            plugin.ChecklistItemStateChanged(SelectedTrackedGame, SelectedRoutine, false);
        }

        public void CommitChecklistItemText(ChecklistItemSettings item, string text)
        {
            if (SelectedRoutine == null || item == null || !SelectedRoutine.Checklist.Contains(item))
            {
                return;
            }

            plugin.EditChecklistItem(SelectedTrackedGame, SelectedRoutine, item.Id, text, false);
        }

        public bool CommitRoutineName(RoutineSettings routine, string text)
        {
            return routine != null &&
                plugin.RenameRoutine(SelectedTrackedGame, routine.Id, text, false);
        }

        public void ChecklistAutoCompletionChanged(bool enabled)
        {
            if (SelectedRoutine != null)
            {
                SelectedRoutine.AutomaticallyCompleteFromChecklist = enabled;
            }

            plugin.ChecklistAutoCompletionChanged(SelectedTrackedGame, SelectedRoutine, false);
        }

        public void RoutineCountTowardOverallChanged(bool enabled)
        {
            if (SelectedRoutine != null)
            {
                SelectedRoutine.CountTowardOverallTaskStatus = enabled;
            }

            plugin.RoutineAggregateSettingChanged(SelectedTrackedGame, false);
        }

        public void SetSelectedRoutineState(bool isComplete)
        {
            if (SelectedTrackedGame != null && SelectedRoutine != null)
            {
                plugin.SetRoutineState(
                    SelectedTrackedGame,
                    SelectedRoutine,
                    isComplete ? TaskState.COMPLETE : TaskState.INCOMPLETE,
                    false);
            }
        }

        internal void NotifyIncompleteIndicatorThemeSupportChanged()
        {
            OnPropertyChanged(nameof(IsIncompleteIndicatorSupportedByCurrentTheme));
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

        private void AddRoutine()
        {
            var routine = plugin.AddRoutine(SelectedTrackedGame, false);
            if (routine != null)
            {
                SelectedRoutine = routine;
            }
        }

        private void DeleteRoutine()
        {
            var game = SelectedTrackedGame;
            var routine = SelectedRoutine;
            if (game == null || routine == null)
            {
                return;
            }

            var index = game.Routines.IndexOf(routine);
            if (plugin.DeleteRoutine(game, routine.Id, true, false))
            {
                SelectedRoutine = game.Routines.Count == 0
                    ? null
                    : game.Routines[Math.Min(index, game.Routines.Count - 1)];
            }
        }

        private bool CanMoveRoutine(RoutineSettings routine, int offset)
        {
            if (SelectedTrackedGame?.Routines == null || routine == null || offset == 0)
            {
                return false;
            }

            var ordered = SelectedTrackedGame.Routines
                .Where(a => a != null)
                .OrderBy(a => a.Order)
                .ToList();
            var index = ordered.FindIndex(a => a.Id == routine.Id);
            var targetIndex = index + offset;
            return index >= 0 && targetIndex >= 0 && targetIndex < ordered.Count;
        }

        private void MoveRoutine(RoutineSettings routine, int offset)
        {
            if (SelectedTrackedGame != null && routine != null &&
                SelectedTrackedGame.Routines.Any(a => a != null && a.Id == routine.Id))
            {
                var routineId = routine.Id;
                if (plugin.MoveRoutine(SelectedTrackedGame, routineId, offset, false))
                {
                    SelectedRoutine = SelectedTrackedGame.Routines
                        .FirstOrDefault(a => a != null && a.Id == routineId);
                }
                OnPropertyChanged(nameof(CanMoveSelectedRoutineUp));
                OnPropertyChanged(nameof(CanMoveSelectedRoutineDown));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void AddChecklistItem()
        {
            if (plugin.AddChecklistItem(SelectedTrackedGame, SelectedRoutine, NewChecklistItemText, false))
            {
                NewChecklistItemText = string.Empty;
            }
        }

        private void DeleteChecklistItem(ChecklistItemSettings item)
        {
            plugin.DeleteChecklistItem(
                SelectedTrackedGame,
                SelectedRoutine,
                item?.Id ?? Guid.Empty,
                false);
        }

        private void MoveChecklistItem(ChecklistItemSettings item, int offset)
        {
            plugin.MoveChecklistItem(
                SelectedTrackedGame,
                SelectedRoutine,
                item?.Id ?? Guid.Empty,
                offset,
                false);
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (string.Equals(args.PropertyName, nameof(GameRoutinesSettings.ShowIncompleteCoverIndicator), StringComparison.Ordinal))
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
            Settings.SchemaVersion = GameRoutinesSettings.CurrentSchemaVersion;
            foreach (var trackedGame in TrackedGames)
            {
                trackedGame.ReminderTime = trackedGame.ReminderTime;
                trackedGame.CustomReminderTitle = trackedGame.CustomReminderTitle;
                trackedGame.CustomReminderMessage = trackedGame.CustomReminderMessage;
                RoutineService.Normalize(trackedGame);
            }
        }

        private GameRoutinesSettings LoadSettings(out bool migrated)
        {
            var loaded = plugin.LoadPluginSettings<GameRoutinesSettings>();
            if (loaded != null && loaded.SchemaVersion >= GameRoutinesSettings.CurrentSchemaVersion)
            {
                migrated = false;
                return loaded;
            }

            if (loaded != null && loaded.SchemaVersion == 1)
            {
                var legacyV1 = plugin.LoadPluginSettings<LegacySettingsV1>();
                migrated = true;
                return legacyV1 == null ? SettingsMigrationService.CreateEmpty() : SettingsMigrationService.Migrate(legacyV1);
            }

            var legacyV0 = plugin.LoadPluginSettings<LegacySettingsV0>();
            migrated = true;
            return legacyV0 == null ? SettingsMigrationService.CreateEmpty() : SettingsMigrationService.Migrate(legacyV0);
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
