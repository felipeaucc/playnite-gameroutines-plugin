# Game Routines

Game Routines is a Playnite extension for managing recurring and long-term game activities with configurable routines, checklists, reset schedules and reminders. Version 1.0.0 is the first stable release.

Game Routines' core features, including routines, checklists, schedules, reminders, notifications, context-menu actions, and pop-out checklist windows, operate independently of the active Playnite theme. Version 1.0.0 is validated with Playnite's stock Default Desktop theme and [FusionX](https://github.com/sakasakiking/FusionX) 2.1.1. FusionX is not required. Supporting themes can optionally provide enhanced embedded integrations.

> Some screenshots below show additional Game Routines controls in [FusionX](https://github.com/sakasakiking/FusionX) that are not currently included in the official FusionX theme.

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
- Use optional Game Routines controls directly in the game view when supported by the active theme. The screenshots below demonstrate these controls with our modified [FusionX](https://github.com/sakasakiking/FusionX) setup.

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

Checklist items can be added, edited, ordered, checked, unchecked and reset independently for every routine. Themes can also display Game Routines controls directly in the game view. The screenshot below shows the embedded **Checklists** tab in the [FusionX](https://github.com/sakasakiking/FusionX) setup used during Game Routines development. The official FusionX theme does not currently include this tab.

![Embedded Game Routines Checklists tab in FusionX](assets/screenshots/03-fusionx-checklists.png)

When automatic completion from checklist is enabled for a routine:

- all checklist items checked = **COMPLETE**;
- any unchecked item = **INCOMPLETE**; and
- an empty checklist = **COMPLETE**.

The checklist controls that routine's state while automatic completion is enabled.

Checklist management is also available through **Open Checklist** and **Manage Checklist** context-menu actions and in pop-out windows, which can remain open while you continue using Playnite.

![Game Routines checklist management pop-out window](assets/screenshots/02-manage-checklist.png)

## Custom Reminders

**Custom Reminder** is configured once per game and is independent from that game's routine reset schedules. It can run **Daily**, **Weekly** or **Biweekly**, with its own title, message, schedule and, for Biweekly reminders, **Start date**.

Biweekly reminders follow a 14-day cycle from the selected Start date at the configured local time. A reminder only creates a notification; it does not reset checklists or change routine status.

## Notifications

Game Routines uses Playnite's internal notification system, not external Windows toast notifications. Its notification records are persisted so pending Game Routines notifications remain available across Playnite restarts.

## Theme compatibility

### Core compatibility

Version 1.0.0 is officially validated with Playnite's stock Default Desktop theme and regression-tested with the enhanced [FusionX](https://github.com/sakasakiking/FusionX) 2.1.1 integration. FusionX is not required to use Game Routines.

Core Game Routines functionality operates independently of the active Playnite theme. A theme does not need to host custom elements to support the core experience, and uncustomized themes are not considered unsupported merely because optional embedded controls are absent.

### Stock Default Desktop theme

Playnite's stock Default Desktop theme is officially validated for the core Game Routines experience. It does not host the optional embedded **Checklists** tab, embedded state controls, or incomplete cover indicator. Their absence is expected and is not a compatibility defect. Default users retain checklist functionality through **Open Checklist**, **Manage Checklist**, other Game Routines context-menu actions, and pop-out windows.

### Enhanced theme integrations

Supporting themes may host Game Routines' optional embedded elements. FusionX 2.1.1 is currently the primary enhanced integration and reference implementation. Other themes can support the core Game Routines experience without custom elements, while theme authors may add and validate embedded integration separately.

### FusionX embedded UI

The extra Game Routines controls shown in the screenshots were added to the [FusionX](https://github.com/sakasakiking/FusionX) setup used during Game Routines development. They include:

- an embedded **Checklists** tab;
- a tasks completion toggle;
- convenient shortcuts associated with the toggle for **Game Routines Settings** and **Custom Reminder** actions; and
- an incomplete-state indicator in the game cover.

These additions are not currently included in the official FusionX theme. They are not required to use Game Routines, and the **Settings** and **Custom Reminder** features remain available without them.

The modified FusionX 2.1.1 setup is the primary enhanced integration and reference implementation. Its additions may eventually be proposed to the FusionX project. They are not included in the `.pext` and are not part of the normal Game Routines installation process.

[FusionX](https://github.com/sakasakiking/FusionX) is a third-party Playnite theme by sakasakiking. Game Routines is not affiliated with the project.

![Tasks completion toggle and incomplete-state cover indicator in FusionX](assets/screenshots/04-fusionx-tasks-indicator.png)

## Installation

### Playnite Add-on Browser

Add-on Browser installation is pending acceptance into the Playnite Add-on Database.

### Manual installation

A `.pext` package is available from the [Game Routines 1.0.0 GitHub release](https://github.com/felipeaucc/playnite-gameroutines-plugin/releases/tag/v1.0.0):

1. Download the `.pext` asset from the release.
2. Open the package with Playnite to start the extension installation flow.
3. Restart Playnite if requested.

The `.pext` installs the Game Routines extension. [FusionX](https://github.com/sakasakiking/FusionX) is not required, and the package does not modify theme files.

## Updating

Until Game Routines is accepted into the Playnite Add-on Database, install updates manually from GitHub Releases. Normal add-on update notifications and installation through Playnite will become available after the database entry is accepted.

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

The files under [`Integrations/FusionX/2.1.1/`](Integrations/FusionX/2.1.1/) are the current optional enhanced integration and reference implementation.

## License

Game Routines is licensed under the [MIT License](LICENSE).

Copied or modified [FusionX](https://github.com/sakasakiking/FusionX) integration files retain their applicable attribution and MIT license notice in [`Integrations/FusionX/2.1.1/LICENSE.FusionX.txt`](Integrations/FusionX/2.1.1/LICENSE.FusionX.txt).

## Credits

- **Game Routines:** felipeaucc
- **Playnite:** [JosefNemec/Playnite](https://github.com/JosefNemec/Playnite)
- **FusionX:** [sakasakiking/FusionX](https://github.com/sakasakiking/FusionX), by sakasakiking

## Development and source

Source code, issue tracking and development history are hosted in this repository. Contributions and bug reports are welcome.
