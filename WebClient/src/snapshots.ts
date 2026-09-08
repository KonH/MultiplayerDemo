import type { StateMessage } from './protocol.js';

/** How far in the past we render, in ms. Two 30 Hz ticks of slack. */
export const INTERPOLATION_DELAY_MS = 100;

export interface TimedSnapshot {
	/** Client clock time (ms) at which the snapshot arrived. */
	receivedAt: number;
	state: StateMessage;
}

export interface RenderPlayer {
	id: number;
	x: number;
	y: number;
	hp: number;
	dx: number;
	dy: number;
	cd: number;
}

export interface RenderBullet {
	id: number;
	x: number;
	y: number;
	owner: number;
}

export interface RenderFrame {
	tick: number;
	elapsedMs: number;
	players: RenderPlayer[];
	bullets: RenderBullet[];
}

const EMPTY_FRAME: RenderFrame = { tick: 0, elapsedMs: 0, players: [], bullets: [] };

function lerp(a: number, b: number, t: number): number {
	return a + ((b - a) * t);
}

/**
 * Ring of recent snapshots, sampled ~100 ms in the past so the 30 Hz stream renders smoothly.
 * Never extrapolates: past the newest snapshot it holds the last known pose, which is far less
 * jarring than entities shooting off and snapping back when a frame is late.
 */
export class SnapshotBuffer {
	readonly #snapshots: TimedSnapshot[] = [];
	readonly #delayMs: number;
	readonly #historyMs: number;

	constructor(delayMs: number = INTERPOLATION_DELAY_MS, historyMs = 2000) {
		this.#delayMs = delayMs;
		this.#historyMs = historyMs;
	}

	get size(): number {
		return this.#snapshots.length;
	}

	get delayMs(): number {
		return this.#delayMs;
	}

	get latest(): TimedSnapshot | null {
		return this.#snapshots.at(-1) ?? null;
	}

	clear(): void {
		this.#snapshots.length = 0;
	}

	push(state: StateMessage, receivedAt: number): void {
		const last = this.#snapshots.at(-1);
		// Out-of-order or duplicate ticks would break the monotonic search below.
		if (last && (state.tick <= last.state.tick)) {
			return;
		}
		this.#snapshots.push({ receivedAt, state });
		const cutoff = receivedAt - this.#historyMs;
		let drop = 0;
		while (((drop + 2) < this.#snapshots.length) && (this.#snapshots[drop]!.receivedAt < cutoff)) {
			drop++;
		}
		if (drop > 0) {
			this.#snapshots.splice(0, drop);
		}
	}

	/** `now` is the client clock; sampling happens at `now - delayMs`. */
	sample(now: number): RenderFrame {
		const target = now - this.#delayMs;
		const count = this.#snapshots.length;
		if (count === 0) {
			return EMPTY_FRAME;
		}
		const first = this.#snapshots[0]!;
		if ((count === 1) || (target <= first.receivedAt)) {
			return frameOf(first);
		}
		const last = this.#snapshots[count - 1]!;
		if (target >= last.receivedAt) {
			return frameOf(last);
		}
		let index = count - 2;
		while ((index > 0) && (this.#snapshots[index]!.receivedAt > target)) {
			index--;
		}
		const a = this.#snapshots[index]!;
		const b = this.#snapshots[index + 1]!;
		const span = b.receivedAt - a.receivedAt;
		const t = span > 0 ? (target - a.receivedAt) / span : 1;
		return interpolate(a.state, b.state, t);
	}
}

function frameOf(snapshot: TimedSnapshot): RenderFrame {
	const state = snapshot.state;
	return {
		tick: state.tick,
		elapsedMs: state.elapsedMs,
		players: state.players.map((p) => ({ id: p.id, x: p.x, y: p.y, hp: p.hp, dx: p.dx, dy: p.dy, cd: p.cd })),
		bullets: state.bullets.map((b) => ({ id: b.id, x: b.x, y: b.y, owner: b.o }))
	};
}

/**
 * Entities are keyed by id, and `b` decides who exists: something that only appears in `b`
 * pops in at its true position rather than sliding in from a stale neighbour, and something
 * that only exists in `a` is already gone. Both cases would otherwise glitch visibly.
 */
export function interpolate(a: StateMessage, b: StateMessage, rawT: number): RenderFrame {
	const t = Math.min(1, Math.max(0, rawT));
	const previousPlayers = new Map(a.players.map((p) => [p.id, p]));
	const previousBullets = new Map(a.bullets.map((x) => [x.id, x]));
	return {
		tick: b.tick,
		elapsedMs: Math.round(lerp(a.elapsedMs, b.elapsedMs, t)),
		players: b.players.map((p) => {
			const prev = previousPlayers.get(p.id);
			if (!prev) {
				return { id: p.id, x: p.x, y: p.y, hp: p.hp, dx: p.dx, dy: p.dy, cd: p.cd };
			}
			return {
				id: p.id,
				x: lerp(prev.x, p.x, t),
				y: lerp(prev.y, p.y, t),
				hp: p.hp,
				dx: p.dx,
				dy: p.dy,
				cd: p.cd
			};
		}),
		bullets: b.bullets.map((x) => {
			const prev = previousBullets.get(x.id);
			if (!prev) {
				return { id: x.id, x: x.x, y: x.y, owner: x.o };
			}
			return { id: x.id, x: lerp(prev.x, x.x, t), y: lerp(prev.y, x.y, t), owner: x.o };
		})
	};
}
