using Playnite.SDK;
using System;
using System.Collections.Generic;

namespace GameRoutines
{
    internal sealed class CustomReminderViewModel : ObservableObject
    {
        private readonly GameRoutines plugin;
        private readonly Guid gameId;

        public string GameName { get; }
        public TrackedGameSettings EditableReminder { get; }

        internal CustomReminderViewModel(GameRoutines plugin, Guid gameId)
        {
            this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            this.gameId = gameId;
            var trackedGame = plugin.GetTrackedGameSettings(gameId) ??
                throw new ArgumentException("The game is not tracked.", nameof(gameId));
            var game = plugin.PlayniteApi.Database.Games.Get(gameId);
            GameName = !string.IsNullOrWhiteSpace(game?.Name)
                ? game.Name
                : trackedGame.CachedGameName ?? "Game";
            EditableReminder = CustomReminderService.CreateEditableCopy(trackedGame);
        }

        internal bool TrySave()
        {
            var errors = new List<string>();
            CustomReminderService.Validate(EditableReminder, GameName, errors);
            if (errors.Count > 0)
            {
                plugin.PlayniteApi.Dialogs.ShowMessage(
                    string.Join(Environment.NewLine, errors),
                    "Game Routines",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return false;
            }

            return plugin.SaveCustomReminder(gameId, EditableReminder);
        }
    }
}
