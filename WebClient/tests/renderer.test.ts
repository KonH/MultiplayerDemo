import { describe, expect, it } from 'vitest';
import type { ArenaBounds } from '../src/protocol.js';
import { fitArena, worldToScreenX, worldToScreenY } from '../src/renderer.js';

const arena: ArenaBounds = { minX: -20, minY: -20, maxX: 20, maxY: 20 };

describe('fitArena', () => {
	it('fits the square arena to the smaller axis and centres it', () => {
		const view = fitArena(arena, 800, 400, 0);
		expect(view.scale).toBe(10);
		expect(view.offsetX).toBe(200);
		expect(view.offsetY).toBe(0);
	});

	it('leaves the requested margin', () => {
		const view = fitArena(arena, 440, 440, 20);
		expect(view.scale).toBe(10);
	});

	it('survives a degenerate canvas', () => {
		expect(Number.isFinite(fitArena(arena, 1, 1, 24).scale)).toBe(true);
	});
});

describe('world to screen', () => {
	it('maps the arena corners to the plane corners', () => {
		const view = fitArena(arena, 400, 400, 0);
		expect(worldToScreenX(arena.minX, arena, view)).toBe(0);
		expect(worldToScreenX(arena.maxX, arena, view)).toBe(400);
		// Server y points up the screen, so maxY lands at the top (screen y 0).
		expect(worldToScreenY(arena.maxY, arena, view)).toBe(0);
		expect(worldToScreenY(arena.minY, arena, view)).toBe(400);
	});

	it('puts the origin in the middle', () => {
		const view = fitArena(arena, 400, 400, 0);
		expect(worldToScreenX(0, arena, view)).toBe(200);
		expect(worldToScreenY(0, arena, view)).toBe(200);
	});

	it('moves a player up the screen when server y increases', () => {
		const view = fitArena(arena, 400, 400, 0);
		expect(worldToScreenY(5, arena, view)).toBeLessThan(worldToScreenY(0, arena, view));
	});
});
