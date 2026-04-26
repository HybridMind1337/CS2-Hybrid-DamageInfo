# Changelog

All notable changes to Hybrid-DamageInfo will be documented here.

## [4.0.0] - 2026-04-26
### Added
- Live center HUD on hit — instantly shows HP/Armor dealt and hitgroup while alive
- `OnTick` listener — HUD stays visible continuously until timer expires
- Warmup period check — no damage info shown during warmup
- `OnMapStart` listener — game rules loaded correctly on each map
- `DisplayDeathInfo` and `DisplayRoundSummary` split into separate methods
- `IsDataShown` flag — prevents duplicate summary display

### Changed
- Damage tracking reworked — both GivenDamage and TakenDamage stored per player
- RecentDamage accumulates hits within 5 seconds for live HUD display
- Switched back to slot-based tracking for reliability with bots

## [3.1.0] - 2026-04-26
### Fixed
- Bot support — bots no longer cause missing damage data
- Steam64 tracking with slot-based fallback ID for bots

## [3.0.0] - 2026-04-26
### Added
- Per-player toggle menu (`!di`)
- Friendly fire detection with `[FF]` tag
- Damage threshold (`MinDamageToShow`) to reduce chat spam
- Compact death message mode
- Localization system (`lang/en.json`, `lang/bg.json`)
### Changed
- Switched from slot-based to Steam64-based damage tracking

## [2.0.0] - 2026-04-26
### Added
- Round-end damage leaderboard with 🥇🥈🥉
### Fixed
- Chat prefix color rendering (`{red}` placeholder broken)

## [1.0.0] - 2026-04-26
### Added
- Initial release
- Chat, HUD and console damage info on death
- Round-end summary
- Bot crash fix (safe dictionary access)
