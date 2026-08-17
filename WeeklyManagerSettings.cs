using Playnite.SDK;
using Playnite.SDK.Data;
using System.Collections.Generic;

namespace WeeklyManager
{
    public class WeeklyManagerSettings : ObservableObject
    {
    }

    public class WeeklyManagerSettingsViewModel : ObservableObject, ISettings
    {
        private readonly WeeklyManager plugin;
        private WeeklyManagerSettings editingClone;
        private WeeklyManagerSettings settings;

        public WeeklyManagerSettings Settings
        {
            get => settings;
            set
            {
                settings = value;
                OnPropertyChanged();
            }
        }

        public WeeklyManagerSettingsViewModel(WeeklyManager plugin)
        {
            this.plugin = plugin;
            Settings = plugin.LoadPluginSettings<WeeklyManagerSettings>() ?? new WeeklyManagerSettings();
        }

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(Settings);
        }

        public void CancelEdit()
        {
            Settings = editingClone;
        }

        public void EndEdit()
        {
            plugin.SavePluginSettings(Settings);
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            return true;
        }
    }
}
