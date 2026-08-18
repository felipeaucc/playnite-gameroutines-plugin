# Weekly Manager integration for FusionX 2.1.1

These files are version-scoped source copies of the two FusionX 2.1.1 views that host Weekly Manager's supported Playnite custom elements. They were derived from the installed FusionX 2.1.1 files whose original SHA-256 hashes were:

- `Views/DetailsViewGameOverview.xaml`: `9261DEBE98EB34AE8CDB48DE323457AA74D2A03DD90EA83E91CA636816E664DA`
- `Views/GridViewGameOverview.xaml`: `4679CB90EE4A8E620463C188AC5E7CDE70CD114976BC6AB3236D71AE2E5E0505`

Both views add the same three placeholders:

- `WeeklyManager_Checklist` in a **Checklist** tab immediately after Notes;
- `WeeklyManager_StateToggle` in the upper compact-action group immediately beside `VIDEO ON`; and
- `WeeklyManager_IncompleteIndicator` as a non-interactive overlay on the cover container.

Each host derives its visibility from the injected control. As a result, the tab, state control, and indicator collapse for untracked games and also collapse safely if Weekly Manager is not installed. The plugin controls use Playnite's `PluginUserControl.GameContext` and authoritative `Game.Id`; the theme contains no game-specific configuration.

The fixed 60 px `TASKS` control reuses FusionX's native `ActionControlPanel`, `SwitcherToggleButton`, and `ActionControlLabelStyle` resources, including the standard neutral border, corner radius, padding, typography, toggle animation, and hover treatment. Hovering the card reveals one separated reload action with an X/check state overlay that toggles the existing per-game automatic checklist completion setting. At the 220 px reference cover height (146.67 px reference width), the incomplete indicator is a 3 px `#FF2C2C` straight line directly on the cover's flat top edge, inset 12 px on both sides, with a zero-depth `#FF2C2C` glow (`BlurRadius=8`, `Opacity=0.7`). Its inset, thickness, and blur radius scale proportionally with the rendered cover width. It is rendered as an independent same-cell sibling overlay with its own 16 px opacity mask, leaving FusionX's stock cover Grid, cover-wide opacity mask, image container, and BackgroundChanger host unchanged.

## Compatibility and updates

The copies in `Views` target FusionX **2.1.1** only. Do not replace files from another FusionX version blindly. Compare and reapply the three small host sections to the newer view structure instead.

A FusionX update can overwrite the active theme files. Native upstream FusionX support for the three placeholders is the most resilient long-term solution. Weekly Manager itself does not require FusionX and does not include an automatic theme patcher.

Before the development integration was applied, the active files were backed up under Playnite's user-data `ThemeBackups/WeeklyManager/FusionX_2.1.1_pre-integration/Views` directory. Restoring those two backups removes only the FusionX host markup; it does not affect Weekly Manager data.
