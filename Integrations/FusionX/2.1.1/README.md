# Enhanced integration for FusionX 2.1.1

FusionX 2.1.1 is the supported and tested Desktop-theme target for Game Routines 0.9.0. This folder provides the version-specific integration by adding Game Routines hosts to two FusionX 2.1.1 views. It does not imply that FusionX officially supports Game Routines today.

The integration modifies only:

- `Views/DetailsViewGameOverview.xaml`
- `Views/GridViewGameOverview.xaml`

The copies are derived from the official FusionX 2.1.1 package. The pristine files used as the comparison baseline have these SHA-256 hashes:

- `Views/DetailsViewGameOverview.xaml`: `9261DEBE98EB34AE8CDB48DE323457AA74D2A03DD90EA83E91CA636816E664DA`
- `Views/GridViewGameOverview.xaml`: `4679CB90EE4A8E620463C188AC5E7CDE70CD114976BC6AB3236D71AE2E5E0505`

FusionX is a third-party project. See [LICENSE.FusionX.txt](LICENSE.FusionX.txt) for its MIT license and attribution.

## Added integration

The two modified views add the following Game Routines-specific hosts:

- `GameRoutines_StateToggle`, a compact **TASKS** control beside FusionX's `VIDEO ON` control. It shows the combined COMPLETE/INCOMPLETE state and exposes hover actions for Game Routines Settings and Custom Reminder.
- `GameRoutines_Checklist`, a **Checklists** tab after Notes. It embeds the multi-routine checklist UI, including New Checklist and Delete Checklist actions supplied by the plugin.
- `GameRoutines_IncompleteIndicator`, a non-interactive red indicator on the cover when the game's overall counted routine state is incomplete.

Each host derives visibility from its injected Game Routines control. The controls collapse for untracked games and fail safely when Game Routines is not installed. They use Playnite's `PluginUserControl.GameContext` and authoritative `Game.Id`; the theme contains no game-specific configuration.

The compact control reuses FusionX's native action-control resources and layout. The cover indicator is rendered as an independent, non-interactive overlay so FusionX's stock cover grid, opacity mask, image container, and BackgroundChanger host remain unchanged.

## Manual installation

These files target FusionX **2.1.1 only**. Before installing them manually, close Playnite and back up the active theme's matching Details and Grid view files. Then replace only those two files with the copies in this folder.

Do not copy these files into another FusionX version without first comparing that version's view structure and reapplying only the three Game Routines host sections. A FusionX update may overwrite manually installed integration files. Game Routines does not patch the theme automatically.

The minimal host changes are intended to remain suitable for a future upstream contribution to the official [FusionX repository](https://github.com/sakasakiking/FusionX).
