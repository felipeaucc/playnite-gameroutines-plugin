using Playnite.SDK.Controls;
using Playnite.SDK.Models;
using System;

namespace GameRoutines
{
    public partial class GameRoutinesStateToggleControl : PluginUserControl
    {
        private readonly GameRoutines plugin;
        private GameTaskStateViewModel viewModel;

        internal GameRoutinesStateToggleControl(GameRoutines plugin)
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

            viewModel = new GameTaskStateViewModel(plugin, game.Id);
            DataContext = viewModel;
        }

        private void DisposeViewModel()
        {
            viewModel?.Dispose();
            viewModel = null;
        }
    }
}
