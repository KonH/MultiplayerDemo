/**
 * Wire shapes from docs/specs/26_09_08_09_multiplayer_arena/protocol.md (v1).
 * Frozen contract — do not extend without updating that document first.
 */

export type ClientKind = 'unity' | 'web' | 'unknown';

export interface ArenaBounds {
	minX: number;
	minY: number;
	maxX: number;
	maxY: number;
}

export interface WelcomeMessage {
	t: 'welcome';
	id: number;
	tickRate: number;
	arena: ArenaBounds;
	playerRadius: number;
	bulletRadius: number;
	moveSpeed: number;
	maxHealth: number;
	respawnMs: number;
	fireCooldownMs: number;
	startedAtMs: number;
	serverNowMs: number;
}

export interface RejectMessage {
	t: 'reject';
	reason: string;
}

export interface StatePlayer {
	id: number;
	x: number;
	y: number;
	hp: number;
	dx: number;
	dy: number;
	cd: number;
}

export interface StateBullet {
	id: number;
	x: number;
	y: number;
	o: number;
}

export interface StateMessage {
	t: 'state';
	tick: number;
	elapsedMs: number;
	players: StatePlayer[];
	bullets: StateBullet[];
}

export interface RosterPlayer {
	id: number;
	name: string;
	kills: number;
	deaths: number;
	connected: boolean;
	ip: string;
	client: string;
	ping: number;
	respawnIn: number;
}

export interface RosterMessage {
	t: 'roster';
	players: RosterPlayer[];
}

export interface PingMessage {
	t: 'ping';
	id: number;
}

export interface EventMessage {
	t: 'event';
	kind: 'kill' | 'hit' | 'spawn';
	victim: number;
	killer: number;
}

export type ServerMessage =
	| WelcomeMessage
	| RejectMessage
	| StateMessage
	| RosterMessage
	| PingMessage
	| EventMessage;

export interface JoinMessage {
	t: 'join';
	name: string;
	client: 'unity' | 'web';
}

export interface InputMessage {
	t: 'input';
	mx: number;
	my: number;
	fire: boolean;
}

export interface PongMessage {
	t: 'pong';
	id: number;
}

export type ClientMessage = JoinMessage | InputMessage | PongMessage;

export interface ServerInfo {
	name: string;
	players: number;
	maxPlayers: number;
	version: number;
	tickRate: number;
	wsPath: string;
	uptimeMs: number;
}

/** Returns null instead of throwing: a malformed frame must never kill the render loop. */
export function parseServerMessage(raw: string): ServerMessage | null {
	let value: unknown;
	try {
		value = JSON.parse(raw);
	} catch {
		return null;
	}
	if ((typeof value !== 'object') || (value === null)) {
		return null;
	}
	const t = (value as { t?: unknown }).t;
	switch (t) {
		case 'welcome':
		case 'reject':
		case 'state':
		case 'roster':
		case 'ping':
		case 'event':
			return value as ServerMessage;
		default:
			return null;
	}
}
