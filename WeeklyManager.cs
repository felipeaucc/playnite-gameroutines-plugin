using Playnite.SDK;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Windows.Controls;

namespace WeeklyManager
{
    public class WeeklyManager : GenericPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();
        private const string TestNotificationId = "WeeklyManager_Test";

        private readonly IPlayniteAPI playniteApi;
        private readonly WeeklyManagerSettingsViewModel settings;

        public override Guid Id { get; } = Guid.Parse("cb076ecb-ea40-4036-8094-f1c554566b49");

        public WeeklyManager(IPlayniteAPI api) : base(api)
        {
            playniteApi = api;
            settings = new WeeklyManagerSettingsViewModel(this);
            Properties = new GenericPluginProperties
            {
                HasSettings = true
            };

            logger.Info("Weekly Manager initialized.");
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new WeeklyManagerSettingsView();
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem
            {
                MenuSection = "@",
                Description = "Weekly Manager - Test",
                Action = _ =>
                {
                    logger.Info("Weekly Manager test notification requested.");
                    playniteApi.Notifications.Add(new NotificationMessage(
                        TestNotificationId,
                        "Weekly Manager\r\nPlugin test successful.",
                        NotificationType.Info));
                }
            };
        }
    }
}
