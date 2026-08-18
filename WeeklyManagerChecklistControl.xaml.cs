using Playnite.SDK.Controls;
using Playnite.SDK.Models;
using System;

namespace WeeklyManager
{
    public partial class WeeklyManagerChecklistControl : PluginUserControl
    {
        private readonly WeeklyManager plugin;
        private WeeklyChecklistViewModel viewModel;

        internal WeeklyManagerChecklistControl(WeeklyManager plugin)
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
            if (viewModel != null && game != null && viewModel.TrackedGame?.GameId == game.Id)
            {
                viewModel.RefreshStatus();
                return;
            }

            DisposeViewModel();
            if (game == null)
            {
                DataContext = null;
                return;
            }

            viewModel = new WeeklyChecklistViewModel(plugin, game.Id);
            DataContext = viewModel;
        }

        private void DisposeViewModel()
        {
            viewModel?.Dispose();
            viewModel = null;
        }
    }
}
