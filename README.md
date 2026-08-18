# Weekly Manager

Weekly Manager is a Playnite generic plugin for managing recurring weekly game activities, resets, reminders, status, and per-game checklists.

**Status:** Early development. Weekly Manager provides configurable weekly resets, INCOMPLETE/COMPLETE state, internal Playnite notifications, optional custom reminders, optional `Tasks Available!` tag synchronization, and recurring per-game checklists.

## Checklist Features

- Create an ordered checklist for each tracked game, with independent checked state.
- Check, edit, delete, and reorder checklist items from the extension settings.
- Open a focused checklist window from a tracked game's context menu or a supported theme to check off tasks without opening extension settings.
- Automatically clear checked items when that game's weekly reset is processed, including a missed reset detected after Playnite starts.
- Optionally mark a game's weekly state COMPLETE when every remaining checklist item is checked. With automatic completion enabled, an empty checklist is COMPLETE because no requirements remain.
- Reset a checklist manually without deleting its items.

## Other Features

- Configurable weekly reset schedules
- Playnite notifications
- INCOMPLETE / COMPLETE weekly state
- Optional `Tasks Available!` tag synchronization
- Optional custom reminders

## Theme Integration

Weekly Manager remains a theme-independent Playnite plugin. Settings, the game context menu, scheduling, reminders, tag synchronization, and the standalone checklist window continue to work with normal Playnite regardless of the selected theme.

Themes that explicitly support Weekly Manager can expose its registered custom UI elements in game details. The version-scoped FusionX 2.1.1 integration in [`Integrations/FusionX/2.1.1`](Integrations/FusionX/2.1.1) adds:

- a **Checklist** details tab for tracked games;
- a fixed-width **TASKS** switch beside FusionX's **VIDEO ON** control, with OFF meaning INCOMPLETE and ON meaning COMPLETE;
- a thin red top-edge cover indicator shown only while a tracked game is INCOMPLETE;
- access to the existing standalone checklist window from the tab.

The Checklist tab and standalone Checklist window also provide a **Manage Checklist** action. It opens a focused, per-game editor for adding, renaming, deleting, and reordering checklist items without exposing unrelated schedule or reminder settings.

This does not imply universal theme compatibility. A theme must include the Weekly Manager element placeholders, and a FusionX update can replace its modified view files.
