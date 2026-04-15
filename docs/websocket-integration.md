# WebSocket Integration Guide

## Purpose

This document explains how the backend WebSocket server works, what messages it accepts, what data it sends back, and what the frontend is expected to do when connecting from Three.js.

Use this file as the source of truth for integration work between the game backend and the frontend renderer.

## Current Server Endpoints

Headless backend base URL:

```text
http://localhost:5000
```

Available endpoints:

- `GET /status`
- `GET /state`
- `GET /enemies`
- `POST /spawn`
- `WS /ws`

## What The Backend Does

The backend runs an ASP.NET Core server in headless mode and keeps one shared `GameLoopService` alive for the whole process.

Important behavior:

- The game loop updates room state in the background.
- Every WebSocket client connection gets its own `Player`.
- The player is removed automatically when the socket disconnects.
- The backend sends a snapshot of the game world every 100ms.
- The backend is currently authoritative for entity state.

This means the frontend should treat the server as the source of truth for positions, health, bullets, and spawned entities.

## Backend Developer Notes

Relevant files:

- [Program.cs](C:/Users/HuyN/OneDrive/Desktop/Projects/PBL_GAME/Backend_sandbox/GameSolution/BackendSandbox/Program.cs)
- [GameWebSocketHandler.cs](C:/Users/HuyN/OneDrive/Desktop/Projects/PBL_GAME/Backend_sandbox/GameSolution/BackendSandbox/Core/GameWebSocketHandler.cs)
- [GameLoopService.cs](C:/Users/HuyN/OneDrive/Desktop/Projects/PBL_GAME/Backend_sandbox/GameSolution/BackendSandbox/Core/GameLoopService.cs)
- [Entity.cs](C:/Users/HuyN/OneDrive/Desktop/Projects/PBL_GAME/Backend_sandbox/GameSolution/BackendSandbox/Models/Entity.cs)

Responsibilities:

- `Program.cs` wires up ASP.NET, registers services, and maps routes.
- `GameWebSocketHandler.cs` owns the WebSocket protocol.
- `GameLoopService.cs` provides safe mutation methods and snapshot generation.
- `Entity.cs` now gives every entity a stable `Id`.

If backend developers add new gameplay actions, they should update both:

- the WebSocket handler message switch
- this document's protocol section

If backend developers change the shape of snapshots, they must coordinate with frontend developers before merging because the frontend renderer depends on the JSON shape.

## Frontend Developer Notes

The frontend should open one WebSocket connection to:

```text
ws://localhost:5000/ws
```

Recommended frontend flow:

1. Open the socket when the game scene loads.
2. Wait for the `welcome` message.
3. Store the returned `playerId`.
4. Listen for `state` messages continuously.
5. Render entities by their `id`.
6. Send input messages such as `move`, `look`, and `shoot`.
7. Remove or clean up scene objects when entities disappear from snapshots.

Recommended rendering rule:

- Do not recreate all meshes every frame.
- Keep a map of `entityId -> mesh`.
- Update transform data when the entity already exists.
- Create meshes only for new IDs.
- Remove meshes when IDs are missing from the latest snapshot.

## WebSocket Lifecycle

When a client connects:

1. The backend accepts the socket.
2. The backend creates a `Player`.
3. The backend sends a `welcome` message.
4. The backend starts streaming `state` messages every 100ms.

When a client disconnects:

1. The socket closes.
2. The backend removes that player's entity from the room.

## Messages Sent By The Server

### `welcome`

Sent once after connection is accepted.

Example:

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

Meaning:

- `playerId`: the server-side player controlled by this socket
- `supportedMessages`: commands currently accepted by the server

### `state`

Sent immediately after connection and then every 100ms.

Example:

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

Meaning:

- `playerId`: the player controlled by this client
- `snapshot.worldWidth`: world width in pixels
- `snapshot.worldHeight`: world height in pixels
- `snapshot.tileSize`: current tile size
- `snapshot.players`: all players currently in the room
- `snapshot.enemies`: all enemies currently in the room
- `snapshot.bullets`: all active bullets currently in the room

### `pong`

Sent back when the client sends `ping`.

Example:

```json
{
  "type": "pong",
  "serverTimeUtc": "2026-04-15T16:03:01.319818+00:00"
}
```

### `error`

Sent when the client sends bad JSON or an unsupported message.

Example:

```json
{
  "type": "error",
  "message": "Unsupported message type 'jump'."
}
```

## Messages Sent By The Client

All client messages are JSON and must include `type`.

### `move`

Use this to move the current player.

Example:

```json
{
  "type": "move",
  "x": 1,
  "y": 0,
  "dt": 0.016
}
```

Notes:

- `x` and `y` are direction input, not target coordinates
- they can be negative, zero, or positive
- they are normalized by the backend player movement code
- `dt` is optional and defaults to roughly `1 / 60`

Suggested mapping:

- `W` => `{ x: 0, y: -1 }`
- `S` => `{ x: 0, y: 1 }`
- `A` => `{ x: -1, y: 0 }`
- `D` => `{ x: 1, y: 0 }`

### `look`

Use this to set where the player is aiming.

Example:

```json
{
  "type": "look",
  "x": 500,
  "y": 300
}
```

Notes:

- `x` and `y` are world-space coordinates
- for mouse aiming, convert the cursor hit point into the same world coordinate system used by gameplay

### `shoot`

Use this to fire a bullet from the current player.

Example:

```json
{
  "type": "shoot"
}
```

### `teleport`

Useful for debugging and testing scenes.

Example:

```json
{
  "type": "teleport",
  "x": 256,
  "y": 256
}
```

### `spawnEnemy`

Useful for testing combat and rendering.

Example:

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

Requests an immediate state snapshot.

Example:

```json
{
  "type": "snapshot"
}
```

### `ping`

Connection health check.

Example:

```json
{
  "type": "ping"
}
```

## Frontend Example

Basic browser example:

```js
const socket = new WebSocket("ws://localhost:5000/ws");
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
  socket.send(JSON.stringify({
    type: "move",
    x,
    y,
    dt: 1 / 60
  }));
}
```

## Coordinate Expectations

The backend currently sends entity positions and sizes in gameplay pixel units.

Frontend developers need to decide how those map into the Three.js scene:

- either use 1 backend pixel = 1 world unit
- or define a scale factor and apply it consistently everywhere

Whatever mapping is chosen, apply the same rule to:

- player positions
- enemy positions
- bullet positions
- mesh sizes
- aiming coordinates sent in `look`

## Integration Checklist

Backend:

- Run the headless server.
- Confirm `/status` returns OK.
- Confirm `ws://localhost:5000/ws` accepts connections.
- Keep this document updated when protocol changes.

Frontend:

- Connect once per active game session.
- Store `playerId` from `welcome`.
- Render from `state.snapshot`.
- Send `move` from keyboard input.
- Send `look` from mouse aim or camera raycast result.
- Send `shoot` on fire input.
- Track entities by `id`.

## Current Limitations

- Snapshots are full-state pushes, not delta updates.
- There is no authentication or room selection yet.
- There is no reconnect session recovery yet.
- There is no interpolation layer yet on the backend.
- `spawnEnemy` and `teleport` are currently debug-friendly commands and may need restriction later.

## Suggested Next Steps

- Add room or match identifiers if multiple sessions are needed.
- Add explicit message schemas shared by backend and frontend.
- Add interpolation on the frontend for smoother rendering between snapshots.
- Add input buffering or sequence numbers if movement prediction is needed.
- Add a dedicated DTO layer if more entity types are introduced.
