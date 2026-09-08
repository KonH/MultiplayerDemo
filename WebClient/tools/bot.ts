/**
 * Headless bot client. Runs the same `ArenaConnection` the browser does, on Node's global
 * WebSocket, so the wire path exercised here is the shipped one.
 *
 *   npx tsx tools/bot.ts --name X --server localhost:8080 --behaviour wander --fire
 */
import { pathToFileURL } from 'node:url';
import { parseArgs } from 'node:util';
import { ArenaConnection } from '../src/connection.js';
import { normaliseAddress, socketUrl } from '../src/discovery.js';
import type {
	EventMessage,
	RosterPlayer,
	StateMessage,
	StatePlayer,
	WelcomeMessage
} from '../src/protocol.js';

const INPUT_INTERVAL_MS = 1000 / 30;

export interface BotOptions {
	name: string;
	server: string;
	log?: (line: string) => void;
}

export class BotClient {
	readonly name: string;
	readonly address: string;
	welcome: WelcomeMessage | null = null;
	state: StateMessage | null = null;
	roster: RosterPlayer[] = [];
	readonly events: EventMessage[] = [];
	closedReason: string | null = null;

	#connection: ArenaConnection | null = null;
	#timer: NodeJS.Timeout | null = null;
	#move = { mx: 0, my: 0 };
	#fire = false;
	#log: (line: string) => void;

	constructor(options: BotOptions) {
		this.name = options.name;
		this.address = normaliseAddress(options.server);
		this.#log = options.log ?? ((line) => console.log(line));
	}

	get id(): number {
		return this.welcome?.id ?? -1;
	}

	self(): StatePlayer | null {
		return this.state?.players.find((player) => player.id === this.id) ?? null;
	}

	rosterEntry(name: string): RosterPlayer | null {
		return this.roster.find((player) => player.name === name) ?? null;
	}

	playerNamed(name: string): StatePlayer | null {
		const entry = this.rosterEntry(name);
		if (!entry) {
			return null;
		}
		return this.state?.players.find((player) => player.id === entry.id) ?? null;
	}

	setMove(mx: number, my: number): void {
		this.#move = { mx, my };
	}

	setFire(fire: boolean): void {
		this.#fire = fire;
	}

	log(line: string): void {
		this.#log(`[${this.name}] ${line}`);
	}

	connect(timeoutMs = 5000): Promise<WelcomeMessage> {
		return new Promise((resolve, reject) => {
			const timer = setTimeout(() => reject(new Error(`${this.name}: join timed out`)), timeoutMs);
			let settled = false;
			const finish = (error: Error | null, welcome?: WelcomeMessage): void => {
				if (settled) {
					return;
				}
				settled = true;
				clearTimeout(timer);
				if (error) {
					reject(error);
				} else {
					resolve(welcome!);
				}
			};

			this.#connection = new ArenaConnection(socketUrl(this.address), this.name, {
				onWelcome: (welcome) => {
					this.welcome = welcome;
					this.log(
						`joined as id ${welcome.id}; arena ${welcome.arena.minX}..${welcome.arena.maxX} x ` +
							`${welcome.arena.minY}..${welcome.arena.maxY}, hp ${welcome.maxHealth}, ` +
							`respawn ${welcome.respawnMs}ms, cooldown ${welcome.fireCooldownMs}ms`
					);
					finish(null, welcome);
				},
				onReject: (message) => {
					this.log(`rejected: ${message.reason}`);
					finish(new Error(`${this.name}: rejected — ${message.reason}`));
				},
				onState: (state) => {
					this.state = state;
				},
				onRoster: (roster) => {
					const before = new Set(this.roster.map((player) => `${player.name}:${player.connected}`));
					this.roster = roster.players;
					for (const player of roster.players) {
						if (!before.has(`${player.name}:${player.connected}`)) {
							this.log(
								`roster: ${player.name} ${player.connected ? 'connected' : 'disconnected'} ` +
									`(${player.kills}k/${player.deaths}d, ${player.client}, ${player.ip}, ${player.ping}ms)`
							);
						}
					}
				},
				onEvent: (event) => {
					this.events.push(event);
					this.log(`event ${event.kind} victim=${this.#nameOf(event.victim)} killer=${this.#nameOf(event.killer)}`);
				},
				onClose: (reason) => {
					this.closedReason = reason;
					this.log(`closed: ${reason}`);
					finish(new Error(`${this.name}: ${reason}`));
				}
			});

			this.#timer = setInterval(() => {
				this.#connection?.sendInput(this.#move.mx, this.#move.my, this.#fire);
			}, INPUT_INTERVAL_MS);
		});
	}

	close(): void {
		if (this.#timer) {
			clearInterval(this.#timer);
			this.#timer = null;
		}
		this.#connection?.close();
	}

	/** Polls at input rate; every assertion in the integration test is expressed through this. */
	async waitFor(label: string, predicate: () => boolean, timeoutMs = 8000): Promise<void> {
		const deadline = Date.now() + timeoutMs;
		while (Date.now() < deadline) {
			if (predicate()) {
				return;
			}
			await delay(INPUT_INTERVAL_MS);
		}
		throw new Error(`${this.name}: timed out waiting for ${label}`);
	}

	#nameOf(id: number): string {
		return this.roster.find((player) => player.id === id)?.name ?? `#${id}`;
	}
}

export function delay(ms: number): Promise<void> {
	return new Promise((resolve) => setTimeout(resolve, ms));
}

/** Unit vector from `from` to `to`; zero when they coincide. */
export function directionTo(
	from: { x: number; y: number },
	to: { x: number; y: number }
): { mx: number; my: number } {
	const dx = to.x - from.x;
	const dy = to.y - from.y;
	const length = Math.hypot(dx, dy);
	if (length < 1e-4) {
		return { mx: 0, my: 0 };
	}
	return { mx: dx / length, my: dy / length };
}

async function main(): Promise<void> {
	const { values } = parseArgs({
		options: {
			name: { type: 'string', default: `bot-${Math.floor(Math.random() * 900 + 100)}` },
			server: { type: 'string', default: 'localhost:8080' },
			behaviour: { type: 'string', default: 'wander' },
			chase: { type: 'string' },
			fire: { type: 'boolean', default: false },
			duration: { type: 'string' }
		},
		allowPositionals: false
	});

	const bot = new BotClient({ name: values.name!, server: values.server! });
	const welcome = await bot.connect();
	bot.setFire(values.fire === true);

	const endAt = values.duration ? Date.now() + (Number(values.duration) * 1000) : Number.POSITIVE_INFINITY;
	const arena = welcome.arena;
	let heading = Math.random() * Math.PI * 2;
	let lastReport = 0;

	process.on('SIGINT', () => {
		bot.close();
		process.exit(0);
	});

	while ((Date.now() < endAt) && (bot.closedReason === null)) {
		const self = bot.self();
		if (values.chase) {
			const target = bot.playerNamed(values.chase);
			if (self && target) {
				const direction = directionTo(self, target);
				bot.setMove(direction.mx, direction.my);
				bot.setFire(true);
			} else {
				bot.setMove(0, 0);
			}
		} else if (values.behaviour === 'wander') {
			if (self) {
				// Turn away from the walls rather than grinding along them.
				const margin = 3;
				if (self.x < (arena.minX + margin)) {
					heading = 0;
				} else if (self.x > (arena.maxX - margin)) {
					heading = Math.PI;
				} else if (self.y < (arena.minY + margin)) {
					heading = Math.PI / 2;
				} else if (self.y > (arena.maxY - margin)) {
					heading = -Math.PI / 2;
				} else {
					heading += (Math.random() - 0.5) * 0.6;
				}
				bot.setMove(Math.cos(heading), Math.sin(heading));
			}
		} else {
			bot.setMove(0, 0);
		}

		const now = Date.now();
		if ((now - lastReport) > 2000) {
			lastReport = now;
			const state = bot.state;
			const me = bot.self();
			bot.log(
				`tick ${state?.tick ?? 0} elapsed ${state?.elapsedMs ?? 0}ms · ` +
					`players ${state?.players.length ?? 0} bullets ${state?.bullets.length ?? 0} · ` +
					(me ? `me (${me.x.toFixed(1)}, ${me.y.toFixed(1)}) hp ${me.hp} cd ${me.cd}ms` : 'me: dead')
			);
		}
		await delay(100);
	}

	bot.close();
}

// Only run the CLI when this file is the entry point, not when the integration test imports it.
if (process.argv[1] && (import.meta.url === pathToFileURL(process.argv[1]).href)) {
	main().catch((error: unknown) => {
		console.error(error instanceof Error ? error.message : error);
		process.exit(1);
	});
}
