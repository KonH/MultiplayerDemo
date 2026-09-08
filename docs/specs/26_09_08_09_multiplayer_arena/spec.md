# Spec — Multiplayer Arena Demo

## Intent

A never-ending top-down deathmatch arena. One authoritative C# server holds the single
source of truth. Two independent clients render the same world:

- **Unity client** — 3D, DOTS/ECS gameplay representation, Unity UI Toolkit interface,
  standalone + WebGL builds.
- **Web client** — 2D canvas, TypeScript, browser.

Clients are *view + input* only. All simulation, collision, damage, respawn and scoring
happen on the server. Any number of clients (including several on one machine, all on
`localhost`) can be connected at once and must see a consistent world.

## Actors

- **Player** — a human running either client. Identified by a unique display name.
- **Server** — headless C# process. Simulates the arena, broadcasts snapshots.

## Functional requirements

### FR-1 Connect
- The player picks a **server** (typed `host:port`, or an entry from a discovered list)
  and a **unique name**, then connects.
- A name already in use by a *connected* player is rejected with a readable reason.
- Reconnecting with a name belonging to a *disconnected* player resumes that player's
  leaderboard record (kills/deaths preserved).
- Two clients started on the same machine, both pointing at `localhost`, connect
  independently and are treated as two distinct players.

### FR-2 Discovery
- The server announces itself on the LAN via a UDP broadcast beacon.
- The server exposes an HTTP `GET /api/info` describing itself.
- Native clients (Unity standalone) discover servers by listening to the beacon.
- Browser clients (Web, Unity WebGL) discover servers by probing `/api/info` on a small
  set of candidate ports on the page host / `localhost`.
- Both clients always also accept a manually typed address.

### FR-3 Arena and movement
- The arena is a finite rectangular plane. Characters cannot leave it.
- The local player's character is a **green box**; all other players are **red boxes**.
- Movement is WASD / arrow keys, 8-directional, constant speed, **no rotation**.
- Characters cannot overlap each other; the server resolves collisions.

### FR-4 Shooting
- Fire with **LMB** or **Space**, in the direction of the last non-zero movement.
- Cooldown 0.5 s between shots; the remaining cooldown is shown in the UI.
- A projectile hit deals 1 damage and is consumed. Projectiles never hit their owner.

### FR-5 Health, death, respawn
- Health is 5 / 5. Reaching 0 is death; the killer gains a kill, the victim a death.
- A dead character is removed from the arena and respawns after 5 s at a random
  position not occupied by another player. The countdown is shown in the UI.
- Current HP is shown in the UI.

### FR-6 Leaderboard
- Toggled with **TAB**. Columns: name, kills, deaths, status (connected / disconnected),
  IP, client type (Unity / Web), ping (ms).
- Disconnected players remain listed until the server is restarted.

### FR-7 Match timer
- The game never ends. A human-readable elapsed timer (`HH:MM:SS`) since server start is
  always visible.

## Unity client UI architecture

The Unity client's connect screen, in-game HUD and TAB leaderboard use Unity UI Toolkit
exclusively, following the established `GlobalStrategy` project pattern:

- UI structure and styling are authored as UXML and USS assets under `UnityClient/Assets/UI/`.
- A `PanelSettings` asset supplies the shared panel configuration, and scene UI is rendered
  through `PanelRenderer`. If the HUD and leaderboard are separate surfaces, their draw order
  is set explicitly with integer `PanelRenderer.sortingOrder` values.
- Each UI surface has a binding MonoBehaviour that owns lifecycle and event subscriptions,
  registers for UI reloads, queries the named root element, and passes current client state to
  a plain C# view object. The view owns VisualElement queries and presentation updates, and has
  no networking, ECS, scene lookup or dependency-resolution responsibility.
- The UI reflects the existing authoritative client/ECS state; it does not introduce client-side
  gameplay state or simulation.
- IMGUI (`OnGUI`/`GUILayout`) and Canvas-based uGUI are not used for the Unity client UI.

## Acceptance criteria

1. Server starts, prints its address, answers `GET /api/info`, and beacons on UDP.
2. A Web client and a Unity client connected from the same machine to `localhost` both
   see each other move, in the correct colours, in real time.
3. Walking into the arena wall stops the character at the wall; walking into another
   player stops the character against them, in both clients.
4. Firing produces a projectile in both clients; a hit reduces the target's HP by 1 in
   both clients' UI.
5. Reaching 0 HP removes the character, starts a 5 s countdown shown in the shooter's
   *victim's* UI, and respawns the character at a free spot; kills/deaths update.
6. TAB toggles a leaderboard listing every player who has ever connected this session,
   with a live ping and a status that flips to `disconnected` when a client closes.
7. The elapsed timer advances and matches between the two clients.
8. Server-side unit tests cover: arena clamping, player-vs-player resolution, fire
   cooldown, projectile hit/damage/kill, respawn placement not overlapping players.
9. An automated headless client test connects two bot clients to a live server and
   asserts they observe each other and a kill sequence.

## Out of scope

- Client-side prediction / rollback (clients interpolate the authoritative state).
- Persistence between server restarts, authentication, matchmaking, teams, obstacles.
