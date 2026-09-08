import { describe, expect, it } from 'vitest';
import { formatElapsed, formatSeconds } from '../src/format.js';

describe('formatElapsed', () => {
	it('pads every field to two digits', () => {
		expect(formatElapsed(0)).toBe('00:00:00');
		expect(formatElapsed(1000)).toBe('00:00:01');
		expect(formatElapsed(61_000)).toBe('00:01:01');
		expect(formatElapsed(3_600_000)).toBe('01:00:00');
		expect(formatElapsed(3_661_000)).toBe('01:01:01');
	});

	it('truncates sub-second remainders', () => {
		expect(formatElapsed(1999)).toBe('00:00:01');
		expect(formatElapsed(41_000)).toBe('00:00:41');
	});

	it('lets hours run past 24 — the match never ends', () => {
		expect(formatElapsed(90_000_000)).toBe('25:00:00');
		expect(formatElapsed(360_000_000)).toBe('100:00:00');
	});

	it('clamps negatives to zero', () => {
		expect(formatElapsed(-5000)).toBe('00:00:00');
	});
});

describe('formatSeconds', () => {
	it('renders one decimal', () => {
		expect(formatSeconds(3200)).toBe('3.2');
		expect(formatSeconds(450)).toBe('0.5');
		expect(formatSeconds(0)).toBe('0.0');
	});

	it('clamps negatives to zero', () => {
		expect(formatSeconds(-100)).toBe('0.0');
	});
});
