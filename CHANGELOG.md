# Changelog

Notable public changes to Game Routines are documented in this file.

## 1.0.0 - 2026-08-28

First stable release.

### Added

- Settings indicate whether the active Desktop theme hosts the optional incomplete cover indicator.

### Changed

- Improved settings and checklist controls to use portable, theme-safe visual resources.
- Clarified that core Game Routines functionality is independent of the active Desktop theme.
- Reframed FusionX 2.1.1 as an optional enhanced integration and reference implementation.

### Fixed

- Fixed settings action icons, interaction states, routine-state controls, and DatePicker alignment in Playnite's stock Default Desktop theme.
- Fixed checklist action icons, check marks, checked-state visibility, and automatic-completion icon semantics across themes.
- Preserved the intended FusionX checklist icon weight without affecting other themes.

### Compatibility

- Officially validated the core Game Routines experience with Playnite's stock Default Desktop theme.
- Regression-tested the enhanced FusionX 2.1.1 integration.
- Stock Default does not host the optional embedded Checklists tab, embedded state controls, or incomplete cover indicator.
- Their absence in stock Default is expected and is not a compatibility defect.
- Checklist access remains available through Game Routines context-menu actions and pop-out windows.

## 0.9.0 - 2026-08-25

First public beta.

### Added

- Multiple named and ordered routines per game.
- Per-routine checklists and **COMPLETE** / **INCOMPLETE** state.
- Never, Daily, Weekly and Biweekly reset schedules with a user-defined Biweekly **Start date**.
- Configurable participation in the game's aggregate overall task status.
- Automatic checklist-driven routine completion.
- Independent game-level Custom Reminders on Daily, Weekly and Biweekly schedules.
- Persistent notifications through Playnite's internal notification system.
- Modeless checklist pop-out windows and game context-menu actions.
- Optional `Tasks Available!` tag synchronization.
- Custom UI elements for enhanced theme integration.
- Version-specific FusionX 2.1.1 enhanced integration.

### Notes

- This is the first public beta and may change in response to testing and feedback.
- FusionX 2.1.1 with the repository's version-specific integration is the supported and tested Desktop-theme target for this beta.
- Other Playnite Desktop themes are not currently supported and may have visual or integration issues.
- Broader Desktop-theme compatibility may be considered in a future release.
- The Game Routines integration is not yet included in the official FusionX project.
