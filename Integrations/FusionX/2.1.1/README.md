# Enhanced integration for [FusionX](https://github.com/sakasakiking/FusionX) 2.1.1

[FusionX](https://github.com/sakasakiking/FusionX) 2.1.1 is the supported and tested Desktop-theme target for Game Routines 0.9.0. This directory is the manual integration referenced by the main project documentation: it adds Game Routines hosts to two [FusionX](https://github.com/sakasakiking/FusionX) 2.1.1 views. There is no separate downloadable integration package. The two XAML files in this directory are the manual integration; they are not included in the Game Routines `.pext`. Their inclusion here does not imply that [FusionX](https://github.com/sakasakiking/FusionX) officially supports Game Routines today.

The manual integration modifies only:

- `Views/DetailsViewGameOverview.xaml`
- `Views/GridViewGameOverview.xaml`

The copies are derived from the official [FusionX](https://github.com/sakasakiking/FusionX) 2.1.1 package. The pristine files used as the comparison baseline have these SHA-256 hashes:

- `Views/DetailsViewGameOverview.xaml`: `9261DEBE98EB34AE8CDB48DE323457AA74D2A03DD90EA83E91CA636816E664DA`
- `Views/GridViewGameOverview.xaml`: `4679CB90EE4A8E620463C188AC5E7CDE70CD114976BC6AB3236D71AE2E5E0505`

[FusionX](https://github.com/sakasakiking/FusionX) is a third-party project. See [LICENSE.FusionX.txt](LICENSE.FusionX.txt) for its MIT license and attribution.

## Added integration

The two modified views add the following Game Routines-specific hosts:

- `GameRoutines_StateToggle`, a compact tasks completion toggle control beside the game cover. The control also provides convenient shortcuts to Game Routines Settings and Custom Reminder actions; those underlying features remain accessible independently of the [FusionX](https://github.com/sakasakiking/FusionX) integration.
- `GameRoutines_Checklist`, a **Checklists** tab after Notes. It embeds the multi-routine checklist UI, including New Checklist and Delete Checklist actions supplied by the plugin.
- `GameRoutines_IncompleteIndicator`, a non-interactive red indicator on the cover when the game's overall counted routine state is incomplete.

Each host derives visibility from its injected Game Routines control. The controls collapse for untracked games and fail safely when Game Routines is not installed. They use Playnite's `PluginUserControl.GameContext` and authoritative `Game.Id`; the theme contains no game-specific configuration.

The compact control reuses [FusionX](https://github.com/sakasakiking/FusionX)'s native action-control resources and layout. The cover indicator is rendered as an independent, non-interactive overlay so [FusionX](https://github.com/sakasakiking/FusionX)'s stock cover grid, opacity mask, image container, and BackgroundChanger host remain unchanged.

## Manual installation

These files target [FusionX](https://github.com/sakasakiking/FusionX) **2.1.1 only**. Before installing them manually, close Playnite and back up the active theme's matching Details and Grid view files. Then replace only those two files with the copies in this folder.

Do not copy these files into another [FusionX](https://github.com/sakasakiking/FusionX) version without first comparing that version's view structure and reapplying only the three Game Routines host sections. A [FusionX](https://github.com/sakasakiking/FusionX) update may overwrite manually installed integration files. Game Routines does not patch the theme automatically.

The minimal host changes are intended to remain suitable for a future upstream contribution to the official [FusionX repository](https://github.com/sakasakiking/FusionX).
