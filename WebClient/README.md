# Web client

2D top-down client for the arena, in TypeScript on a canvas. No UI framework.

## Run

```sh
npm install
npm run dev          # http://localhost:5173
```

Start a server first (`dotnet run --project ../Server/MultiplayerDemo.Server`). On the
connect screen, type a name and an address, or pick a server from the discovered list —
browsers cannot receive the UDP beacon, so discovery probes `GET /api/info` on ports
8080-8083 of `localhost` and of the page host.

Open the page in two tabs with two different names to play against yourself.

## Controls

WASD / arrows to move, LMB or Space to shoot, TAB for the leaderboard.

## Build

```sh
npm run build        # type-checks, then emits dist/
```

To serve the client from the game server itself, copy `dist/` into
`../Server/MultiplayerDemo.Server/wwwroot/` — the server hosts it when that folder exists,
which also puts the client on the same origin as the socket.

## Tests

```sh
npm test                                          # vitest, pure modules
npm run test:integration -- --server localhost:8080   # two bots against a live server
npx tsx tools/bot.ts --name X --server localhost:8080 --fire
```

`tools/integration.ts` connects two headless bots to a running server and asserts they see
each other, that a kill increments the killer's kills and the victim's deaths, and that the
victim respawns at full health within ~5 s. It exits non-zero on failure.

## Layout

Everything that can be tested without a DOM is a separate module: `protocol.ts` (wire
types + parsing), `snapshots.ts` (interpolation buffer), `input.ts`, `format.ts`,
`discovery.ts`, and the pure parts of `renderer.ts`. `main.ts` is the DOM shell,
`connection.ts` the socket wrapper shared with the bots.
