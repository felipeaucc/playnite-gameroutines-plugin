using Playnite.SDK.Controls;
using Playnite.SDK.Models;
using System;

namespace GameRoutines
{
    public partial class GameRoutinesChecklistControl : PluginUserControl
    {
        private readonly GameRoutines plugin;
        private GameChecklistViewModel viewModel;

        internal GameRoutinesChecklistControl(GameRoutines plugin)
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
                return;
            }

            DisposeViewModel();
            if (game == null)
            {
                DataContext = null;
                return;
            }

            viewModel = new GameChecklistViewModel(plugin, game.Id);
            DataContext = viewModel;
        }

        private void DisposeViewModel()
        {
            viewModel?.Dispose();
            viewModel = null;
        }
    }
}
