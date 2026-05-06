# WebSocket Integration Guide

## Purpose

This document defines the backend WebSocket contract and how the frontend should integrate with the live game state stream.

## Live Endpoint

WebSocket:

```text
wss://pblgame.huyn.site/ws
```

## Backend Behavior (Summary)

- ASP.NET Core runs headless with one `GameLoopService`.
- Each WebSocket connection gets a dedicated `Player` and room instance.
- Player and room are removed on disconnect.
- The backend is authoritative for movement and state.
- The server streams live state at a fixed interval.

Frontend must treat server data as the source of truth.

## Frontend Integration (Recommended Flow)

1. Connect to `wss://pblgame.huyn.site/ws`.
2. Wait for `welcome` to get `playerId`.
3. Listen for live state messages.
4. Render entities using stable IDs and update transforms only.
5. Send input messages (`move`, `look`, `shoot`, etc).

### Rendering Rule

- Keep a `entityId -> mesh` map.
- Update transforms for existing entities.
- Create only for new IDs.
- Remove meshes when IDs disappear.

## WebSocket Lifecycle

On connect:

1. Socket accepted.
2. Player created.
3. `welcome` sent.
4. Live state streamed periodically.

On disconnect:

1. Socket closes.
2. Player and room are removed.

## Server Messages

### `welcome`

Sent once after connect.

```json
{
  "type": "welcome",
  "playerId": "e04869d4-fd9c-438e-9833-ed94aea1324a",
  "serverTimeUtc": "2026-04-15T16:03:01.2852576+00:00",
  "supportedMessages": [
    "move { x, y, dt? }",
    "teleport { x, y }",
    "look { x, y }",
    "shoot { x, y }",
    "spawnEnemy { x, y, width?, height? }",
    "snapshot",
    "ping"
  ]
}
```

> Note: treat `supportedMessages` as informational only. Always follow the message formats below.

### `LiveData` (state stream)

The backend currently streams `LiveData` payloads (not `type: "state"`). Expect this format for each tick:

```json
{
  "DataId": "LiveData",
  "PlayerX": 128,
  "PlayerY": 128,
  "CurrentPlayerHp": 100,
  "Speed": 400,
  "Spawns": [
    { "X": 400, "Y": 300, "CurrentHp": 50, "Speed": 200 }
  ],
  "Bullets": [
    { "X": 220, "Y": 180, "DirectionX": 1, "DirectionY": 0 }
  ]
}
```

### `pong`

Reply to `ping`.

```json
{
  "type": "pong",
  "serverTimeUtc": "2026-04-15T16:03:01.319818+00:00"
}
```

### `error`

Returned for bad JSON or unsupported message types.

```json
{
  "type": "error",
  "message": "Unsupported message type 'jump'."
}
```

## Client Messages

All client messages are JSON and must include `type`.

### `move`

```json
{
  "type": "move",
  "x": 1,
  "y": 0,
  "dt": 0.016
}
```

Notes:
- `x`/`y` are directional input.
- `dt` is optional (default ~`1 / 60`).

### `look`

```json
{
  "type": "look",
  "x": 500,
  "y": 300
}
```

### `shoot`

```json
{
  "type": "shoot",
  "x": 1,
  "y": 0
}
```

### `teleport` (debug)

```json
{
  "type": "teleport",
  "x": 256,
  "y": 256
}
```

### `spawnEnemy` (debug)

```json
{
  "type": "spawnEnemy",
  "x": 600,
  "y": 400,
  "width": 50,
  "height": 50
}
```

### `snapshot`

```json
{
  "type": "snapshot"
}
```

### `ping`

```json
{
  "type": "ping"
}
```

## Frontend Example

```js
const socket = new WebSocket("wss://pblgame.huyn.site/ws");
const entities = new Map();
let myPlayerId = null;

socket.addEventListener("open", () => {
  socket.send(JSON.stringify({ type: "ping" }));
});

socket.addEventListener("message", (event) => {
  const message = JSON.parse(event.data);

  if (message.type === "welcome") {
    myPlayerId = message.playerId;
    return;
  }

  if (message.DataId === "LiveData") {
    syncPlayer(message.PlayerX, message.PlayerY, message.CurrentPlayerHp);
    syncEnemies(message.Spawns);
    syncBullets(message.Bullets);
  }
});

function sendMove(x, y) {
  socket.send(JSON.stringify({ type: "move", x, y, dt: 1 / 60 }));
}
```

## Coordinate Expectations

Backend values are gameplay pixel units.

Frontend should either:

- map `1 backend pixel = 1 scene unit`, or
- apply one consistent scale factor everywhere.

Use the same rule for positions, sizes, bullets, and `look` coordinates.

## Integration Checklist

Frontend:

- Connect once per active session.
- Store `playerId` from `welcome`.
- Render from `LiveData`.
- Send `move`, `look`, `shoot` based on input.
- Track entities by ID.

## Current Limitations

- Full-state streaming only (no deltas).
- No auth or room selection.
- No reconnect session recovery.
- `spawnEnemy` and `teleport` are debug-only and may be restricted.

## Suggested Next Steps

- Add room/match identifiers for multi-session support.
- Share explicit JSON schemas for each message type.
- Add client-side interpolation.
- Add input sequencing for prediction.
