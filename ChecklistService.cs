using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace WeeklyManager
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
        public static void Normalize(TrackedGameSettings trackedGame)
        {
            if (trackedGame == null)
            {
                return;
            }

            var source = trackedGame.Checklist ?? new ObservableCollection<ChecklistItemSettings>();
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
                trackedGame.Checklist = new ObservableCollection<ChecklistItemSettings>(normalized);
            }
        }

        public static ChecklistItemSettings AddItem(TrackedGameSettings trackedGame, string text)
        {
            if (trackedGame == null)
            {
                return null;
            }

            var normalizedText = ChecklistItemSettings.NormalizeText(text);
            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                return null;
            }

            Normalize(trackedGame);
            var item = new ChecklistItemSettings
            {
                Id = Guid.NewGuid(),
                Text = normalizedText,
                IsChecked = false,
                Order = trackedGame.Checklist.Count
            };
            trackedGame.Checklist.Add(item);
            return item;
        }

        public static bool EditItem(TrackedGameSettings trackedGame, Guid itemId, string text)
        {
            var item = FindItem(trackedGame, itemId);
            var normalizedText = ChecklistItemSettings.NormalizeText(text);
            if (item == null || string.IsNullOrWhiteSpace(normalizedText))
            {
                return false;
            }

            item.Text = normalizedText;
            return true;
        }

        public static bool DeleteItem(TrackedGameSettings trackedGame, Guid itemId)
        {
            var item = FindItem(trackedGame, itemId);
            if (item == null)
            {
                return false;
            }

            trackedGame.Checklist.Remove(item);
            UpdateOrder(trackedGame);
            return true;
        }

        public static bool MoveItem(TrackedGameSettings trackedGame, Guid itemId, int offset)
        {
            var item = FindItem(trackedGame, itemId);
            if (item == null || offset == 0)
            {
                return false;
            }

            var oldIndex = trackedGame.Checklist.IndexOf(item);
            var newIndex = Math.Max(0, Math.Min(trackedGame.Checklist.Count - 1, oldIndex + offset));
            if (newIndex == oldIndex)
            {
                return false;
            }

            trackedGame.Checklist.Move(oldIndex, newIndex);
            UpdateOrder(trackedGame);
            return true;
        }

        public static bool SetItemChecked(TrackedGameSettings trackedGame, Guid itemId, bool isChecked)
        {
            var item = FindItem(trackedGame, itemId);
            if (item == null)
            {
                return false;
            }

            item.IsChecked = isChecked;
            return true;
        }

        public static bool Reset(TrackedGameSettings trackedGame)
        {
            if (trackedGame == null)
            {
                return false;
            }

            var changed = false;
            foreach (var item in trackedGame.Checklist)
            {
                if (item != null && item.IsChecked)
                {
                    item.IsChecked = false;
                    changed = true;
                }
            }

            return changed;
        }

        public static ChecklistProgress GetProgress(TrackedGameSettings trackedGame)
        {
            var items = trackedGame?.Checklist?.Where(a => a != null).ToList() ??
                new List<ChecklistItemSettings>();
            return new ChecklistProgress(items.Count(a => a.IsChecked), items.Count);
        }

        private static ChecklistItemSettings FindItem(TrackedGameSettings trackedGame, Guid itemId)
        {
            return trackedGame?.Checklist?.FirstOrDefault(a => a != null && a.Id == itemId);
        }

        private static void UpdateOrder(TrackedGameSettings trackedGame)
        {
            for (var index = 0; index < trackedGame.Checklist.Count; index++)
            {
                trackedGame.Checklist[index].Order = index;
            }
        }
    }
}
