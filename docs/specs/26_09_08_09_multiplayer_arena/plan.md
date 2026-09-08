# Plan — Multiplayer Arena Demo

## Spec

Source: `spec.md`. Wire contract: `protocol.md` (already frozen).

The feature remains a never-ending, server-authoritative top-down arena rendered by a
Unity 3D client and a TypeScript Web client. Clients send input and present authoritative
snapshots; movement, collision, damage, death, respawn and scoring remain server-owned.
The Unity client must support standalone and WebGL and must present its connect screen,
HUD and TAB leaderboard with Unity UI Toolkit exclusively.

Acceptance remains unchanged: server discovery and connection work; Unity and Web clients
observe the same movement, collision, shots, health, death, respawn, roster and elapsed
time; server rules and the two-bot kill flow remain automated; the Unity UI shows HP,
cooldown, respawn countdown, timer, connection errors and every leaderboard column.

## Goal

Replace the already-implemented Unity client's `ArenaHud.OnGUI`/`GUILayout` presentation
with an authored UI Toolkit surface without changing networking, ECS gameplay state,
input behavior, discovery, protocol, rendering, or Web client/server behavior.

## Current state

- Server, Web client, wire protocol, runtime-created Unity DOTS world, transports,
  discovery, input, interpolation and URP rendering are implemented.
- `ArenaClientBootstrap` currently creates and binds `ArenaHud` at runtime.
- `ArenaHud` currently implements the connect screen, playing HUD and leaderboard in
  IMGUI; `ArenaFormat` and input-vector behavior already have EditMode coverage.
- `Arena.unity` has the bootstrap but no authored UI Toolkit panel, and
  `UnityClient/README.md` still documents the UI as IMGUI and asset-free.

## Approach

### UI assets and composition

Create one authored `PanelSettings` asset and one `PanelRenderer` in `Arena.unity`. Its
single root `ArenaHud.uxml` composes three named templates: connect, in-game HUD and
leaderboard. The root switches those templates with `display: none/flex`; no additional
panels or sorting layers are needed.

Organize assets under `UnityClient/Assets/UI/ArenaHud/`:

```text
ArenaHudPanelSettings.asset
ArenaHud.uxml                 root document and template composition
ArenaHud.uss                  full-screen/root and template-instance positioning
Shared.uss                    shared colours, typography, panels and controls
Connect/Connect.uxml
Connect/Connect.uss           connect layout
Hud/Hud.uxml
Hud/Hud.uss                   in-game HUD layout
Leaderboard/Leaderboard.uxml
Leaderboard/Leaderboard.uss  overlay/table layout
```

Every UXML imports `Shared.uss` before its own local USS. The root also imports the three
template USS files so rows created dynamically in C# resolve their classes against the
owning document. Root-level positioning stays in `ArenaHud.uss`; reusable visual styling
stays in `Shared.uss`; template-specific layout stays in the corresponding template USS.
Use UI Toolkit controls and `VisualElement`s only—no IMGUI, Canvas, uGUI or TextMesh Pro
UI components.

### Binding, view and state flow

- Replace `ArenaHud` with `ArenaHudDocument`, a binding MonoBehaviour requiring the one
  `PanelRenderer`. It registers/unregisters the UI reload callback, constructs the view
  from the supplied root, owns player-name/address change, connect, refresh and
  server-selection callbacks, and refreshes from the current client state. Text changes
  update the bootstrap immediately; projected text is applied with
  `SetValueWithoutNotify` so per-frame refresh cannot erase typing or recurse through
  change callbacks. Register clicks with a project-local pointer-up helper
  suitable for Unity 6000.5.5f1 rather than depending on `GlobalStrategy` code or the
  unreliable `Button.clicked` path documented there.
- Add a plain C# `ArenaHudView` that queries named elements, updates fields and visibility
  from a passed presentation state, and renders discovered-server and leaderboard rows.
  It has no networking, ECS queries, scene lookup or dependency resolution.
- Add a pure UI state projection model/function between runtime data and the view. It
  converts connection status, player/health/cooldown/respawn/time, discovery results,
  debug counts and roster entries into display-ready state while retaining `ArenaFormat`
  for time/countdown formatting.
- Keep `ArenaClientBootstrap` as the composition root. Give it an explicit serialized
  `ArenaHudDocument` reference, call `Bind(this)` during bootstrap, and remove the runtime
  `AddComponent<ArenaHud>()`. The document may read only the public client-facing state
  and methods already supplied by the bootstrap/link; no `FindObjectOfType`, static
  mutable singleton or global scene lookup is introduced.
- Preserve `ArenaInput`: TAB continues to toggle `ShowLeaderboard`, and WASD/arrows plus
  LMB/Space continue to feed ECS only while playing. Add a project-local UI pointer query
  over the visible connect/HUD/leaderboard blocking roots, and pass that result into
  `ArenaInput` so LMB is suppressed over UI while Space remains unchanged. Do not use
  `EventSystem.current.IsPointerOverGameObject()` or treat the full-screen document root
  as blocking. Hidden templates use `display: none` so they do not remain in the picking
  tree.

### Reference boundary

Use these `GlobalStrategy` files as implementation references only; do not copy assembly
dependencies, styles, VContainer services or runtime code into MultiplayerDemo:

- `.claude/rules/unity/uitoolkit.md`
- `.claude/rules/unity/ui_implementation.md`
- `Assets/Scripts/Unity/UI/MainMenuDocument.cs`
- `Assets/Scripts/Unity/UI/MainMenuView.cs`
- `Assets/UI/Modal/MainMenu/MainMenu.uxml`
- `Assets/UI/Modal/MainMenu/MainMenu.uss`

The applicable patterns are root UXML composition, shared/local USS separation,
`PanelRenderer.RegisterUIReloadCallback`, binding-MonoBehaviour lifecycle, and a plain C#
view. `GlobalStrategy` remains a separate project and is not a package or asset source.

## Agent Steps

- [ ] **Add pure presentation state and tests first** — Define display-ready connect,
  HUD, discovery and roster state plus a projector; extend formatting/state tests for
  disconnected, connecting, rejected, failed, playing, alive, dead, cooldown-ready,
  leaderboard-visible and local-player-row cases, plus pointer-over-UI mouse-fire
  suppression and unchanged keyboard-fire behavior.
- [ ] **Author the UI Toolkit asset tree** — Create the shared/root USS, root UXML and
  connect/HUD/leaderboard UXML+USS templates with stable names for every queried control,
  label, list container and visibility root; create the single scaled `PanelSettings`.
- [ ] **Implement and test the plain view** — Add `ArenaHudView`, render projected state
  into a cloned UI tree, update dynamic discovery/roster rows without leaking stale rows,
  and add EditMode assertions for visibility, text, columns, selection and callback
  controls.
- [ ] **Implement the document binding** — Replace `ArenaHud` with
  `ArenaHudDocument`; own reload/lifecycle and UI callbacks, bind/rebind safely after a UI
  reload, and refresh the view from authoritative client/ECS/discovery state.
- [ ] **Wire the composition root and scene** — Update `ArenaClientBootstrap` to bind its
  serialized document explicitly; update `Arena.unity` with one UI object containing the
  one `PanelRenderer`, `ArenaHudDocument`, root UXML and `ArenaHudPanelSettings`; remove all
  runtime IMGUI creation and ensure no `OnGUI`, `GUILayout`, Canvas or uGUI remains.
- [ ] **Update Unity client documentation** — Revise `UnityClient/README.md` structure,
  asset-authorship statement, tests and build notes to describe UI Toolkit, the one-panel
  architecture and unchanged standalone/WebGL controls.
- [ ] **Run automated regression checks** — Run Unity EditMode tests, server unit tests,
  Web client tests/build and the existing live two-bot integration test; resolve only
  regressions introduced by the UI migration.
- [ ] **Verify in live Unity targets** — In Unity 6000.5.5f1, confirm a clean compile and
  Play Mode connection/discovery flow, readable rejection/failure state, responsive HUD,
  HP/cooldown/death/respawn/timer updates, TAB leaderboard rows, window resizing and no
  console errors; make and run a standalone build, then make a WebGL build and verify the
  same UI flow and browser WebSocket/HTTP discovery compatibility.

## User Steps

None. The agent performs Editor, standalone and WebGL verification through the available
Unity/browser tooling; if a required Editor or browser target is unavailable, the agent
must report the unverified item rather than treating it as passed.

## Tests

- **Pure EditMode:** retain `ArenaFormat` and input-vector coverage; add UI projection
  tests for every connection/play state, elapsed/cooldown/respawn formatting, discovered
  servers, roster columns/status/ping, local-player marking and leaderboard visibility.
- **UI tree EditMode:** clone `ArenaHud.uxml`, construct `ArenaHudView`, refresh with
  representative states and assert template visibility, label/field values, dynamic row
  replacement, all seven leaderboard columns, server selection and refresh callbacks,
  plus that edited name/address values survive refresh and are the values passed to
  Connect.
- **Unity integration:** compile/reload the document, run all EditMode tests, exercise
  Play Mode against a live server, then validate standalone and WebGL builds for the same
  connect/HUD/leaderboard behavior.
- **Cross-project regression:** run `dotnet test Server/MultiplayerDemo.sln`, Web client
  unit/build checks, and the two-bot live kill/respawn test to demonstrate that the UI-only
  migration did not change the authoritative feature.

## Risks

- UI Toolkit asset references and scene YAML are easiest to validate through a live Unity
  import; compilation alone does not prove UXML template paths or serialized references.
- Per-frame network/ECS changes require refreshes, but dynamic server/roster rows should
  be rebuilt only when their projected values change to avoid unnecessary allocations.
- WebGL must not acquire editor-only APIs through view tests or runtime code; keep
  `AssetDatabase` use confined to Editor tests and retain the existing transport split.

## Constitution Check

No conflicts found — plan aligns with all principles.

Use the implement skill to start working on the plan or request changes.
