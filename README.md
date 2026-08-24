# Game Routines

Game Routines is a Playnite generic plugin for recurring or persistent per-game tasks. It is currently in early development and has not been published as a packaged release.

## Features

- Track games by their authoritative Playnite `Game.Id`, either from the searchable settings picker or directly from the game context menu. Multi-selection adds only games that are not already tracked.
- Create zero or more named routines for each tracked game. Routine identity uses a stable GUID; names are required, unique within the game, and limited to 40 characters.
- Give every routine its own ordered checklist, checked states, **COMPLETE** or **INCOMPLETE** status, automatic checklist-completion option, and reset schedule.
- Choose a reset schedule independently for each routine:
  - **Never** keeps that routine unchanged until it is reset manually.
  - **Daily** resets that routine once per local calendar day at the configured time.
  - **Weekly** resets that routine once per week on the configured day and time.
  - **Biweekly** resets that routine once every 14 days from its persisted, user-selectable local start date and time.
- Process only the latest missed occurrence for each routine after Playnite starts, with per-routine exact-once occurrence tracking.
- Optionally count each routine toward the game's derived overall task status. The overall status is **INCOMPLETE** when any counted routine is incomplete and **COMPLETE** when all counted routines are complete or no routines are counted.
- Change one routine manually from the Checklist UI, or use the FusionX **TASKS** control and context-menu actions to update all counted routines atomically. An automatic checklist conflict blocks the entire aggregate change.
- Optionally synchronize the canonical `Tasks Available!` tag while the derived overall status is **INCOMPLETE**.
- Optionally show the approved red incomplete-task cover indicator globally and per game. It follows only the derived overall status.
- Configure one independent game-level **Custom reminder** with its own Daily, Weekly, or Biweekly frequency, start date, time, title, and message. It is notification-only and never resets a checklist or changes a routine status.

Newly tracked games start with one `Tasks` routine: **COMPLETE**, **Never**, not counted toward overall status, automatic completion off, and an empty checklist. Newly added routines use the same safe defaults with a unique generated name. The per-game cover indicator defaults on and Custom reminder defaults off.

Existing schema-v1 tracked games migrate to exactly one counted routine while preserving task state, checklist data, reset schedule and occurrence marker, automatic-completion ownership, Custom reminder values, tracking, and cover preferences. Migrated names reflect the existing cadence: `Dailies`, `Weeklies`, or `Tasks`.

## Theme integration

Game Routines remains theme-independent: settings, per-routine scheduling, Playnite notifications, context-menu actions, tags, and standalone checklist windows work without theme support.

Themes can opt into the registered Game Routines custom elements. The version-scoped [FusionX 2.1.1 integration](Integrations/FusionX/2.1.1) adds:

- a stacked multi-routine **Checklists** details tab with reusable routine cards;
- the approved fixed-width aggregate **TASKS** card;
- compact settings and game-level Custom Reminder actions on the **TASKS** hover;
- the approved red overall-incomplete cover indicator, including its existing glow and proportional scaling; and
- per-routine automatic-completion controls, ordering actions, and shared all-routine or focused pop-out checklist windows.

The global and per-game cover-indicator settings remain authoritative. A FusionX update can replace modified theme view files, so integrations are kept as reproducible, version-specific source copies.
