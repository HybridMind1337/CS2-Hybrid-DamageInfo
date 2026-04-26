# Hybrid-DamageInfo

A [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) plugin for CS2 that displays damage information to players after death and at round end.

> Created by **HybridMind** | Inspired by the archived [K4-DamageInfo](https://github.com/KitsuneLab-Development/K4-DamageInfo)

---

## Features

- **Chat messages** — shows who dealt damage to you and how much you dealt before dying
- **Center HUD** — clean HTML overlay with damage summary on death
- **Round-end summary** — detailed per-player damage breakdown at the end of each round
- **Damage leaderboard** — 🥇🥈🥉 ranked by total HP dealt each round
- **Friendly fire detection** — FF damage shown with `[FF]` tag
- **Bot support** — works correctly with bots (no crashes)
- **Anti-spam** — configurable minimum damage threshold
- **Localization** — English and Bulgarian included, easily extendable
- **Per-player toggles** — each player controls their own display via `!di`
- **Steam64 tracking** — reliable tracking even when players reconnect mid-round

---

## Requirements

- [Metamod:Source](https://www.sourcemm.net/)
- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) `>= API 200`

---

## Installation

1. Download the latest release from the [Releases](../../releases) page
2. Extract to your server:
```
csgo/addons/counterstrikesharp/plugins/HybridDamageInfo/
```
3. The folder should look like:
```
HybridDamageInfo/
├── HybridDamageInfo.dll
└── lang/
    ├── en.json
    └── bg.json
```
4. Restart your server or run `css_plugins load HybridDamageInfo`
5. Config is auto-generated at:
```
csgo/addons/counterstrikesharp/configs/plugins/HybridDamageInfo/HybridDamageInfo.json
```

---

## Configuration

```json
{
  "Language": "en",
  "ShowCenterHUD": true,
  "ShowChatMessage": true,
  "ShowConsoleLog": false,
  "ShowRoundEndSummary": true,
  "ShowHitgroup": true,
  "HUDDisplaySeconds": 3.0,
  "ShowBotDamage": true,
  "ShowFriendlyFire": true,
  "MinDamageToShow": 20,
  "CompactDeathMessage": true,
  "ConfigVersion": 1
}
```

| Option | Default | Description |
|---|---|---|
| `Language` | `en` | Language file to use (`en`, `bg`) |
| `ShowCenterHUD` | `true` | Show damage info as center HTML overlay |
| `ShowChatMessage` | `true` | Show damage info in chat |
| `ShowConsoleLog` | `false` | Log damage info to player console |
| `ShowRoundEndSummary` | `true` | Show damage summary at round end |
| `ShowHitgroup` | `true` | Show hit zones (Head, Chest, etc.) |
| `HUDDisplaySeconds` | `3.0` | How long the HUD overlay stays visible |
| `ShowBotDamage` | `true` | Track and show damage involving bots |
| `ShowFriendlyFire` | `true` | Show friendly fire damage with `[FF]` tag |
| `MinDamageToShow` | `20` | Minimum HP damage required to show an entry |
| `CompactDeathMessage` | `true` | Show 1 compact line on death instead of full list |

---

## Commands

| Command | Description |
|---|---|
| `!di` | Open the settings menu |
| `!di 1` | Toggle chat messages |
| `!di 2` | Toggle HUD overlay |
| `!di 3` | Toggle round summary |
| `!di chat` | Toggle chat messages |
| `!di hud` | Toggle HUD overlay |
| `!di summary` | Toggle round summary |

> All commands are also available with `!damageinfo` and via console as `css_di` / `css_damageinfo`.

---

## Localization

Language files are located in the `lang/` folder. Currently included:

- `en.json` — English
- `bg.json` — Bulgarian

To add a new language, copy `en.json`, rename it (e.g. `de.json`) and translate the values. Then set `"Language": "de"` in the config.

---

## Changelog

### v3.1.0
- Fixed bot support — bots no longer cause missing damage data
- Steam64 tracking with slot-based fallback ID for bots

### v3.0.0
- Added per-player toggle menu (`!di`)
- Added friendly fire detection with `[FF]` tag
- Added damage threshold (`MinDamageToShow`) to reduce chat spam
- Added compact death message mode
- Switched from slot-based to Steam64-based damage tracking
- Added localization system (`lang/en.json`, `lang/bg.json`)

### v2.0.0
- Added round-end damage leaderboard 🥇🥈🥉
- Fixed chat prefix color rendering
- Added `ShowRoundEndSummary` toggle

### v1.0.0
- Initial release
- Chat, HUD and console damage info on death
- Bot crash fix (safe dictionary access)

---

## License

Distributed under the [GPL-3.0 License](LICENSE).

---

## Credits

- Inspired by [K4-DamageInfo](https://github.com/KitsuneLab-Development/K4-DamageInfo) by K4ryuu
- Built with [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) by roflmuffin
