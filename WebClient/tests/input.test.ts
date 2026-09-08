import { describe, expect, it } from 'vitest';
import { isMoveKey, moveVector } from '../src/input.js';

describe('moveVector', () => {
	it('is zero with nothing pressed', () => {
		expect(moveVector([])).toEqual({ mx: 0, my: 0 });
	});

	it('maps screen-up keys to +y in server space', () => {
		expect(moveVector(['KeyW'])).toEqual({ mx: 0, my: 1 });
		expect(moveVector(['ArrowUp'])).toEqual({ mx: 0, my: 1 });
		expect(moveVector(['KeyS'])).toEqual({ mx: 0, my: -1 });
		expect(moveVector(['ArrowDown'])).toEqual({ mx: 0, my: -1 });
	});

	it('maps left and right', () => {
		expect(moveVector(['KeyA'])).toEqual({ mx: -1, my: 0 });
		expect(moveVector(['ArrowRight'])).toEqual({ mx: 1, my: 0 });
	});

	it('normalises diagonals so they are not faster', () => {
		const diagonal = moveVector(['KeyW', 'KeyD']);
		expect(Math.hypot(diagonal.mx, diagonal.my)).toBeCloseTo(1, 3);
		expect(diagonal.mx).toBeCloseTo(Math.SQRT1_2, 3);
		expect(diagonal.my).toBeCloseTo(Math.SQRT1_2, 3);
	});

	it('cancels opposite keys', () => {
		expect(moveVector(['KeyW', 'KeyS'])).toEqual({ mx: 0, my: 0 });
		expect(moveVector(['KeyA', 'ArrowRight'])).toEqual({ mx: 0, my: 0 });
	});

	it('treats WASD and arrows as the same axis', () => {
		expect(moveVector(['KeyW', 'ArrowUp'])).toEqual({ mx: 0, my: 1 });
	});

	it('never exceeds unit length with every key held', () => {
		const all = moveVector(['KeyW', 'KeyA', 'KeyS', 'KeyD', 'ArrowUp', 'ArrowLeft']);
		expect(Math.hypot(all.mx, all.my)).toBeLessThanOrEqual(1.0001);
	});

	it('ignores unrelated keys', () => {
		expect(moveVector(['Space', 'KeyQ', 'KeyD'])).toEqual({ mx: 1, my: 0 });
	});

	it('accepts a Set as well as an array', () => {
		expect(moveVector(new Set(['KeyD']))).toEqual({ mx: 1, my: 0 });
	});
});

describe('isMoveKey', () => {
	it('recognises the movement keys only', () => {
		expect(isMoveKey('KeyW')).toBe(true);
		expect(isMoveKey('ArrowLeft')).toBe(true);
		expect(isMoveKey('Space')).toBe(false);
		expect(isMoveKey('Tab')).toBe(false);
	});
});
