import { describe, expect, it } from 'vitest';
import type { StateMessage } from '../src/protocol.js';
import { SnapshotBuffer, interpolate } from '../src/snapshots.js';

function state(
	tick: number,
	players: Array<[number, number, number]>,
	bullets: Array<[number, number, number]> = []
): StateMessage {
	return {
		t: 'state',
		tick,
		elapsedMs: tick * 1000,
		players: players.map(([id, x, y]) => ({ id, x, y, hp: 5, dx: 0, dy: 1, cd: 0 })),
		bullets: bullets.map(([id, x, y]) => ({ id, x, y, o: 1 }))
	};
}

describe('interpolate', () => {
	it('lerps positions of entities present in both snapshots', () => {
		const frame = interpolate(state(1, [[1, 0, 0]]), state(2, [[1, 10, -4]]), 0.5);
		expect(frame.players[0]).toMatchObject({ id: 1, x: 5, y: -2 });
	});

	it('clamps t outside [0,1]', () => {
		expect(interpolate(state(1, [[1, 0, 0]]), state(2, [[1, 10, 0]]), 2).players[0]!.x).toBe(10);
		expect(interpolate(state(1, [[1, 0, 0]]), state(2, [[1, 10, 0]]), -1).players[0]!.x).toBe(0);
	});

	it('places a newly appeared entity at its real position, not lerped from a neighbour', () => {
		const frame = interpolate(state(1, [[1, 0, 0]]), state(2, [[1, 1, 0], [2, 18, -18]]), 0.5);
		expect(frame.players).toHaveLength(2);
		expect(frame.players.find((p) => p.id === 2)).toMatchObject({ x: 18, y: -18 });
	});

	it('drops an entity that disappeared in the newer snapshot', () => {
		const frame = interpolate(state(1, [[1, 0, 0], [2, 5, 5]]), state(2, [[1, 1, 0]]), 0.5);
		expect(frame.players.map((p) => p.id)).toEqual([1]);
	});

	it('handles bullets appearing and disappearing the same way', () => {
		const appear = interpolate(state(1, [], []), state(2, [], [[9, 3, 3]]), 0.5);
		expect(appear.bullets).toEqual([{ id: 9, x: 3, y: 3, owner: 1 }]);
		const vanish = interpolate(state(1, [], [[9, 3, 3]]), state(2, [], []), 0.5);
		expect(vanish.bullets).toEqual([]);
	});

	it('takes hp, facing and cooldown from the newer snapshot', () => {
		const older = state(1, [[1, 0, 0]]);
		const newer = state(2, [[1, 2, 0]]);
		newer.players[0]!.hp = 3;
		newer.players[0]!.cd = 400;
		const frame = interpolate(older, newer, 0.25);
		expect(frame.players[0]!.hp).toBe(3);
		expect(frame.players[0]!.cd).toBe(400);
	});
});

describe('SnapshotBuffer', () => {
	it('returns an empty frame before anything arrives', () => {
		const buffer = new SnapshotBuffer(100);
		expect(buffer.sample(1000).players).toEqual([]);
	});

	it('samples 100 ms in the past, between the bracketing snapshots', () => {
		const buffer = new SnapshotBuffer(100);
		buffer.push(state(1, [[1, 0, 0]]), 1000);
		buffer.push(state(2, [[1, 10, 0]]), 1100);
		// now = 1200 -> target 1100 -> exactly the newer snapshot.
		expect(buffer.sample(1200).players[0]!.x).toBe(10);
		// now = 1150 -> target 1050 -> halfway.
		expect(buffer.sample(1150).players[0]!.x).toBeCloseTo(5, 6);
	});

	it('holds the oldest pose when the buffer has not filled the delay yet', () => {
		const buffer = new SnapshotBuffer(100);
		buffer.push(state(1, [[1, 7, 7]]), 1000);
		buffer.push(state(2, [[1, 9, 9]]), 1033);
		expect(buffer.sample(1010).players[0]).toMatchObject({ x: 7, y: 7 });
	});

	it('holds the newest pose instead of extrapolating when snapshots stop arriving', () => {
		const buffer = new SnapshotBuffer(100);
		buffer.push(state(1, [[1, 0, 0]]), 1000);
		buffer.push(state(2, [[1, 4, 0]]), 1033);
		expect(buffer.sample(5000).players[0]!.x).toBe(4);
	});

	it('picks the right pair across a longer history', () => {
		const buffer = new SnapshotBuffer(100);
		for (let i = 0; i < 10; i++) {
			buffer.push(state(i + 1, [[1, i, 0]]), 1000 + (i * 33));
		}
		// target 1099 sits between the snapshots at 1099 (i=3) exactly.
		expect(buffer.sample(1199).players[0]!.x).toBeCloseTo(3, 6);
		expect(buffer.sample(1215).players[0]!.x).toBeGreaterThan(3);
		expect(buffer.sample(1215).players[0]!.x).toBeLessThan(4);
	});

	it('ignores duplicate and out-of-order ticks', () => {
		const buffer = new SnapshotBuffer(100);
		buffer.push(state(5, [[1, 0, 0]]), 1000);
		buffer.push(state(5, [[1, 9, 9]]), 1010);
		buffer.push(state(4, [[1, 8, 8]]), 1020);
		expect(buffer.size).toBe(1);
		expect(buffer.sample(2000).players[0]).toMatchObject({ x: 0, y: 0 });
	});

	it('prunes old snapshots but always keeps enough to interpolate', () => {
		const buffer = new SnapshotBuffer(100, 300);
		for (let i = 0; i < 60; i++) {
			buffer.push(state(i + 1, [[1, i, 0]]), 1000 + (i * 33));
		}
		expect(buffer.size).toBeLessThan(20);
		expect(buffer.size).toBeGreaterThanOrEqual(2);
	});

	it('clears on reconnect', () => {
		const buffer = new SnapshotBuffer(100);
		buffer.push(state(1, [[1, 0, 0]]), 1000);
		buffer.clear();
		expect(buffer.size).toBe(0);
		expect(buffer.latest).toBeNull();
	});
});
