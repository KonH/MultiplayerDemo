/** Server space has `y` pointing up the screen, so the "up" keys produce +y. */
const MOVE_KEYS: Record<string, { x: number; y: number }> = {
	KeyW: { x: 0, y: 1 },
	ArrowUp: { x: 0, y: 1 },
	KeyS: { x: 0, y: -1 },
	ArrowDown: { x: 0, y: -1 },
	KeyA: { x: -1, y: 0 },
	ArrowLeft: { x: -1, y: 0 },
	KeyD: { x: 1, y: 0 },
	ArrowRight: { x: 1, y: 0 }
};

export interface MoveVector {
	mx: number;
	my: number;
}

export function isMoveKey(code: string): boolean {
	return code in MOVE_KEYS;
}

/**
 * Sums the held direction keys and normalises, so a diagonal is not faster than a straight
 * line. Opposite keys cancel to zero, which the server reads as "stand still".
 */
export function moveVector(pressed: Iterable<string>): MoveVector {
	let x = 0;
	let y = 0;
	for (const code of pressed) {
		const delta = MOVE_KEYS[code];
		if (delta) {
			x += delta.x;
			y += delta.y;
		}
	}
	const length = Math.hypot(x, y);
	if (length < 1e-6) {
		return { mx: 0, my: 0 };
	}
	return { mx: round(x / length), my: round(y / length) };
}

function round(value: number): number {
	return Math.round(value * 1000) / 1000;
}
