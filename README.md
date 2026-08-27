# Game Routines

Game Routines is a Playnite extension for managing recurring and long-term game activities with configurable routines, checklists, reset schedules and reminders.

Game Routines' core routine, checklist, scheduling, reminder, notification, context-menu, and pop-out checklist functionality operates independently of the active Playnite theme. [FusionX](https://github.com/sakasakiking/FusionX) is not required to install or use the extension. Version 0.9.0 was developed and tested primarily around [FusionX](https://github.com/sakasakiking/FusionX), so broader Playnite Desktop-theme UI compatibility has not yet been officially validated and some themes may have missing or incorrectly rendered Game Routines UI elements.

> All screenshots below were captured using the version-specific [FusionX](https://github.com/sakasakiking/FusionX) reference implementation in this repository. Stock FusionX does not currently include these embedded Game Routines controls.

## Features

- Track games from Playnite's game context menu or the extension settings.
- Create and order multiple named routines for each game.
- Maintain an ordered checklist and **COMPLETE** / **INCOMPLETE** state for each routine.
- Reset routines on a set schedule: **Never**, **Daily**, **Weekly** or **Biweekly**.
- Automatically derive a routine's state from its checklist.
- Choose which routines count toward the game's overall task status.
- Configure an independent game-level **Custom Reminder**.
- Receive persistent notifications through Playnite's internal notification system.
- Open checklists in separate pop-out windows while continuing to use Playnite.
- Use context-menu actions to open, complete, reset and configure tracked tasks.
- Optionally manage a `Tasks Available!` tag automatically based on a game's overall task status.
- Use optional theme-hosted controls for an embedded **Checklists** tab, tasks completion toggle, related shortcuts, and incomplete-state cover indicator (currently demonstrated by this repository's [FusionX](https://github.com/sakasakiking/FusionX) reference implementation).

## How it works

Each tracked game can have multiple routines: Daily tasks, Weekly tasks, Biweekly tasks, and a never-resetting checklist can all coexist with their own schedules. Every routine has its own name, order, checklist, status, and reset configuration.

A routine can either count toward the game's overall task status or be excluded from it:

- No participating routines means the overall state is **COMPLETE**.
- If all participating routines are **COMPLETE**, the overall state is **COMPLETE**.
- If any participating routine is **INCOMPLETE**, the overall state is **INCOMPLETE**.

This participation setting keeps optional or long-term routines from affecting the game's main task status unless you want them to.

![Game and routine configuration in Game Routines settings](assets/screenshots/01-settings.png)

If automatic `Tasks Available!` tag management is enabled, the tag gets added whenever a game's overall status is incomplete and removed once everything is complete. You can then use the tag to filter your library and quickly see which games still have tasks left to do.

## Routine schedules

- **Never** leaves the routine unchanged until you update or reset it manually.
- **Daily** resets at the configured local time each day.
- **Weekly** resets on the configured day and local time each week.
- **Biweekly** means once every two weeks. The **Start date** defines the 14-day cycle; the routine then resets every 14 days from that date at the configured local time.

When Playnite was not running at a scheduled time, Game Routines processes the latest missed occurrence after startup and avoids processing the same occurrence twice.

## Checklists and task status

Checklist items can be added, edited, ordered, checked, unchecked and reset independently for every routine. Themes can also host Game Routines controls directly in the game view. The screenshot below shows the embedded **Checklists** tab provided by this repository's version-specific [FusionX](https://github.com/sakasakiking/FusionX) reference implementation; it is not currently part of stock FusionX.

![Embedded Game Routines Checklists tab in FusionX](assets/screenshots/03-fusionx-checklists.png)

When automatic completion from checklist is enabled for a routine:

- all checklist items checked = **COMPLETE**;
- any unchecked item = **INCOMPLETE**; and
- an empty checklist = **COMPLETE**.

The checklist controls that routine's state while automatic completion is enabled.

Checklist management is also available in pop-out windows, which can remain open while you continue using Playnite.

![Game Routines checklist management pop-out window](assets/screenshots/02-manage-checklist.png)

## Custom Reminders

**Custom Reminder** is configured once per game and is independent from that game's routine reset schedules. It can run **Daily**, **Weekly** or **Biweekly**, with its own title, message, schedule and, for Biweekly reminders, **Start date**.

Biweekly reminders follow a 14-day cycle from the selected Start date at the configured local time. A reminder only creates a notification; it does not reset checklists or change routine status.

## Notifications

Game Routines uses Playnite's internal notification system, not external Windows toast notifications. Its notification records are persisted so pending Game Routines notifications remain available across Playnite restarts.

## Theme compatibility

### Supported and tested

Version 0.9.0 was developed and tested primarily around [FusionX](https://github.com/sakasakiking/FusionX), so broader Playnite Desktop-theme UI compatibility has not yet been officially validated. FusionX is not required to use Game Routines.

Game Routines' core features—including routines, checklists, schedules, reminders, notifications, context-menu actions, and pop-out checklist windows—operate independently of the active Playnite theme. Only embedded theme controls require theme-specific hosts.

### Other Desktop themes

Other Playnite Desktop themes are not yet officially supported. They may lack embedded Game Routines controls or render extension icons and layouts incorrectly. If you'd like support for a specific Playnite theme, [open a GitHub issue](https://github.com/felipeaucc/playnite-gameroutines-plugin/issues/new) to request it. Theme support requests will be considered as my time allows. :)

### FusionX reference implementation

This repository's version-specific [FusionX](https://github.com/sakasakiking/FusionX) reference files provide:

- an embedded **Checklists** tab;
- a tasks completion toggle;
- convenient shortcuts associated with the toggle for **Game Routines Settings** and **Custom Reminder** actions; and
- an incomplete-state indicator in the game cover.

Settings and Custom Reminders are core Game Routines features and remain accessible independently of these reference shortcuts.

The official [FusionX](https://github.com/sakasakiking/FusionX) project does not currently include these Game Routines hosts. The [reference implementation](Integrations/FusionX/2.1.1/README.md) is used for development and testing, documents the version-specific changes, and may serve as material for a future upstream contribution. Its files are not included in the `.pext` and are not part of the normal Game Routines installation process.

[FusionX](https://github.com/sakasakiking/FusionX) is a third-party Playnite theme by sakasakiking. Game Routines is not affiliated with the project.

![Tasks completion toggle and incomplete-state cover indicator in FusionX](assets/screenshots/04-fusionx-tasks-indicator.png)

## Installation

### Playnite Add-on Browser

Add-on Browser installation is planned for the first stable release and is pending acceptance into the Playnite Add-on Database.

### Manual installation

A `.pext` package is available from the [Game Routines 0.9.0 GitHub pre-release](https://github.com/felipeaucc/playnite-gameroutines-plugin/releases/tag/v0.9.0):

1. Download the `.pext` asset from the release.
2. Open the package with Playnite to start the extension installation flow.
3. Restart Playnite if requested.

The `.pext` installs the Game Routines extension. [FusionX](https://github.com/sakasakiking/FusionX) is not required, and the package does not modify theme files. The version-specific [FusionX reference files](Integrations/FusionX/2.1.1/README.md) in this repository are development and testing material, not a normal installation step.

## Updating

Once Game Routines is accepted into the Playnite Add-on Database, normal add-on update notifications and installation will be available through Playnite. During the beta, GitHub Releases may also provide `.pext` packages for manual installation. Automatic database updates are not active until the database entry exists.

## Getting started

1. Install Game Routines and restart Playnite if requested.
2. Select a game and choose **Game Routines > Start Tracking This Game** from its context menu, or use **Add game** in the Game Routines settings page.
3. Configure one or more routines and arrange their order.
4. Add checklist items if desired.
5. Choose the reset schedule, whether the routine counts toward overall task status, and whether its checklist controls automatic completion.
6. Optionally enable and configure the game-level **Custom Reminder**.
7. Use **Game Routines > Open Checklist** from the game context menu when a separate checklist window is useful.

![Game Routines commands in Playnite's game context menu](assets/screenshots/05-context-menu.png)

## Theme developer integration

Game Routines exposes three custom UI elements that Playnite Desktop themes can host: the multi-routine checklist, overall-state toggle and incomplete-state indicator. See [Theme integration](THEME_INTEGRATION.md) for the registered source and element names, hosting guidance and behavior expectations.

The files under [`Integrations/FusionX/2.1.1/`](Integrations/FusionX/2.1.1/) are the current version-specific reference implementation.

## License

Game Routines is licensed under the [MIT License](LICENSE).

Copied or modified [FusionX](https://github.com/sakasakiking/FusionX) integration files retain their applicable attribution and MIT license notice in [`Integrations/FusionX/2.1.1/LICENSE.FusionX.txt`](Integrations/FusionX/2.1.1/LICENSE.FusionX.txt).

## Credits

- **Game Routines:** felipeaucc
- **Playnite:** [JosefNemec/Playnite](https://github.com/JosefNemec/Playnite)
- **FusionX:** [sakasakiking/FusionX](https://github.com/sakasakiking/FusionX), by sakasakiking

## Development and source

Source code, issue tracking and development history are hosted in this repository. Contributions and bug reports are welcome.
