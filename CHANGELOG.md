# Changelog

All notable changes to this project are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and versioning follows [Semantic Versioning](https://semver.org/) (`MAJOR.MINOR.PATCH`,
pre-1.0 so breaking changes may land in a MINOR bump).

## [Unreleased]

## [0.2.0] - 2026-07-30

### Changed
- Replaced player/enemy-basic/enemy-fast/boss sprites with user-supplied AI-generated
  character art (provided directly as reference images, bypassing the blocked
  HuggingFace ZeroGPU generation pipeline). Background removed via `rembg`
  (u2net model for player/enemies; the boss's busier burning-city/flame-sword source
  image needed a hand-tuned silhouette mask + a near-black vignette fill for the
  remaining edges, since automated segmentation struggled with its dark, high-detail
  background). Each character was resized to a 128x128 frame and tiled into a
  1024x128 sheet to fit the existing `SpriteFlipbook` import pipeline unchanged.

### Known limitations
- The source art is a single static pose per character, not a multi-frame walk/attack
  cycle, so the same frame is repeated 8x - characters no longer animate while
  moving (previously fixed in 0.1.0's visual reboot). Traded off deliberately for
  much better visibility/contrast; a true walk-cycle regeneration is a follow-up.
- The 0.1.2 visual-scale (1.7x) and brightness-tint (1.9x) stopgap from
  `ProjectBootstrap.cs` still applies on top of this new art. It reads fine (verified
  via screenshot) but is untuned for this specific art and could be revisited.
- arrow.png/medal.png are still the PIL-drawn placeholders from 0.0.1 (see Known
  limitations there) - not part of this swap.

## [0.1.2] - 2026-07-30

### Fixed
- Player/enemy/boss sprites were barely visible in play: a player (radius 16) rendered
  at only ~3.5% of the 900-unit-tall arena view, and the generated art's dark, desaturated
  palette (RGB ~23,1,4) was nearly indistinguishable from the near-black arena floor
  (RGB ~16-19). `Global Light 2D`/`BossSpotlight` had zero effect on this since character
  sprites use the unlit `Sprites/Default` shader.
- Stopgap fix (full fix needs new art once ZeroGPU quota resets, see Known limitations):
  character sprites now render at 1.7x their previous size via a new
  `CharacterPixelsPerUnitFor` import path (`Assets/Editor/ProjectBootstrap.cs`) that scales
  the sprite only - `CircleCollider2D.radius` (and therefore hitboxes/gameplay) is untouched.
  Also applied a `Color(1.9, 1.9, 1.9, 1)` brightening tint to Player/Enemy `SpriteRenderer`s.
  Projectile/pickup icons keep their exact 1:1 diameter-matches-radius sizing.

### Known limitations
- The brightening tint is a multiply, so it can't lift true near-black pixels much. Real
  fix is regenerating player/enemy/boss art with a lighter palette once HuggingFace
  ZeroGPU quota is available again (same blocker as arrow.png/medal.png, see 0.1.0).

## [0.1.1] - 2026-07-30

### Fixed
- Upgrade card buttons on the level-up screen were unclickable. Root cause: `Hud`
  is active at the same time as `LevelUpScreen` and, as the last sibling under
  `Canvas`, rendered on top of it; its full-screen (but fully transparent) `Image`
  still blocked `GraphicRaycaster` hits by default, silently swallowing every
  click meant for the cards underneath. Fixed by setting `Hud`'s
  `Image.raycastTarget = false` in `ProjectBootstrap.BuildUI` (`Assets/Editor/ProjectBootstrap.cs`).

### Process notes
- Added a PlayMode regression test (`FullPlaytestTests.cs`) that raycasts through
  the real `EventSystem`/`GraphicRaycaster` at the upgrade card's screen position
  and asserts the `Button` is the topmost hit. The prior test suite only ever
  called `GameManager.ChooseUpgrade()` directly, bypassing the UI entirely, which
  is why 55 EditMode + 1 PlayMode tests were all green despite the button being
  unclickable in actual play. Verified the new test fails against the pre-fix
  code (top hit: `Hud`) and passes after the fix.

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
