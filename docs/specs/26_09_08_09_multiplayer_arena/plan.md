# Plan — Multiplayer Arena Demo

Spec: `spec.md`. Wire contract: `protocol.md` (frozen before any client work starts).

## Constitution check

| Principle | How this plan complies |
|---|---|
| URP only | Unity client creates its runtime material from `Universal Render Pipeline/Lit`. No Built-in RP shaders. |
| DOTS for gameplay | Unity client keeps world state in ECS components and drives it with `ISystem` systems in a runtime-created `World`. MonoBehaviours only bootstrap the world, poll input and draw IMGUI. |
| No `FindObjectOfType` / mutable statics | The bootstrap MonoBehaviour is the composition root: it creates the connection, injects it into the ECS world as a managed singleton component. |
| Plan before implement | This file. |
| Spec before plan | `spec.md`. |
| `docs/specs/<YY_MM_DD_HH>_<name>/` | `docs/specs/26_09_08_09_multiplayer_arena/`. |
| C# style: tabs, `_` privates, always braces, no redundant modifiers | Enforced by `.editorconfig` at the repo root, applied to both the server and the Unity client. |

## Repository layout

```
Server/
  MultiplayerDemo.sln
  MultiplayerDemo.Server/         ASP.NET Core (net9.0) — WebSocket + HTTP + UDP beacon
  MultiplayerDemo.Core/           Pure simulation, no I/O — unit-testable
  MultiplayerDemo.Tests/          xUnit tests over MultiplayerDemo.Core
WebClient/                        Vite + TypeScript, 2D canvas client
  tools/bot.ts                    Headless bot client used by the integration test
UnityClient/Assets/Scripts/       Unity DOTS client
docs/specs/26_09_08_09_multiplayer_arena/
```

## Design decisions

1. **WebSocket + JSON.** The only transport all three runtimes share (native C#, browser
   TS, Unity WebGL). JSON keeps the contract debuggable; payloads are tiny at these
   player counts.
2. **Simulation split into `MultiplayerDemo.Core`** — `Arena`, `SimPlayer`, `SimBullet`,
   `GameSim.Tick(dt)` — with zero networking. Every rule in FR-3..FR-5 is then a plain
   unit test.
3. **No client-side prediction.** Clients render the authoritative snapshot with a short
   interpolation buffer. Simpler, always consistent; acceptable for LAN/localhost.
4. **Unity client builds its world at runtime** (no subscenes, no baked prefabs, no
   `.unity` scene edits, IMGUI for UI). The project can therefore be built and reviewed
   without an Editor session authoring assets, and the DOTS code stays the only source
   of truth for gameplay state.
5. **Discovery is two-pronged** because browsers cannot receive UDP: beacon for native,
   `/api/info` port probing for browsers. Both clients also accept a typed address.

## Work breakdown

### Phase 1 — Server (sequential, blocks both clients)
1. `.editorconfig`, `.gitignore` additions, solution + three projects.
2. `MultiplayerDemo.Core`: arena clamp, circle-vs-circle separation, input →
   movement, fire cooldown, projectile integration + hit resolution, death,
   respawn timer, free-spot search, kill/death counters, elapsed clock.
3. `MultiplayerDemo.Tests`: unit tests for every rule above (acceptance criterion 8).
4. `MultiplayerDemo.Server`: Kestrel host, `/api/info`, `/ws` session handling
   (join validation, name reuse, input intake, snapshot + roster broadcast, ping/pong,
   disconnect → `connected:false`), UDP beacon, static file hosting for the Web client.
5. Verify: `dotnet test`, then start the server and hit `/api/info`.

### Phase 2 — Clients (parallel, one subagent each)
Both agents work against the frozen `protocol.md` and a running server.

**2a Web client** (`WebClient/`)
- Vite + TS, no framework. Connect screen (name + address + discovered list + refresh).
- Canvas renderer: arena bounds, own player green, others red, name labels, bullets.
- HUD: HP, cooldown bar, respawn countdown, elapsed timer; TAB leaderboard overlay.
- Input: WASD/arrows, LMB/Space to fire, sent at ≤60 Hz.
- Tests: vitest over pure modules (interpolation, input vector, formatting) plus
  `tools/bot.ts` headless client.

**2b Unity client** (`UnityClient/Assets/Scripts/`)
- `ArenaClientBootstrap` MonoBehaviour: composition root, owns the WebSocket connection
  (native `ClientWebSocket`; `jslib` bridge for WebGL), creates the ECS world contents.
- ECS: `PlayerId`, `NetPosition`, `RenderPos`, `Health`, `LocalPlayerTag`, `BulletTag`,
  `ArenaConfig`, `NetSnapshot` (managed singleton) components; systems for applying
  snapshots, spawning/despawning entities, interpolating and writing `LocalTransform`.
- Rendering: `RenderMeshUtility.AddComponents` with runtime cube meshes + URP Lit
  materials (green / red / yellow bullet), plus a scaled plane for the arena.
- UI: IMGUI overlay — connect screen, HP, cooldown, respawn, timer, TAB leaderboard.
- Tests: Unity Test Framework EditMode tests over the pure protocol parsing/interp code.

### Phase 3 — Integration (sequential, main session)
1. Start the server.
2. Run the two-bot headless test: both bots join, observe each other, one kills the
   other, assert kill/death/respawn (acceptance criterion 9).
3. Drive the real Web client in a browser; run a second Web client tab to prove
   two clients from one machine (criteria 2–7).
4. Unity client: compile and play-mode check via Unity MCP if an Editor is available;
   otherwise document the exact steps and leave the code review-ready.
5. Root `README.md`: how to run server, Web client, Unity client.

## Risks

- **No Unity Editor is currently running**, so the Unity client cannot be compile-checked
  in this session. Mitigation: keep the Unity client asset-free and code-only, and target
  the exact `com.unity.entities@1.4` API surface. Flag clearly if it stays unverified.
- **Unity WebGL WebSockets** need a `jslib` bridge; the native path uses
  `ClientWebSocket`. Both live behind one `IArenaConnection` interface.
