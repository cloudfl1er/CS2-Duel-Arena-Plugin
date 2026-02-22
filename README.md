# CS2 Duel Arena Plugin

Counter-Strike 2 server plugin for competitive 1v1 duels, built with the [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) framework.

[![Build](https://github.com/cloudfl1er/CS2-Duel-Arena-Plugin/actions/workflows/build.yml/badge.svg)](https://github.com/cloudfl1er/CS2-Duel-Arena-Plugin/actions/workflows/build.yml)

## Features

- **Automated match management** — polls backend API for match assignments every 3 seconds
- **Player whitelist** — only allows authorised Steam IDs to join the reserved server
- **MR16 competitive format** — 16 max rounds, 1v1, team swap at round 9
- **GOTV demo recording** — automatically records and reports demo file paths
- **Skin changer integration** — subscription-based skin selection via `!guns`
- **Disconnect handling** — instant loss when losing, 2-minute grace period when winning
- **Admin commands** — `duel_cancel`, `duel_restart`, `duel_status`, `duel_force_end`
- **Full result reporting** — kills, deaths, headshots, damage, scores, duration

## Prerequisites

- Counter-Strike 2 dedicated server
- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp/releases) installed on the server
- .NET 8.0 runtime (included with CounterStrikeSharp)
- A running backend API compatible with the Duel Arena API spec

## Installation

1. Download the latest `CS2DuelServer.dll` from the [Releases](../../releases) page.
2. Copy `CS2DuelServer.dll` to your server's plugin directory:
   ```
   game/csgo/addons/counterstrikesharp/plugins/CS2DuelServer/CS2DuelServer.dll
   ```
3. Copy `config.json` to the same directory and edit it with your server details:
   ```
   game/csgo/addons/counterstrikesharp/plugins/CS2DuelServer/config.json
   ```
4. Restart your CS2 server.

## Configuration

Edit `config.json` (auto-created in the plugin directory on first run):

| Field | Default | Description |
|---|---|---|
| `server_id` | `1` | Unique ID of this server in the backend |
| `api_base_url` | `http://localhost:8000` | Base URL of the Duel Arena backend API |
| `api_key` | *(see config.json)* | API authentication key |
| `poll_interval_seconds` | `3` | How often to poll the API for match assignments |
| `join_timeout_minutes` | `5` | Cancel duel if players don't join within this time |
| `reconnect_grace_period_seconds` | `120` | Grace period for a winning player who disconnects |
| `round_time_seconds` | `115` | Round time (1m 55s) |
| `freeze_time_seconds` | `15` | Freeze time at round start |
| `team_swap_at_round` | `9` | Round number at which teams swap sides |
| `max_rounds` | `16` | Total rounds (MR16 format) |
| `enable_demo_recording` | `true` | Enable GOTV demo recording |
| `demo_folder` | `gotv` | Folder for demo files (relative to server root) |
| `enable_skin_changer` | `true` | Enable subscription-based skin changer |
| `skin_menu_command` | `!guns` | Chat command to open the skin menu |
| `debug_mode` | `true` | Print extra debug information to console |

## Admin Commands

All admin commands require the `css_` prefix when typed in console or rcon:

| Command | Description |
|---|---|
| `css_duel_cancel` | Cancel the current duel and refund |
| `css_duel_restart` | Restart the current game (mp_restartgame) |
| `css_duel_status` | Print current match state and score |
| `css_duel_force_end` | Force-end the match and report results |

## API Endpoints Used

| Method | Endpoint | Description |
|---|---|---|
| `POST` | `/api/server/{id}/heartbeat` | Send server heartbeat with status |
| `GET` | `/api/server/{id}/current_match` | Poll for assigned match |
| `PATCH` | `/api/server/{id}/status` | Update server status |
| `POST` | `/api/matches/{id}/result` | Submit match results |
| `POST` | `/api/matches/{id}/cancel` | Cancel a match |
| `POST` | `/api/matches/{id}/team_swap` | Record team swap event |
| `GET` | `/api/users/{steam_id}/subscription` | Check subscription for skin changer |

All requests include the header `api-key: <your_api_key>`.

## Building from Source

Requires .NET 8.0 SDK.

```bash
dotnet build CS2DuelServer.csproj --configuration Release
```

Compiled output will be in `bin/Release/net8.0/`.

## Troubleshooting

- **Plugin not loading** — ensure CounterStrikeSharp is installed correctly and the DLL is in the right folder.
- **API connection errors** — verify `api_base_url` and `api_key` in `config.json`.
- **Players getting kicked immediately** — the server is in a private match; only whitelisted Steam IDs can join.
- **Demo not recording** — ensure `tv_enable 1` is set and the `demo_folder` path is writable.

## License

MIT

