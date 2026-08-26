using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GameRoutines
{
    public static class RoutineService
    {
        public static RoutineSettings CreateDefault(
            string name,
            int order,
            bool countTowardOverallTaskStatus = false)
        {
            return new RoutineSettings
            {
                Id = Guid.NewGuid(),
                Name = name,
                Order = Math.Max(0, order),
                CurrentState = TaskState.COMPLETE,
                ResetCadence = ResetCadence.Never,
                ResetDay = DayOfWeek.Monday,
                ResetTime = "00:00",
                AutomaticallyCompleteFromChecklist = false,
                CountTowardOverallTaskStatus = countTowardOverallTaskStatus,
                Checklist = new ObservableCollection<ChecklistItemSettings>()
            };
        }

        public static void Normalize(TrackedGameSettings trackedGame)
        {
            if (trackedGame == null)
            {
                return;
            }

            var source = trackedGame.Routines ?? new ObservableCollection<RoutineSettings>();
            var usedIds = new HashSet<Guid>();
            var normalized = source
                .Where(a => a != null)
                .Select((routine, index) => new { Routine = routine, OriginalIndex = index })
                .OrderBy(a => a.Routine.Order)
                .ThenBy(a => a.OriginalIndex)
                .Select(a => a.Routine)
                .ToList();

            for (var index = 0; index < normalized.Count; index++)
            {
                var routine = normalized[index];
                if (routine.Id == Guid.Empty || !usedIds.Add(routine.Id))
                {
                    routine.Id = Guid.NewGuid();
                    usedIds.Add(routine.Id);
                }

                routine.Name = routine.Name;
                if (string.IsNullOrWhiteSpace(routine.Name))
                {
                    routine.Name = GenerateUniqueName(normalized, "Routine", routine);
                }

                routine.Order = index;
                routine.ResetTime = routine.ResetTime;
                ChecklistService.Normalize(routine);
            }

            if (!source.SequenceEqual(normalized))
            {
                trackedGame.Routines = new ObservableCollection<RoutineSettings>(normalized);
            }

            trackedGame.NotifyOverallStateChanged();
        }

        public static TaskState GetOverallState(TrackedGameSettings trackedGame)
        {
            if (trackedGame?.Routines == null)
            {
                return TaskState.COMPLETE;
            }

            return trackedGame.Routines.Any(a =>
                a != null &&
                a.CountTowardOverallTaskStatus &&
                a.CurrentState == TaskState.INCOMPLETE)
                    ? TaskState.INCOMPLETE
                    : TaskState.COMPLETE;
        }

        public static IReadOnlyList<RoutineSettings> GetParticipatingRoutines(
            TrackedGameSettings trackedGame)
        {
            return (trackedGame?.Routines ?? new ObservableCollection<RoutineSettings>())
                .Where(a => a != null && a.CountTowardOverallTaskStatus)
                .OrderBy(a => a.Order)
                .ToList();
        }

        public static bool TryValidateName(
            TrackedGameSettings trackedGame,
            Guid routineId,
            string value,
            out string normalizedName,
            out string error)
        {
            normalizedName = RoutineSettings.NormalizeName(value);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                error = "Routine name is required.";
                return false;
            }

            if ((value ?? string.Empty).Trim().Length > RoutineSettings.MaximumNameLength)
            {
                error = $"Routine names cannot exceed {RoutineSettings.MaximumNameLength} characters.";
                return false;
            }

            var nameToCompare = normalizedName;
            if (trackedGame?.Routines?.Any(a =>
                    a != null &&
                    a.Id != routineId &&
                    string.Equals(a.Name, nameToCompare, StringComparison.OrdinalIgnoreCase)) == true)
            {
                error = $"A routine named \"{normalizedName}\" already exists for this game.";
                return false;
            }

            error = null;
            return true;
        }

        public static string GenerateUniqueName(
            IEnumerable<RoutineSettings> routines,
            string baseName = "Routine",
            RoutineSettings excludedRoutine = null)
        {
            var usedNames = new HashSet<string>(
                (routines ?? Enumerable.Empty<RoutineSettings>())
                    .Where(a => a != null && !ReferenceEquals(a, excludedRoutine))
                    .Select(a => a.Name ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);

            for (var number = 1; ; number++)
            {
                var candidate = RoutineSettings.NormalizeName($"{baseName} {number}");
                if (!usedNames.Contains(candidate))
                {
                    return candidate;
                }
            }
        }

        public static bool MoveRoutine(
            TrackedGameSettings trackedGame,
            Guid routineId,
            int offset)
        {
            if (trackedGame?.Routines == null || offset == 0)
            {
                return false;
            }

            var ordered = GetCanonicalOrder(trackedGame.Routines);
            var oldIndex = ordered.FindIndex(a => a.Id == routineId);
            if (oldIndex < 0)
            {
                return false;
            }

            var newIndex = Math.Max(0, Math.Min(ordered.Count - 1, oldIndex + offset));
            if (oldIndex == newIndex)
            {
                return false;
            }

            var routine = ordered[oldIndex];
            ordered.RemoveAt(oldIndex);
            ordered.Insert(newIndex, routine);

            // Publish the final canonical order before ObservableCollection raises any
            // Move events. Checklist views rebuild and sort on those events, so they
            // must never observe the previous Order values during a move.
            for (var index = 0; index < ordered.Count; index++)
            {
                ordered[index].Order = index;
            }

            EnsureCollectionMatchesOrder(trackedGame.Routines, ordered);

            return true;
        }

        private static List<RoutineSettings> GetCanonicalOrder(
            IEnumerable<RoutineSettings> routines)
        {
            return routines
                .Select((routine, index) => new { Routine = routine, OriginalIndex = index })
                .Where(a => a.Routine != null)
                .OrderBy(a => a.Routine.Order)
                .ThenBy(a => a.OriginalIndex)
                .Select(a => a.Routine)
                .ToList();
        }

        private static void EnsureCollectionMatchesOrder(
            ObservableCollection<RoutineSettings> routines,
            IReadOnlyList<RoutineSettings> ordered)
        {
            var collectionChanged = false;
            for (var targetIndex = 0; targetIndex < ordered.Count; targetIndex++)
            {
                var currentIndex = routines.IndexOf(ordered[targetIndex]);
                if (currentIndex >= 0 && currentIndex != targetIndex)
                {
                    routines.Move(currentIndex, targetIndex);
                    collectionChanged = true;
                }
            }

            if (!collectionChanged && ordered.Count > 0)
            {
                // The raw collection can already match the new order while a sorted
                // checklist view still reflects the previous Order values. Emit one
                // no-op Move after final values are assigned so every open view resorts.
                routines.Move(0, 0);
            }
        }
    }
}
