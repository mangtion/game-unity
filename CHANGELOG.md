# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and versioning follows [Semantic Versioning](https://semver.org/) (`MAJOR.MINOR.PATCH`,
pre-1.0 so breaking changes may land in a MINOR bump).

## [Unreleased]

## [0.1.0] - 2026-07-30

### Changed
- Full visual reboot: replaced the placeholder 4-frame pixel-art sprites (player, enemy-basic,
  enemy-fast, boss) with new 8-frame "serious graphic-novel" vector-style art sheets
  (128x128px per frame).
- Added a new background illustration (Joseon-era village/battlefield at night), replacing
  the flat solid-color arena floor.
- Sprite import pipeline (`Assets/Editor/ProjectBootstrap.cs`) updated: Point → Bilinear
  texture filtering, new frame size/count constants, and background-texture wiring with a
  graceful fallback if the art is missing.

### Known limitations
- `arrow.png` and `medal.png` are simpler PIL-drawn placeholders rather than the AI-generated
  style used for the characters, due to an image-generation service quota outage during
  development. The trade-off was reviewed and accepted; a follow-up art pass can revisit these
  two assets once generation capacity is available again.

### Process notes
- Design spec: `docs/superpowers/specs/2026-07-30-visual-reboot-design.md`
- Implementation plan: `docs/superpowers/plans/2026-07-30-visual-reboot.md`
- Built via subagent-driven development: 3 tasks, each independently task-reviewed (with one
  fix round each), plus a final whole-branch review before merging to `main`.

## [0.0.1] - 2026-07-30

### Added
- Initial Unity 6 (6000.5.5f1) port of the "이순신 서바이버" web prototype
  (`Desktop/base/game`), preserving its game logic/balance.
- Core gameplay: player movement, auto-targeting weapon/projectile system, enemy spawning with
  a difficulty curve, XP gems and leveling, a boss encounter, an upgrade-selection system, and
  win/game-over/title flow.
- URP 2D rendering pipeline with 2D lighting (global moonlit tone + boss spotlight).
- Automated test suite: 55 EditMode tests + 1 PlayMode integration test, runnable via
  `run-tests.bat` / `run-playtest.bat`.
