# MultiplayerDemo

A never-ending top-down deathmatch arena with **one authoritative C# server** and **two
independent clients** — a Unity DOTS 3D client and a TypeScript 2D web client — that share
the same world.

```
Server/       C# (.NET 9) authoritative server: simulation, WebSocket, HTTP, LAN beacon
WebClient/    TypeScript + Vite, 2D canvas client
UnityClient/  Unity 6000.5.5f1, URP, DOTS/Entities 3D client
docs/specs/   Spec, plan and the frozen wire protocol
```

## Rules

Green box = you, red boxes = everyone else. WASD / arrows to move (no rotation), LMB or
Space to shoot in the direction you last moved. 0.5 s cooldown, 1 damage per hit, 5 HP,
5 s respawn at a random free spot. TAB shows the leaderboard. The match never ends.

## Run the server

```sh
dotnet run --project Server/MultiplayerDemo.Server            # port 8080
dotnet run --project Server/MultiplayerDemo.Server -- --port 8081 --name "Second Arena"
dotnet test Server                                            # simulation unit tests
```

- Game socket: `ws://<host>:<port>/ws`
- Server info: `GET http://<host>:<port>/api/info`
- LAN discovery: UDP broadcast on port `47777` once a second

Several clients on one machine can all connect to `localhost` — each is a separate player,
distinguished by its unique name.

## Run the clients

See `WebClient/README.md` and `UnityClient/README.md`.

## Documentation

- `docs/constitution.md` — non-negotiable architectural principles
- `docs/specs/26_09_08_09_multiplayer_arena/spec.md` — requirements and acceptance criteria
- `docs/specs/26_09_08_09_multiplayer_arena/plan.md` — implementation plan
- `docs/specs/26_09_08_09_multiplayer_arena/protocol.md` — the wire protocol both clients implement
