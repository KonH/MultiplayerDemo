# Unity client

3D client for the arena. Unity 6000.5.5f1, URP, gameplay state in DOTS/Entities.

## Run

Open `Assets/Scenes/Arena.unity` and press Play. Type a name and a server address, or pick
a server from the discovered list, then Connect. Start a server first:

```sh
dotnet run --project ../Server/MultiplayerDemo.Server
```

## Controls

WASD / arrows to move, LMB or Space to shoot, TAB for the leaderboard.

## Build

`Arena` is build scene 0.

- **Standalone**: File → Build Settings → Windows → Build.
- **WebGL**: switch the platform to WebGL and build. `Assets/Plugins/WebGL/ArenaWebSocket.jslib`
  takes over from `ClientWebSocket`, and discovery falls back to probing `/api/info` because
  a browser cannot receive the UDP beacon.

## Structure

```
Assets/Scripts/
  ArenaClientBootstrap.cs   composition root: owns the socket + discovery, builds the ECS world
  Net/                      IArenaConnection (native + WebGL), discovery, wire types and parsing
  Ecs/                      components, systems, runtime meshes and URP materials
  Ui/                       IMGUI HUD, connect screen, leaderboard, pure input/format helpers
Assets/Tests/EditMode/      Unity Test Framework tests over the pure logic
Assets/Plugins/WebGL/       WebSocket jslib bridge
```

The client holds no simulation of its own: `ArenaSnapshotSystem` turns each server snapshot
into entities, `ArenaInterpolationSystem` renders them ~100 ms in the past, and
`ArenaInputSystem` sends input at 30 Hz. There is deliberately no client-side prediction.

Entities are created at runtime — no subscenes, no baked prefabs, no authored art. Meshes
and URP Lit materials are built in code, so the project needs no art pipeline and the DOTS
code stays the only description of the world.

## Tests

Window → General → Test Runner → EditMode → Run All, or via MCP for Unity. They cover
protocol parsing, interpolation (including entities appearing and disappearing), the input
vector and time formatting.

## Notes

- The project uses the Input System package only (`activeInputHandler: 1`), so the code
  reads `Keyboard.current` / `Mouse.current` rather than the legacy `Input` API.
- Two Unity clients on one machine both default to the name `UnityPlayer`; the second is
  rejected with "already taken" until the name is changed. That is the intended rule.
