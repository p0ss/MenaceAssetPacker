# TwitchSquaddies

A modpack that lets Twitch viewers take over your squaddies. Viewers type `!draft` in chat to enter a pool, and the streamer assigns them to squaddies in-game via the DevConsole panel. The squaddie's nickname changes to the viewer's Twitch display name, and their latest chat message appears in the home planet field.

---

## Architecture

Two components work together:

- **TwitchServer** — standalone .NET 8 console app that connects to Twitch IRC, manages the viewer draft pool, and exposes a local HTTP API on port 7654.
- **Modpack client** — runs inside the game via MelonLoader. Polls the server for status, provides DevConsole commands and a dice button per squaddie to assign viewers.

The split keeps network I/O out of the game process. If the server is offline the modpack degrades gracefully — everything except Twitch features continues to work.

---

## Setup

### Prerequisites

- .NET 8 SDK (for building/running TwitchServer)
- MelonLoader installed on the game
- Menace.ModpackLoader deployed (the modpack loader that compiles and loads this modpack)

### 1. Configure TwitchServer

```bash
cd tools/TwitchServer
dotnet run
```

On first run the server creates a `config.json` template next to the binary:

```json
{
  "channel": "your_channel_name",
  "oauthToken": "oauth:your_token_here"
}
```

Fill in your Twitch channel name and an OAuth token. You can generate a token at [twitchapps.com/tmi](https://twitchapps.com/tmi/). The token only needs chat read permission — the server connects as an anonymous listener by default, but the token is still required by Twitch IRC.

`config.json` is gitignored. Never commit credentials.

### 2. Start the server

```bash
cd tools/TwitchServer
dotnet run
```

The server auto-connects to your channel on startup if `config.json` is valid. You should see:

```
[Config] Auto-connected to #yourchannel
TwitchServer starting on http://localhost:7654
```

### 3. Deploy the modpack

The modpack lives in `TwitchSquaddies-modpack/` and is compiled by ModpackLoader at game startup. Ensure the modpack folder is in your staging directory and launch the game.

---

## Usage

### Viewer side (Twitch chat)

Viewers type `!draft` in your Twitch channel to enter the draft pool. They can type it multiple times — only one entry per username is kept.

### Streamer side (in-game)

Open the DevConsole (default key: F1) and switch to the **Squaddies** panel.

**Panel header** shows the current Twitch connection status:

- `Server: OFFLINE` — TwitchServer isn't running or unreachable
- `Server: not connected` — server is running but not connected to Twitch
- `Twitch: #channel | Pool: 5` — connected, 5 viewers in draft pool

**Dice button** — when the server is connected and the draft pool is non-empty, a `?` button appears next to each squaddie. Clicking it picks a random viewer from the pool and assigns them:

1. Squaddie's nickname is set to the viewer's Twitch display name
2. Home planet field shows the viewer's latest chat message

**Click "Refresh Data"** to reload the squaddie list after scene changes.

---

## Commands

All commands are available in the DevConsole command line.

### Squaddie commands

| Command | Args | Description |
|---------|------|-------------|
| `sq.list` | | List all alive squaddies |
| `sq.name` | `<id> <name>` | Set a squaddie's name |
| `sq.nick` | `<id> <nickname>` | Set a squaddie's nickname |
| `sq.twitch` | `<id> <message>` | Set the home planet display text |
| `sq.twitch.clear` | `<id>` | Restore the real home planet name |
| `sq.twitch.clearall` | | Clear all home planet overrides |

### Twitch commands

| Command | Args | Description |
|---------|------|-------------|
| `twitch.status` | | Show server connection state |
| `twitch.draft` | | List viewers in the draft pool |
| `twitch.pick` | `<squaddie-id>` | Pick a random viewer and assign to squaddie |
| `twitch.url` | `[url]` | Get or set the TwitchServer URL (default `http://localhost:7654`) |

---

## Server HTTP API

The TwitchServer exposes these endpoints on `http://localhost:7654`. They're consumed by the modpack client but can also be used directly for debugging or external tools.

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/status` | Connection state, channel, pool size |
| GET | `/api/draft` | List viewers in draft pool |
| POST | `/api/draft/pick` | Pick and remove a random viewer |
| GET | `/api/messages/{username}` | Latest chat messages from a viewer |
| POST | `/api/connect` | Connect to a channel (JSON body: `{"channel":"...","oauthToken":"..."}`) |
| POST | `/api/disconnect` | Disconnect from Twitch |

Example:

```bash
# Check status
curl localhost:7654/api/status

# Pick a viewer
curl -X POST localhost:7654/api/draft/pick

# Read someone's recent messages
curl localhost:7654/api/messages/someviewer
```

---

## Thread Safety

The modpack runs in Unity's single-threaded environment but needs to make HTTP calls without blocking the game loop. The approach:

- **HTTP calls** run via `Task.Run` on the thread pool
- **Status results** are stored in `volatile` fields that IMGUI reads directly
- **Viewer assignments** go into a `ConcurrentQueue` that `OnUpdate()` drains on the main thread
- Write operations (`WriteSquaddieNickname`, `SetTwitchMessage`) only happen on the main thread

The server side uses `ConcurrentDictionary` for the draft pool and message store.

---

## File Structure

```
TwitchSquaddies-modpack/
  modpack.json                  Modpack manifest (sources, references)
  src/
    TwitchSquaddiesPlugin.cs    Plugin entry point, panel, commands
    SquaddieExplorer.cs         Runtime type discovery for squaddie data
    SquaddieReadWrite.cs        Name/nickname writes, home planet interception
    TwitchClient.cs             HTTP client polling TwitchServer

tools/TwitchServer/
  TwitchServer.csproj           .NET 8 minimal API project
  Program.cs                    HTTP endpoints, config loading, wiring
  TwitchIrcClient.cs            Raw TCP IRC client for Twitch
  DraftPool.cs                  Thread-safe viewer draft pool
  MessageStore.cs               Per-user chat message history
  config.json                   Credentials (gitignored, created on first run)
```

---

## Troubleshooting

**Server says "Auto-connect failed"** — Check that your OAuth token is valid and starts with `oauth:`. Tokens expire; generate a new one if needed.

**Panel shows "Server: OFFLINE"** — The modpack polls `localhost:7654` every 3 seconds. Make sure the server is running before or alongside the game. Check that nothing else is using port 7654.

**Dice button doesn't appear** — The button only shows when: (1) the server is reachable, (2) it's connected to Twitch, and (3) at least one viewer is in the draft pool. Check `twitch.status`.

**Squaddie list is empty** — You need to be in the strategy scene. Click "Refresh Data" or run `sq.list` after the scene loads. The setup coroutine retries for up to 60 seconds after the first scene load.

**"No squaddies found (wrong scene?)"** — `StrategyState.Get()` returned null. This is normal in non-strategy scenes (menus, tactical combat). Switch to the strategy map.

**SecurityScanner flags HttpClient as DANGER** — This is expected. The modpack uses `System.Net.Http` for localhost communication. The security warning is advisory and does not block compilation.
