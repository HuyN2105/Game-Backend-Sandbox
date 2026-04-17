# WebSocket Integration Guide

## Purpose

This document defines how the backend WebSocket service works, which messages are supported, and what the frontend should do when integrating with Three.js.

Use this as the integration contract between backend and frontend.

## Current Server Endpoints

Hosted backend base URL:

```text
https://pblgame.huyn.site
```

Available endpoints:

- `GET /status`
- `GET /state`
- `GET /enemies`
- `POST /spawn`
- `WS /ws`

## Backend Behavior

The backend runs ASP.NET Core in headless mode with one shared `GameLoopService`.

Key behavior:

- The game loop updates room state continuously.
- Each WebSocket connection gets its own `Player`.
- The player is removed when the socket disconnects.
- The server pushes a full `state` snapshot every 100ms.
- The backend is authoritative for entity state.

Frontend should treat server data as source of truth for positions, health, bullets, and spawned entities.

## Backend Developer Notes

Relevant files:

- [Program.cs](C:/Users/HuyN/OneDrive/Desktop/Projects/PBL_GAME/Backend_sandbox/GameSolution/BackendSandbox/Program.cs)
- [GameWebSocketHandler.cs](C:/Users/HuyN/OneDrive/Desktop/Projects/PBL_GAME/Backend_sandbox/GameSolution/BackendSandbox/Core/GameWebSocketHandler.cs)
- [GameLoopService.cs](C:/Users/HuyN/OneDrive/Desktop/Projects/PBL_GAME/Backend_sandbox/GameSolution/BackendSandbox/Core/GameLoopService.cs)
- [Entity.cs](C:/Users/HuyN/OneDrive/Desktop/Projects/PBL_GAME/Backend_sandbox/GameSolution/BackendSandbox/Models/Entity.cs)

Responsibilities:

- `Program.cs`: ASP.NET setup, service registration, route mapping.
- `GameWebSocketHandler.cs`: WebSocket protocol handling.
- `GameLoopService.cs`: thread-safe game state mutation and snapshots.
- `Entity.cs`: stable `Id` for each entity.

When backend actions or payload shapes change, update this document and coordinate with frontend before merging.

## Frontend Developer Notes

Connect using:

```text
wss://pblgame.huyn.site/ws
```

Recommended flow:

1. Open socket when scene loads.
2. Wait for `welcome`.
3. Store `playerId`.
4. Listen for `state` continuously.
5. Render entities by `id`.
6. Send input (`move`, `look`, `shoot`).
7. Remove scene objects for missing entity IDs.

Rendering rule:

- Do not recreate all meshes each frame.
- Maintain `entityId -> mesh`.
- Update transforms for existing entities.
- Create meshes only for new IDs.
- Remove meshes when IDs disappear from snapshot.

## WebSocket Lifecycle

On connect:

1. Socket accepted.
2. `Player` created.
3. `welcome` sent.
4. `state` streamed every 100ms.

On disconnect:

1. Socket closes.
2. Player entity removed.

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
    "shoot",
    "spawnEnemy { x, y, width?, height? }",
    "snapshot",
    "ping"
  ]
}
```

### `state`

Sent immediately after connect and then every 100ms.

```json
{
  "type": "state",
  "playerId": "e04869d4-fd9c-438e-9833-ed94aea1324a",
  "serverTimeUtc": "2026-04-15T16:03:01.3215737+00:00",
  "snapshot": {
    "worldWidth": 81920,
    "worldHeight": 46080,
    "tileSize": 64,
    "players": [
      {
        "id": "e04869d4-fd9c-438e-9833-ed94aea1324a",
        "x": 128,
        "y": 128,
        "width": 48,
        "height": 48,
        "health": 100
      }
    ],
    "enemies": [
      {
        "id": "b78912e7-c65f-4322-8664-793e4f0a929d",
        "x": 400,
        "y": 300,
        "width": 50,
        "height": 50,
        "health": 50
      }
    ],
    "bullets": []
  }
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

Returned for bad JSON or unsupported message.

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

- `x`/`y` are directional input, not target position.
- Can be negative, zero, or positive.
- Normalized by backend movement code.
- `dt` is optional (default ~`1 / 60`).

### `look`

```json
{
  "type": "look",
  "x": 500,
  "y": 300
}
```

`x`/`y` should be world-space coordinates.

### `shoot`

```json
{
  "type": "shoot"
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

  if (message.type === "state") {
    syncPlayers(message.snapshot.players);
    syncEnemies(message.snapshot.enemies);
    syncBullets(message.snapshot.bullets);
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

Use the same rule for positions, mesh sizes, bullets, and `look` coordinates.

## Integration Checklist

Backend:

- Deploy and run headless server.
- Confirm `https://pblgame.huyn.site/status` returns OK.
- Confirm `wss://pblgame.huyn.site/ws` accepts connections.
- Keep this document updated when protocol changes.

Frontend:

- Connect once per active session.
- Store `playerId` from `welcome`.
- Render from `state.snapshot`.
- Send `move`, `look`, `shoot` from input.
- Track entities by `id`.

## Current Limitations

- Full-state snapshots only (no delta updates).
- No auth or room selection yet.
- No reconnect session recovery yet.
- No backend interpolation yet.
- `spawnEnemy` and `teleport` are debug-oriented and may be restricted later.

## Suggested Next Steps

- Add room/match identifiers for multi-session support.
- Share explicit message schemas between frontend and backend.
- Add frontend interpolation between snapshots.
- Add input sequencing/buffering for prediction support.
- Add dedicated DTOs as entity types grow.
