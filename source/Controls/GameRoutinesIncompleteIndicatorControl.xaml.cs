using Playnite.SDK.Controls;
using Playnite.SDK.Models;
using System;
using System.Windows;

namespace GameRoutines
{
    public partial class GameRoutinesIncompleteIndicatorControl : PluginUserControl
    {
        private const double ReferenceRenderedCoverHeight = 220.0;
        private const double ReferenceRenderedCoverWidth = ReferenceRenderedCoverHeight * 2.0 / 3.0;
        private const double ReferenceHorizontalMargin = 12.0;
        private const double ReferenceLineThickness = 3.0;
        private const double ReferenceGlowBlurRadius = 8.0;
        private const double MinimumScale = 0.5;
        private const double MaximumScale = 2.0;

        private readonly GameRoutines plugin;
        private GameTaskStateViewModel viewModel;

        internal GameRoutinesIncompleteIndicatorControl(GameRoutines plugin)
        {
            this.plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            InitializeComponent();
            Unloaded += (sender, args) => DisposeViewModel();
            Loaded += (sender, args) =>
            {
                BindGame(GameContext);
                UpdateIndicatorScale();
            };
            SizeChanged += (sender, args) => UpdateIndicatorScale();
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

        private void UpdateIndicatorScale()
        {
            if (ActualWidth <= 0 || double.IsNaN(ActualWidth) || double.IsInfinity(ActualWidth))
            {
                return;
            }

            var scale = Math.Max(
                MinimumScale,
                Math.Min(MaximumScale, ActualWidth / ReferenceRenderedCoverWidth));
            var margin = ReferenceHorizontalMargin * scale;

            IncompleteIndicatorLine.Margin = new Thickness(margin, 0, margin, 0);
            IncompleteIndicatorLine.Height = ReferenceLineThickness * scale;
            if (IncompleteIndicatorLine.Effect is System.Windows.Media.Effects.DropShadowEffect glow)
            {
                glow.BlurRadius = ReferenceGlowBlurRadius * scale;
            }
        }
    }
}
