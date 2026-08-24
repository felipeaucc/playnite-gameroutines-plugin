# Game Routines integration for FusionX 2.1.1

These files are version-scoped source copies of the FusionX 2.1.1 views that host Game Routines' supported Playnite custom elements and the notification badge visual. They were derived from the installed FusionX 2.1.1 files whose original SHA-256 hashes were:

- `Views/DetailsViewGameOverview.xaml`: `9261DEBE98EB34AE8CDB48DE323457AA74D2A03DD90EA83E91CA636816E664DA`
- `Views/GridViewGameOverview.xaml`: `4679CB90EE4A8E620463C188AC5E7CDE70CD114976BC6AB3236D71AE2E5E0505`
- `Views/TopPanel.xaml`: `463D9BDE0C1CAE1DFCA24D0757AF6D64A235CDBA469144C100071C4A504E73BB`

Both views add the same three placeholders:

- `GameRoutines_Checklist` in a **Checklists** tab immediately after Notes;
- `GameRoutines_StateToggle` in the upper compact-action group immediately beside `VIDEO ON`; and
- `GameRoutines_IncompleteIndicator` as a non-interactive overlay on the cover container.

Each host derives its visibility from the injected control. As a result, the tab, state control, and indicator collapse for untracked games and also collapse safely if Game Routines is not installed. The plugin controls use Playnite's `PluginUserControl.GameContext` and authoritative `Game.Id`; the theme contains no game-specific configuration. `Views/TopPanel.xaml` preserves FusionX's notification bell, badge geometry, and number styling while changing only the badge background to the Game Routines blue-to-purple gradient (`#2090F0` to `#9000F0`).

The fixed 60 px `TASKS` control reuses FusionX's native `ActionControlPanel`, `SwitcherToggleButton`, and `ActionControlLabelStyle` resources, including the standard neutral border, corner radius, padding, typography, toggle animation, and hover treatment. It represents the derived status of routines counted toward overall task status. Hovering the card reveals compact actions that open Game Routines settings focused on the current `Game.Id` and configure that game's Custom Reminder. Automatic checklist completion is controlled per routine from the stacked checklist cards, using the preserved loop icon with its mini X/check state marker. At the 220 px reference cover height (146.67 px reference width), the incomplete indicator is a 3 px `#FF2C2C` straight line directly on the cover's flat top edge, inset 12 px on both sides, with a zero-depth `#FF2C2C` glow (`BlurRadius=8`, `Opacity=0.7`). Its inset, thickness, and blur radius scale proportionally with the rendered cover width. It is rendered as an independent same-cell sibling overlay with its own 16 px opacity mask, leaving FusionX's stock cover Grid, cover-wide opacity mask, image container, and BackgroundChanger host unchanged.

## Compatibility and updates

The copies in `Views` target FusionX **2.1.1** only. Do not replace files from another FusionX version blindly. Compare and reapply the three small host sections and notification-badge brush to the newer view structure instead.

A FusionX update can overwrite the active theme files. Native upstream FusionX support for the three placeholders is the most resilient long-term solution. Game Routines itself does not require FusionX and does not include an automatic theme patcher.

Before changing placeholder names in an active development theme, back up all three view files. Restoring those backups removes only the FusionX host markup; it does not affect Game Routines data.
