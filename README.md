# Game Routines

![Game Routines icon](icon.png)

**Game Routines** is a Playnite extension for managing recurring and long-term game activities with configurable routines, checklists, reset schedules and reminders.

Core functionality works independently of your Playnite theme through settings, context-menu actions, notifications and pop-out checklists.

Supported themes can provide enhanced integration directly in the game details view. **FusionX** is currently the primary supported enhanced integration, providing the **Checklists** tab, **Tasks** completion toggle and incomplete-state indicator.

Version 0.9.0 is the first public beta.

## Features

- Track games from Playnite's game context menu or the extension settings.
- Create and order multiple named routines for each game.
- Maintain an ordered checklist and **COMPLETE** / **INCOMPLETE** state for each routine.
- Reset routines on **Never**, **Daily**, **Weekly** or **Biweekly** schedules.
- Automatically derive a routine's state from its checklist.
- Choose which routines count toward the game's overall task status.
- Configure an independent game-level **Custom Reminder**.
- Receive persistent notifications through Playnite's internal notification system.
- Open modeless checklist windows while continuing to use Playnite.
- Use context-menu actions to open, complete, reset and configure tracked tasks.
- Optionally maintain a `Tasks Available!` tag while a game's overall status is incomplete.
- Show an optional incomplete-state cover indicator in supported themes.
- Let compatible themes embed Game Routines controls directly in game views.

## How it works

Each tracked game can have multiple routines: Daily tasks, Weekly tasks and a long-term checklist can coexist without sharing a schedule. Every routine has its own name, order, checklist, status and reset configuration.

A routine can either count toward the game's overall task status or be excluded from it:

- No participating routines means the overall state is **COMPLETE**.
- If all participating routines are **COMPLETE**, the overall state is **COMPLETE**.
- If any participating routine is **INCOMPLETE**, the overall state is **INCOMPLETE**.

This participation setting keeps optional or long-term routines from affecting the game's main task status unless you want them to.

## Routine schedules

- **Never** leaves the routine unchanged until you update or reset it manually.
- **Daily** resets at the configured local time each day.
- **Weekly** resets on the configured day and local time each week.
- **Biweekly** means once every two weeks. The **Start date** defines the 14-day cycle; the routine then resets every 14 days from that date at the configured local time.

When Playnite was not running at a scheduled time, Game Routines processes the latest missed occurrence after startup and avoids processing the same occurrence twice.

## Checklists and task status

Checklist items can be added, edited, ordered, checked, unchecked and reset independently for every routine. Checklists can stay embedded in a supported theme or open in modeless pop-out windows.

When automatic completion from checklist is enabled for a routine:

- all checklist items checked = **COMPLETE**;
- any unchecked item = **INCOMPLETE**; and
- an empty checklist = **COMPLETE**.

The checklist controls that routine's state while automatic completion is enabled.

## Custom Reminders

**Custom Reminder** is configured once per game and is independent from that game's routine reset schedules. It can run **Daily**, **Weekly** or **Biweekly**, with its own title, message, schedule and, for Biweekly reminders, **Start date**.

Biweekly reminders follow a 14-day cycle from the selected Start date at the configured local time. A reminder only creates a notification; it does not reset checklists or change routine status.

## Notifications

Game Routines uses Playnite's internal notification system, not external Windows toast notifications. Its notification records are persisted so pending Game Routines notifications remain available across Playnite restarts.

## Theme compatibility

**Game Routines does not require FusionX.** Core functionality works independently of the active Playnite Desktop theme through settings, game context-menu actions, Playnite notifications and checklist pop-out windows.

Themes can optionally integrate Game Routines' custom UI elements to provide embedded controls in game views. Embedded controls and the cover indicator appear only when the active theme explicitly supports them; Game Routines is not universally embedded in every Playnite theme.

### FusionX enhanced integration

[FusionX](https://github.com/sakasakiking/FusionX) 2.1.1 is currently the primary enhanced integration target. The version-specific integration in this repository provides:

- a **Checklists** tab;
- a **TASKS** / **Tasks** completion toggle;
- **Game Routines Settings** and **Custom Reminder** actions; and
- an incomplete-state cover indicator.

The official FusionX project does not currently include Game Routines integration. This repository provides an optional integration specifically for FusionX 2.1.1, and the intent is to propose the integration upstream in the future. This does not imply approval or acceptance by the FusionX author.

FusionX is a third-party Playnite theme by sakasakiking. Game Routines is not affiliated with the FusionX project.

Game Routines can be installed and used without modifying FusionX. If you want the optional embedded FusionX controls, follow the version-specific [FusionX 2.1.1 integration instructions](Integrations/FusionX/2.1.1/README.md).

## Installation

### Playnite Add-on Browser

Add-on Browser installation is planned for the first public release and is pending acceptance into the Playnite Add-on Database.

### Manual installation

No `.pext` package has been published yet. Once a GitHub Release is available:

1. Download the `.pext` file from the release.
2. Open the package with Playnite to start the supported extension installation flow.
3. Restart Playnite if requested.

No FusionX integration is required for these steps.

## Updating

Once Game Routines is accepted into the Playnite Add-on Database, normal add-on update notifications and installation will be available through Playnite. During the beta, GitHub Releases may also provide `.pext` packages for manual installation. Automatic database updates are not active until the database entry exists.

## Getting started

1. Install Game Routines and restart Playnite if requested.
2. Select a game and choose **Game Routines > Start Tracking This Game** from its context menu, or use **Add game** in the Game Routines settings.
3. Configure one or more routines and arrange their order.
4. Add checklist items if desired.
5. Choose the reset schedule, whether the routine counts toward overall task status, and whether its checklist controls automatic completion.
6. Optionally enable and configure the game-level **Custom Reminder**.
7. Use **Game Routines > Open Checklist** to open the game's checklist window from any Desktop theme.

## Theme developer integration

Game Routines exposes three custom UI elements that Playnite Desktop themes can host: the multi-routine checklist, overall-state toggle and incomplete-state indicator. See [Theme integration](THEME_INTEGRATION.md) for the registered source and element names, hosting guidance and behavior expectations.

The files under [`Integrations/FusionX/2.1.1/`](Integrations/FusionX/2.1.1/) are a working, version-specific integration example.

## Compatibility

- Generic Playnite plugin targeting .NET Framework 4.6.2.
- Built against Playnite SDK/API 6.16.0.
- Enhanced FusionX integration is scoped specifically to FusionX 2.1.1.

No minimum Playnite application version is claimed beyond the technically known SDK/API requirement.

## License

Game Routines is licensed under the [MIT License](LICENSE).

Copied or modified FusionX integration files retain their applicable FusionX attribution and MIT license notice in [`Integrations/FusionX/2.1.1/LICENSE.FusionX.txt`](Integrations/FusionX/2.1.1/LICENSE.FusionX.txt).

## Credits

- **Game Routines:** Felipe Aucc
- **Playnite:** [JosefNemec/Playnite](https://github.com/JosefNemec/Playnite)
- **FusionX:** [sakasakiking/FusionX](https://github.com/sakasakiking/FusionX), by sakasakiking

FusionX remains the work of its respective author. Its derived integration files are covered by the attribution and license notice linked above.

## Development and source

Source code, issue tracking and development history are hosted in this repository. Contributions and bug reports are welcome after the repository's public launch.
