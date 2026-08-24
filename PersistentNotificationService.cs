using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;

namespace GameRoutines
{
    public sealed class PendingGameRoutinesNotification
    {
        public string Id { get; set; }
        public Guid GameId { get; set; }
        public Guid? RoutineId { get; set; }
        public string Source { get; set; }
        public DateTime OccurrenceLocal { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public DateTime CreatedLocal { get; set; }
        public NotificationType Type { get; set; } = NotificationType.Info;

        [DontSerialize]
        public string DisplayText => string.IsNullOrWhiteSpace(Message)
            ? Title ?? string.Empty
            : $"{Title}\r\n{Message}";
    }

    internal sealed class PendingNotificationLedger
    {
        public List<PendingGameRoutinesNotification> Notifications { get; set; } =
            new List<PendingGameRoutinesNotification>();
    }

    internal sealed class PersistentNotificationService : IDisposable
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private readonly INotificationsAPI notifications;
        private readonly string ledgerPath;
        private readonly Dictionary<string, NotificationMessage> activeMessages =
            new Dictionary<string, NotificationMessage>(StringComparer.Ordinal);
        private PendingNotificationLedger ledger = new PendingNotificationLedger();
        private bool isStarted;
        private bool isShuttingDown;

        public PersistentNotificationService(GameRoutines plugin)
        {
            if (plugin == null)
            {
                throw new ArgumentNullException(nameof(plugin));
            }

            notifications = plugin.PlayniteApi.Notifications;
            ledgerPath = Path.Combine(plugin.GetPluginUserDataPath(), "pending-notifications.json");
        }

        public void Start()
        {
            if (isStarted)
            {
                return;
            }

            isStarted = true;
            isShuttingDown = false;
            LoadLedger();
            notifications.Messages.CollectionChanged += Messages_CollectionChanged;
            RestorePendingNotifications();
        }

        public void Post(
            string id,
            Guid gameId,
            Guid? routineId,
            string source,
            DateTime occurrenceLocal,
            string title,
            string message,
            NotificationType type)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A stable notification ID is required.", nameof(id));
            }

            var pending = ledger.Notifications.FirstOrDefault(a =>
                string.Equals(a.Id, id, StringComparison.Ordinal));
            if (pending == null)
            {
                pending = new PendingGameRoutinesNotification
                {
                    Id = id,
                    GameId = gameId,
                    RoutineId = routineId,
                    Source = source,
                    OccurrenceLocal = DateTime.SpecifyKind(occurrenceLocal, DateTimeKind.Local),
                    Title = title,
                    Message = message,
                    CreatedLocal = DateTime.Now,
                    Type = type
                };
                ledger.Notifications.Add(pending);
                SaveLedger();
            }

            EnsureActive(pending);
        }

        public void Dispose()
        {
            Stop();
        }

        public void Stop()
        {
            if (!isStarted)
            {
                return;
            }

            isShuttingDown = true;
            notifications.Messages.CollectionChanged -= Messages_CollectionChanged;
            foreach (var message in activeMessages.Values.ToList())
            {
                message.Closed -= Message_Closed;
            }

            activeMessages.Clear();
            isStarted = false;
        }

        private void LoadLedger()
        {
            try
            {
                if (!File.Exists(ledgerPath))
                {
                    ledger = new PendingNotificationLedger();
                    return;
                }

                ledger = Serialization.FromJsonFile<PendingNotificationLedger>(ledgerPath) ??
                    new PendingNotificationLedger();
                ledger.Notifications = (ledger.Notifications ?? new List<PendingGameRoutinesNotification>())
                    .Where(a => a != null && !string.IsNullOrWhiteSpace(a.Id))
                    .GroupBy(a => a.Id, StringComparer.Ordinal)
                    .Select(a => a.First())
                    .ToList();
            }
            catch (Exception exception)
            {
                logger.Error(exception, $"Failed to load pending Game Routines notifications from {ledgerPath}.");
                ledger = new PendingNotificationLedger();
            }
        }

        private void SaveLedger()
        {
            try
            {
                var directory = Path.GetDirectoryName(ledgerPath);
                Directory.CreateDirectory(directory);
                var temporaryPath = ledgerPath + ".tmp";
                File.WriteAllText(temporaryPath, Serialization.ToJson(ledger, true));
                if (File.Exists(ledgerPath))
                {
                    File.Replace(temporaryPath, ledgerPath, null);
                }
                else
                {
                    File.Move(temporaryPath, ledgerPath);
                }
            }
            catch (Exception exception)
            {
                logger.Error(exception, $"Failed to save pending Game Routines notifications to {ledgerPath}.");
            }
        }

        private void RestorePendingNotifications()
        {
            foreach (var pending in ledger.Notifications.ToList())
            {
                EnsureActive(pending);
            }
        }

        private void EnsureActive(PendingGameRoutinesNotification pending)
        {
            if (activeMessages.ContainsKey(pending.Id))
            {
                return;
            }

            var existing = notifications.Messages.FirstOrDefault(a =>
                string.Equals(a?.Id, pending.Id, StringComparison.Ordinal));
            if (existing != null)
            {
                Attach(existing);
                return;
            }

            var message = new NotificationMessage(pending.Id, pending.DisplayText, pending.Type);
            Attach(message);
            notifications.Add(message);
        }

        private void Attach(NotificationMessage message)
        {
            if (message == null || string.IsNullOrWhiteSpace(message.Id))
            {
                return;
            }

            if (activeMessages.TryGetValue(message.Id, out var current))
            {
                if (ReferenceEquals(current, message))
                {
                    return;
                }

                current.Closed -= Message_Closed;
            }

            activeMessages[message.Id] = message;
            message.Closed += Message_Closed;
        }

        private void Message_Closed(object sender, EventArgs args)
        {
            if (isShuttingDown || !(sender is NotificationMessage message))
            {
                return;
            }

            RemovePending(message.Id);
        }

        private void Messages_CollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if (args.OldItems != null)
            {
                foreach (NotificationMessage message in args.OldItems)
                {
                    if (message == null || !activeMessages.TryGetValue(message.Id, out var active) ||
                        !ReferenceEquals(active, message))
                    {
                        continue;
                    }

                    message.Closed -= Message_Closed;
                    activeMessages.Remove(message.Id);
                    if (!isShuttingDown)
                    {
                        RemovePending(message.Id);
                    }
                }
            }

            if (args.NewItems != null)
            {
                foreach (NotificationMessage message in args.NewItems)
                {
                    if (message != null && ledger.Notifications.Any(a =>
                        string.Equals(a.Id, message.Id, StringComparison.Ordinal)))
                    {
                        Attach(message);
                    }
                }
            }

            if (args.Action == NotifyCollectionChangedAction.Reset && !isShuttingDown)
            {
                var remainingIds = new HashSet<string>(
                    notifications.Messages.Where(a => a != null).Select(a => a.Id),
                    StringComparer.Ordinal);
                var removedIds = ledger.Notifications
                    .Where(a => !remainingIds.Contains(a.Id))
                    .Select(a => a.Id)
                    .ToList();
                foreach (var id in removedIds)
                {
                    RemovePending(id);
                }
            }
        }

        private void RemovePending(string id)
        {
            var removed = ledger.Notifications.RemoveAll(a =>
                string.Equals(a.Id, id, StringComparison.Ordinal)) > 0;
            if (removed)
            {
                SaveLedger();
            }
        }
    }
}
