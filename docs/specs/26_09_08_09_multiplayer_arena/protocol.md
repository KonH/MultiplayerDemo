# Wire protocol — v1

Transport: **WebSocket**, endpoint `ws://<host>:<port>/ws`. Chosen because it is the only
transport shared by native C#, browser TypeScript and Unity WebGL.

Encoding: **JSON**, one message per WebSocket text frame. Every message has a `t` field.
Field names are short because snapshots are sent 30x/second.

Coordinates are server-space: `x` right, `y` "up the screen".
- Web client: `screenX = (x - arenaMinX) * scale`, `screenY = (arenaMaxY - y) * scale`.
  Canvas Y grows downward, so it must be flipped: server `+y` has to appear *up* the
  screen, matching the input rule that pressing W sends `my = +1`.
- Unity client: `worldPos = (x, 0.5, y)` — server `y` maps to Unity `z`. With a top-down
  camera looking along `-Y` and its `up` along `+Z`, server `+y` is also up the screen.

## HTTP

`GET /api/info` (CORS `*`) →
```json
{ "name": "Arena", "players": 2, "maxPlayers": 32, "version": 1,
  "tickRate": 30, "wsPath": "/ws", "uptimeMs": 12345 }
```

`GET /` → the built Web client (static files), when present.

## UDP beacon

Broadcast to `255.255.255.255:47777` every 1000 ms, UTF-8 JSON:
```json
{ "magic": "MPDEMO1", "name": "Arena", "port": 8080, "players": 2 }
```
The receiver takes the sender's IP as the host.

## Client → Server

| `t` | Payload | Notes |
|---|---|---|
| `join` | `{ name: string, client: "unity" \| "web" }` | First message. |
| `input` | `{ mx: number, my: number, fire: boolean }` | `mx`,`my` in `[-1,1]`, normalised by the server. Sent every client frame, max 60/s. |
| `pong` | `{ id: number }` | Echo of a `ping`. |

## Server → Client

### `welcome`
```json
{ "t":"welcome", "id":3, "tickRate":30,
  "arena": { "minX":-20, "minY":-20, "maxX":20, "maxY":20 },
  "playerRadius":0.5, "bulletRadius":0.15, "moveSpeed":6,
  "maxHealth":5, "respawnMs":5000, "fireCooldownMs":500,
  "startedAtMs":1690000000000, "serverNowMs":1690000012345 }
```

### `reject`
```json
{ "t":"reject", "reason":"Name already taken" }
```

### `state` — 30 Hz, the authoritative snapshot
```json
{ "t":"state", "tick":123, "elapsedMs":41000,
  "players":[ { "id":3, "x":1.5, "y":-2.0, "hp":4, "dx":0, "dy":1, "cd":250 } ],
  "bullets":[ { "id":88, "x":3.0, "y":1.0, "o":3 } ] }
```
- `players` contains **alive** players only. `cd` = remaining fire cooldown ms.
  `dx`,`dy` = facing (last movement direction). `o` on a bullet = owner player id.
- A player present in `roster` but absent from `players` is dead or disconnected.

### `roster` — sent on change (join/leave/kill) and at least every 1 s
```json
{ "t":"roster", "players":[
  { "id":3, "name":"kon", "kills":2, "deaths":1, "connected":true,
    "ip":"127.0.0.1", "client":"web", "ping":12, "respawnIn":3200 } ] }
```
`respawnIn` is 0 when the player is alive, otherwise the ms left before respawn.

### `ping`
```json
{ "t":"ping", "id":42 }
```

### `event` — transient, for effects/feedback (clients may ignore)
```json
{ "t":"event", "kind":"kill", "victim":3, "killer":5 }
{ "t":"event", "kind":"hit", "victim":3, "killer":5 }
{ "t":"event", "kind":"spawn", "victim":3 }
```
