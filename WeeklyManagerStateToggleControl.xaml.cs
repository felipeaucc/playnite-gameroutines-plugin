using Playnite.SDK.Controls;
using Playnite.SDK.Models;
using System;

namespace WeeklyManager
{
    public partial class WeeklyManagerStateToggleControl : PluginUserControl
    {
        private readonly WeeklyManager plugin;
        private WeeklyGameStateViewModel viewModel;

        internal WeeklyManagerStateToggleControl(WeeklyManager plugin)
        {
            this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            InitializeComponent();
            Unloaded += (sender, args) => DisposeViewModel();
            Loaded += (sender, args) => BindGame(GameContext);
        }

        public override void GameContextChanged(Game oldContext, Game newContext)
        {
            BindGame(newContext);
        }

        private void BindGame(Game game)
        {
            DisposeViewModel();
            if (game == null)
            {
                DataContext = null;
                return;
            }

            viewModel = new WeeklyGameStateViewModel(plugin, game.Id);
            DataContext = viewModel;
        }

        private void DisposeViewModel()
        {
            viewModel?.Dispose();
            viewModel = null;
        }
    }
}
