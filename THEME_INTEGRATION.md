# Theme integration

Game Routines' core routine, checklist, scheduling, reminder, and notification logic operates independently of any Playnite Desktop theme. Themes can optionally host the extension's three custom UI controls by providing suitable hosts, resources, and layout. Version 0.9.0 was developed and tested primarily around [FusionX](https://github.com/sakasakiking/FusionX), and this repository's 2.1.1 reference files are the current supported and tested example. Other Desktop themes are not yet officially supported and require their own integration, styling, and runtime validation before official support can be claimed.

For Playnite's host naming convention and installation checks, see the official [Integrating extension elements](https://api.playnite.link/docs/tutorials/themes/extensionIntegration.html) documentation.

## Registration

- Add-on ID: `GameRoutines_cb076ecb-ea40-4036-8094-f1c554566b49`
- Source name: `GameRoutines`

| Element name | ContentControl name | Intended purpose |
| --- | --- | --- |
| `Checklist` | `GameRoutines_Checklist` | Multi-routine checklist for the currently bound game. Best hosted in a game details tab or another scrollable content region. |
| `StateToggle` | `GameRoutines_StateToggle` | Compact overall **COMPLETE** / **INCOMPLETE** control, with settings and Custom Reminder actions. Best hosted in a game action or status area. |
| `IncompleteIndicator` | `GameRoutines_IncompleteIndicator` | Non-interactive red incomplete-state marker. Best overlaid across the top of a game's cover region. |

The host name is formed from `<SourceName>_<ElementName>`, for example:

```xml
<ContentControl x:Name="GameRoutines_Checklist" />
```

The availability of these elements does not by itself make a theme officially compatible with Game Routines. Theme authors must provide suitable hosts, resources and layout, then validate the resulting integration.

## Behavior expectations

- Each control receives the current game through Playnite's `PluginUserControl.GameContext`; themes should not provide game IDs or game-specific configuration.
- `Checklist` and `StateToggle` collapse when the current game is not tracked.
- `IncompleteIndicator` collapses unless the game is tracked, its derived overall status is **INCOMPLETE**, and both the global and per-game indicator options are enabled.
- `IncompleteIndicator` is not hit-testable and should be placed in an overlay layer that does not replace or intercept the theme's cover controls.
- `StateToggle` is a compact 60-pixel-wide control designed for an action area. Its appearance should be checked against the host theme's dynamic control resources.
- `Checklist` includes actions for creating and deleting routines, managing items, changing routine state and opening focused or all-routine pop-out windows. Give it enough width and height for interactive content.
- Themes should continue to work when Game Routines is not installed. Use Playnite's documented plugin-status facilities where conditional surrounding layout is needed.
- These controls are currently documented for Desktop themes; no Fullscreen-theme compatibility is claimed.

## [FusionX](https://github.com/sakasakiking/FusionX) reference example

[`Integrations/FusionX/2.1.1/`](Integrations/FusionX/2.1.1/) demonstrates all three controls in real game overview views:

- `Checklist` is hosted in a **Checklists** details tab.
- `StateToggle` is hosted beside [FusionX](https://github.com/sakasakiking/FusionX)'s other compact game actions.
- `IncompleteIndicator` is layered over the cover without replacing [FusionX](https://github.com/sakasakiking/FusionX)'s stock cover structure.

These reference files target [FusionX](https://github.com/sakasakiking/FusionX) 2.1.1 only and are used to develop and test the currently supported embedded UI. They also document the version-specific changes as material for a possible future upstream contribution. Reuse the Game Routines hosts and behavior, not unrelated version-specific theme markup, when developing support for another theme or [FusionX](https://github.com/sakasakiking/FusionX) version. Official support is not claimed until an implementation is styled and tested for its target theme.
