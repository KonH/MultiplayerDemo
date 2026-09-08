# Unity client

3D client for the arena. Unity 6000.5.5f1, URP, gameplay state in DOTS/Entities.

## Run

Open `Assets/Scenes/Arena.unity` and press Play. In the UI Toolkit connect panel, type a
name and server address, or pick a server from the discovered list, then Connect. Start a
server first:

```sh
dotnet run --project ../Server/MultiplayerDemo.Server
```

## Controls

WASD / arrows to move, LMB or Space to shoot, TAB for the leaderboard.

## Build

`Arena` remains build scene 0. The UI Toolkit migration does not change either build target.

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
  Ui/                       UI Toolkit binding/view, pure projector, input and format helpers
Assets/UI/ArenaHud/         panel settings, root UXML/shared USS, connect/HUD/leaderboard templates
Assets/Tests/EditMode/      Unity Test Framework logic, UI tree and scene-contract tests
Assets/Plugins/WebGL/       WebSocket jslib bridge
```

`Arena.unity` contains one authored UI Toolkit `PanelRenderer`, using
`Assets/UI/ArenaHud/ArenaHudPanelSettings.asset` and root `ArenaHud.uxml`. The root composes
the connect, HUD and leaderboard templates with shared and root USS. Its `ArenaHudDocument`
binding is explicitly serialized into `ArenaClientBootstrap`; `ArenaHudView` owns the UI tree
and `ArenaHudProjector` turns client state into presentation data.

The client holds no simulation of its own: `ArenaSnapshotSystem` turns each server snapshot
into entities, `ArenaInterpolationSystem` renders them ~100 ms in the past, and
`ArenaInputSystem` sends input at 30 Hz. There is deliberately no client-side prediction.

World entities are created at runtime, with no subscenes, baked prefabs or authored arena
art. Meshes and URP Lit materials are still built in code. The HUD is the authored exception:
its UXML, USS and panel settings live under `Assets/UI/ArenaHud/`.

## Tests

Window → General → Test Runner → EditMode → Run All, or via MCP for Unity. They cover
protocol parsing, interpolation (including entities appearing and disappearing), input and
formatting, HUD projection, UI tree/view behavior, document binding and the scene contract.

## Notes

- The project uses the Input System package only (`activeInputHandler: 1`), so the code
  reads `Keyboard.current` / `Mouse.current` rather than the legacy `Input` API.
- LMB firing is suppressed only while the pointer is over a visible connect, HUD or
  leaderboard panel. Space always fires, and TAB still toggles the leaderboard.
- Two Unity clients on one machine both default to the name `UnityPlayer`; the second is
  rejected with "already taken" until the name is changed. That is the intended rule.
