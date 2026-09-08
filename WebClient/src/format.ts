/** `HH:MM:SS`, hours unbounded — the match never ends. Negative input clamps to zero. */
export function formatElapsed(elapsedMs: number): string {
	const total = Math.max(0, Math.floor(elapsedMs / 1000));
	const hours = Math.floor(total / 3600);
	const minutes = Math.floor((total % 3600) / 60);
	const seconds = total % 60;
	return `${pad(hours)}:${pad(minutes)}:${pad(seconds)}`;
}

/** One decimal, e.g. `1.4` — used for the respawn and cooldown countdowns. */
export function formatSeconds(ms: number): string {
	return (Math.max(0, ms) / 1000).toFixed(1);
}

function pad(value: number): string {
	return value.toString().padStart(2, '0');
}
