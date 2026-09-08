/**
 * Two headless bots against a live server: they must see each other, one must kill the other,
 * and the score plus respawn must follow. Acceptance criterion 9 of the spec.
 *
 *   npm run test:integration -- --server localhost:8080
 *
 * Exits non-zero on the first failed assertion.
 */
import { parseArgs } from 'node:util';
import { BotClient, delay, directionTo } from './bot.js';
import { infoUrl, normaliseAddress, probeServer } from '../src/discovery.js';

const { values } = parseArgs({
	options: { server: { type: 'string', default: 'localhost:8080' } },
	allowPositionals: false
});

const server = normaliseAddress(values.server!);
const suffix = Date.now().toString(36).slice(-4);
const killerName = `it-killer-${suffix}`;
const victimName = `it-victim-${suffix}`;

let failures = 0;

function check(label: string, condition: boolean, detail = ''): void {
	if (condition) {
		console.log(`  PASS  ${label}${detail ? ` — ${detail}` : ''}`);
	} else {
		failures++;
		console.error(`  FAIL  ${label}${detail ? ` — ${detail}` : ''}`);
	}
}

function step(label: string): void {
	console.log(`\n== ${label}`);
}

async function run(): Promise<void> {
	step(`Probing ${infoUrl(server)}`);
	const probe = await probeServer(server, 2000);
	if (!probe) {
		throw new Error(`No server answering at ${server}. Start it with: dotnet run --project Server/MultiplayerDemo.Server`);
	}
	console.log(`  server "${probe.info.name}" v${probe.info.version}, ${probe.info.players}/${probe.info.maxPlayers} players, tick ${probe.info.tickRate}`);

	const killer = new BotClient({ name: killerName, server });
	const victim = new BotClient({ name: victimName, server });

	try {
		step('Joining two bots with distinct names');
		await killer.connect();
		await victim.connect();
		check('both bots got a welcome', (killer.id > 0) && (victim.id > 0), `ids ${killer.id} and ${victim.id}`);
		check('bots have distinct ids', killer.id !== victim.id);

		step('Each bot observes the other in state and roster');
		await killer.waitFor('killer sees victim in roster', () => killer.rosterEntry(victimName) !== null);
		await victim.waitFor('victim sees killer in roster', () => victim.rosterEntry(killerName) !== null);
		await killer.waitFor('killer sees victim in state', () => killer.playerNamed(victimName) !== null);
		await victim.waitFor('victim sees killer in state', () => victim.playerNamed(killerName) !== null);
		check('killer sees victim in roster + state', killer.playerNamed(victimName) !== null);
		check('victim sees killer in roster + state', victim.playerNamed(killerName) !== null);

		const killerBefore = killer.rosterEntry(killerName)!;
		const victimBefore = victim.rosterEntry(victimName)!;
		const killsBefore = killerBefore.kills;
		const deathsBefore = victimBefore.deaths;
		console.log(`  baseline: ${killerName} kills=${killsBefore}, ${victimName} deaths=${deathsBefore}`);

		step('Killer chases and shoots the victim until it dies');
		victim.setMove(0, 0);
		victim.setFire(false);
		const killDeadline = Date.now() + 30_000;
		let killEventAt = 0;
		while (Date.now() < killDeadline) {
			const me = killer.self();
			const target = killer.playerNamed(victimName);
			if (me && target) {
				const direction = directionTo(me, target);
				killer.setMove(direction.mx, direction.my);
				killer.setFire(true);
			}
			const killEvent = killer.events.find(
				(event) => (event.kind === 'kill') && (event.victim === victim.id) && (event.killer === killer.id)
			);
			if (killEvent) {
				killEventAt = Date.now();
				break;
			}
			await delay(30);
		}
		killer.setFire(false);
		killer.setMove(0, 0);
		check('killer killed the victim', killEventAt > 0);
		if (killEventAt === 0) {
			return;
		}

		step('Victim leaves the arena and the score updates');
		check('victim is absent from the snapshot after death', killer.playerNamed(victimName) === null);

		await killer.waitFor(
			'killer kills incremented',
			() => (killer.rosterEntry(killerName)?.kills ?? 0) > killsBefore,
			3000
		);
		await victim.waitFor(
			'victim deaths incremented',
			() => (victim.rosterEntry(victimName)?.deaths ?? 0) > deathsBefore,
			3000
		);
		const killsAfter = killer.rosterEntry(killerName)!.kills;
		const deathsAfter = victim.rosterEntry(victimName)!.deaths;
		check('killer kills +1', killsAfter === (killsBefore + 1), `${killsBefore} -> ${killsAfter}`);
		check('victim deaths +1', deathsAfter === (deathsBefore + 1), `${deathsBefore} -> ${deathsAfter}`);
		check(
			'victim reports a respawn countdown',
			(victim.rosterEntry(victimName)?.respawnIn ?? 0) > 0,
			`${victim.rosterEntry(victimName)?.respawnIn}ms left`
		);

		step('Victim respawns within ~5 s');
		await victim.waitFor('victim back in its own state', () => victim.self() !== null, 8000);
		const respawnDelay = Date.now() - killEventAt;
		check('respawn took roughly the configured 5 s', (respawnDelay > 4000) && (respawnDelay < 7500), `${respawnDelay} ms`);
		await killer.waitFor('killer sees the victim again', () => killer.playerNamed(victimName) !== null, 3000);
		check('killer sees the respawned victim', killer.playerNamed(victimName) !== null);
		check('victim is back to full health', (victim.self()?.hp ?? 0) === victim.welcome!.maxHealth, `hp ${victim.self()?.hp}`);
	} finally {
		killer.close();
		victim.close();
		await delay(200);
	}
}

run()
	.then(() => {
		console.log(`\n${failures === 0 ? 'INTEGRATION PASSED' : `INTEGRATION FAILED (${failures} check(s))`}`);
		process.exit(failures === 0 ? 0 : 1);
	})
	.catch((error: unknown) => {
		console.error(`\nINTEGRATION FAILED — ${error instanceof Error ? error.message : String(error)}`);
		process.exit(1);
	});
