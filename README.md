# Game Routines

Game Routines is a Playnite generic plugin for recurring or persistent per-game tasks. It is currently in early development and has not been published as a packaged release.

## Features

- Track games by their authoritative Playnite `Game.Id`, either from the searchable settings picker or directly from the game context menu. Multi-selection adds only games that are not already tracked.
- Maintain one ordered checklist per tracked game, including independent checked states and a focused standalone checklist window.
- Mark game tasks **COMPLETE** or **INCOMPLETE** without changing Playnite's built-in game completion status.
- Optionally derive task status automatically from checklist completion.
- Choose a per-game reset schedule:
  - **Never** keeps checklist state until it is reset manually.
  - **Daily** resets once per local calendar day at the configured time.
  - **Weekly** resets once per week on the configured day and time.
- Process only the latest missed reset after Playnite starts, with exact-once occurrence tracking.
- Configure an independent **Custom reminder** with its own Daily or Weekly frequency, time, title, and message. Reminders are notification-only and never reset the checklist or change task status.
- Optionally synchronize the canonical `Tasks Available!` tag while tasks are INCOMPLETE.
- Optionally show the approved incomplete-task cover indicator globally and per game.

Newly tracked games default to a **Never** reset schedule, automatic checklist completion off, the per-game cover indicator on, an empty checklist, COMPLETE task status, and Custom reminder off.

## Theme integration

Game Routines remains theme-independent: settings, scheduling, Playnite notifications, context-menu actions, tags, and standalone checklist windows work without theme support.

Themes can opt into the registered Game Routines custom elements. The version-scoped [FusionX 2.1.1 integration](Integrations/FusionX/2.1.1) adds:

- a **Checklist** details tab for tracked games;
- the approved fixed-width **TASKS** card and hover actions;
- the approved red incomplete-task cover indicator, including its glow and proportional scaling; and
- **Manage Checklist** and pop-out **Checklist** access.

The global and per-game cover-indicator settings remain authoritative. A FusionX update can replace modified theme view files, so integrations are kept as reproducible, version-specific source copies.
