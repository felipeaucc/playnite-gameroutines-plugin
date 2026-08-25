using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GameRoutines
{
    public struct ChecklistProgress
    {
        public ChecklistProgress(int completed, int total)
        {
            Completed = completed;
            Total = total;
        }

        public int Completed { get; }

        public int Total { get; }

        public bool IsComplete => Completed == Total;
    }

    public static class ChecklistService
    {
        public static void Normalize(RoutineSettings routine)
        {
            if (routine == null)
            {
                return;
            }

            var source = routine.Checklist ?? new ObservableCollection<ChecklistItemSettings>();
            var usedIds = new HashSet<Guid>();
            var normalized = source
                .Where(a => a != null)
                .Select((item, index) => new { Item = item, OriginalIndex = index })
                .Where(a => !string.IsNullOrWhiteSpace(a.Item.Text))
                .OrderBy(a => a.Item.Order)
                .ThenBy(a => a.OriginalIndex)
                .Select(a => a.Item)
                .ToList();

            for (var index = 0; index < normalized.Count; index++)
            {
                var item = normalized[index];
                item.Text = item.Text;
                if (item.Id == Guid.Empty || !usedIds.Add(item.Id))
                {
                    item.Id = Guid.NewGuid();
                    usedIds.Add(item.Id);
                }

                item.Order = index;
            }

            if (!source.SequenceEqual(normalized))
            {
                routine.Checklist = new ObservableCollection<ChecklistItemSettings>(normalized);
            }
        }

        public static ChecklistItemSettings AddItem(RoutineSettings routine, string text)
        {
            if (routine == null)
            {
                return null;
            }

            var normalizedText = ChecklistItemSettings.NormalizeText(text);
            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                return null;
            }

            Normalize(routine);
            var item = new ChecklistItemSettings
            {
                Id = Guid.NewGuid(),
                Text = normalizedText,
                IsChecked = false,
                Order = routine.Checklist.Count
            };
            routine.Checklist.Add(item);
            return item;
        }

        public static bool EditItem(RoutineSettings routine, Guid itemId, string text)
        {
            var item = FindItem(routine, itemId);
            var normalizedText = ChecklistItemSettings.NormalizeText(text);
            if (item == null || string.IsNullOrWhiteSpace(normalizedText))
            {
                return false;
            }

            item.Text = normalizedText;
            return true;
        }

        public static bool DeleteItem(RoutineSettings routine, Guid itemId)
        {
            var item = FindItem(routine, itemId);
            if (item == null)
            {
                return false;
            }

            routine.Checklist.Remove(item);
            UpdateOrder(routine);
            return true;
        }

        public static bool MoveItem(RoutineSettings routine, Guid itemId, int offset)
        {
            var item = FindItem(routine, itemId);
            if (item == null || offset == 0)
            {
                return false;
            }

            var oldIndex = routine.Checklist.IndexOf(item);
            var newIndex = Math.Max(0, Math.Min(routine.Checklist.Count - 1, oldIndex + offset));
            if (newIndex == oldIndex)
            {
                return false;
            }

            routine.Checklist.Move(oldIndex, newIndex);
            UpdateOrder(routine);
            return true;
        }

        public static bool SetItemChecked(RoutineSettings routine, Guid itemId, bool isChecked)
        {
            var item = FindItem(routine, itemId);
            if (item == null)
            {
                return false;
            }

            item.IsChecked = isChecked;
            return true;
        }

        public static bool Reset(RoutineSettings routine)
        {
            if (routine == null)
            {
                return false;
            }

            var changed = false;
            foreach (var item in routine.Checklist)
            {
                if (item != null && item.IsChecked)
                {
                    item.IsChecked = false;
                    changed = true;
                }
            }

            return changed;
        }

        public static ChecklistProgress GetProgress(RoutineSettings routine)
        {
            var items = routine?.Checklist?.Where(a => a != null).ToList() ??
                new List<ChecklistItemSettings>();
            return new ChecklistProgress(items.Count(a => a.IsChecked), items.Count);
        }

        private static ChecklistItemSettings FindItem(RoutineSettings routine, Guid itemId)
        {
            return routine?.Checklist?.FirstOrDefault(a => a != null && a.Id == itemId);
        }

        private static void UpdateOrder(RoutineSettings routine)
        {
            for (var index = 0; index < routine.Checklist.Count; index++)
            {
                routine.Checklist[index].Order = index;
            }
        }
    }
}
