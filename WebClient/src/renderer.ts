import type { ArenaBounds } from './protocol.js';
import type { RenderFrame } from './snapshots.js';

export interface ViewTransform {
	scale: number;
	offsetX: number;
	offsetY: number;
}

/**
 * Largest uniform scale that keeps the whole arena on screen, centred, with a small margin.
 * Server `y` grows up the screen, so the vertical axis is flipped on the way to canvas space.
 */
export function fitArena(
	arena: ArenaBounds,
	widthPx: number,
	heightPx: number,
	marginPx = 24
): ViewTransform {
	const arenaWidth = Math.max(1e-3, arena.maxX - arena.minX);
	const arenaHeight = Math.max(1e-3, arena.maxY - arena.minY);
	const usableWidth = Math.max(1, widthPx - (marginPx * 2));
	const usableHeight = Math.max(1, heightPx - (marginPx * 2));
	const scale = Math.min(usableWidth / arenaWidth, usableHeight / arenaHeight);
	return {
		scale,
		offsetX: (widthPx - (arenaWidth * scale)) / 2,
		offsetY: (heightPx - (arenaHeight * scale)) / 2
	};
}

export function worldToScreenX(x: number, arena: ArenaBounds, view: ViewTransform): number {
	return view.offsetX + ((x - arena.minX) * view.scale);
}

export function worldToScreenY(y: number, arena: ArenaBounds, view: ViewTransform): number {
	return view.offsetY + ((arena.maxY - y) * view.scale);
}

export interface RenderOptions {
	arena: ArenaBounds;
	playerRadius: number;
	bulletRadius: number;
	localId: number;
	names: Map<number, string>;
}

const COLOR_BACKDROP = '#0b0f14';
const COLOR_FLOOR = '#151d27';
const COLOR_GRID = '#1e2a38';
const COLOR_BORDER = '#4c6fa5';
const COLOR_SELF = '#3ddc63';
const COLOR_OTHER = '#e04a4a';
const COLOR_BULLET = '#ffd93b';

export class ArenaRenderer {
	readonly #canvas: HTMLCanvasElement;
	readonly #context: CanvasRenderingContext2D;
	#view: ViewTransform = { scale: 1, offsetX: 0, offsetY: 0 };

	constructor(canvas: HTMLCanvasElement) {
		const context = canvas.getContext('2d');
		if (!context) {
			throw new Error('2D canvas context unavailable');
		}
		this.#canvas = canvas;
		this.#context = context;
	}

	get view(): ViewTransform {
		return this.#view;
	}

	/** Sizes the backing store to the CSS box times DPR so lines stay crisp on retina screens. */
	resize(): void {
		const dpr = Math.min(3, globalThis.devicePixelRatio || 1);
		const rect = this.#canvas.getBoundingClientRect();
		const width = Math.max(1, Math.round(rect.width * dpr));
		const height = Math.max(1, Math.round(rect.height * dpr));
		if ((this.#canvas.width !== width) || (this.#canvas.height !== height)) {
			this.#canvas.width = width;
			this.#canvas.height = height;
		}
	}

	render(frame: RenderFrame, options: RenderOptions): void {
		this.resize();
		const ctx = this.#context;
		const width = this.#canvas.width;
		const height = this.#canvas.height;
		const dpr = Math.min(3, globalThis.devicePixelRatio || 1);
		const view = fitArena(options.arena, width, height, 24 * dpr);
		this.#view = view;

		ctx.fillStyle = COLOR_BACKDROP;
		ctx.fillRect(0, 0, width, height);

		const arena = options.arena;
		const left = worldToScreenX(arena.minX, arena, view);
		const top = worldToScreenY(arena.maxY, arena, view);
		const planeWidth = (arena.maxX - arena.minX) * view.scale;
		const planeHeight = (arena.maxY - arena.minY) * view.scale;

		ctx.fillStyle = COLOR_FLOOR;
		ctx.fillRect(left, top, planeWidth, planeHeight);

		ctx.strokeStyle = COLOR_GRID;
		ctx.lineWidth = 1 * dpr;
		ctx.beginPath();
		for (let x = Math.ceil(arena.minX / 5) * 5; x <= arena.maxX; x += 5) {
			const sx = Math.round(worldToScreenX(x, arena, view)) + 0.5;
			ctx.moveTo(sx, top);
			ctx.lineTo(sx, top + planeHeight);
		}
		for (let y = Math.ceil(arena.minY / 5) * 5; y <= arena.maxY; y += 5) {
			const sy = Math.round(worldToScreenY(y, arena, view)) + 0.5;
			ctx.moveTo(left, sy);
			ctx.lineTo(left + planeWidth, sy);
		}
		ctx.stroke();

		ctx.strokeStyle = COLOR_BORDER;
		ctx.lineWidth = 3 * dpr;
		ctx.strokeRect(left, top, planeWidth, planeHeight);

		const side = options.playerRadius * 2 * view.scale;
		const fontSize = Math.max(10, Math.round(11 * dpr));
		ctx.textAlign = 'center';
		ctx.textBaseline = 'alphabetic';
		ctx.font = `${fontSize}px system-ui, sans-serif`;

		for (const player of frame.players) {
			const isSelf = player.id === options.localId;
			const cx = worldToScreenX(player.x, arena, view);
			const cy = worldToScreenY(player.y, arena, view);
			ctx.fillStyle = isSelf ? COLOR_SELF : COLOR_OTHER;
			ctx.fillRect(cx - (side / 2), cy - (side / 2), side, side);

			const name = options.names.get(player.id) ?? `#${player.id}`;
			ctx.fillStyle = '#e8eef7';
			ctx.fillText(name, cx, cy - (side / 2) - (4 * dpr));
		}

		ctx.fillStyle = COLOR_BULLET;
		const bulletPx = Math.max(2 * dpr, options.bulletRadius * view.scale);
		for (const bullet of frame.bullets) {
			const cx = worldToScreenX(bullet.x, arena, view);
			const cy = worldToScreenY(bullet.y, arena, view);
			ctx.beginPath();
			ctx.arc(cx, cy, bulletPx, 0, Math.PI * 2);
			ctx.fill();
		}
	}
}
