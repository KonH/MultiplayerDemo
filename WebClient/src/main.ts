import { ArenaConnection } from './connection.js';
import { DEFAULT_ADDRESS, defaultAddress, discoverServers, normaliseAddress, socketUrl } from './discovery.js';
import { formatElapsed, formatSeconds } from './format.js';
import { isMoveKey, moveVector } from './input.js';
import type { ArenaBounds, RosterPlayer, WelcomeMessage } from './protocol.js';
import { ArenaRenderer } from './renderer.js';
import { SnapshotBuffer } from './snapshots.js';

/** Fixed send rate, independent of the render loop: 30/s matches the server tick. */
const INPUT_SEND_INTERVAL_MS = 1000 / 30;

/** A fire press stays latched for this many sends so a fast click cannot fall between them. */
const FIRE_LATCH_SENDS = 2;

const DEFAULT_ARENA: ArenaBounds = { minX: -20, minY: -20, maxX: 20, maxY: 20 };

function element<T extends HTMLElement>(id: string): T {
	const found = document.getElementById(id);
	if (!found) {
		throw new Error(`Missing element #${id}`);
	}
	return found as T;
}

const canvas = element<HTMLCanvasElement>('view');
const hud = element<HTMLDivElement>('hud');
const connectScreen = element<HTMLDivElement>('connect');
const connectForm = element<HTMLFormElement>('connect-form');
const connectButton = element<HTMLButtonElement>('connect-button');
const connectError = element<HTMLParagraphElement>('connect-error');
const nameInput = element<HTMLInputElement>('name');
const addressInput = element<HTMLInputElement>('address');
const refreshButton = element<HTMLButtonElement>('refresh');
const serverList = element<HTMLUListElement>('server-list');
const hpText = element<HTMLSpanElement>('hp-text');
const hpBar = element<HTMLDivElement>('hp-bar');
const cooldownText = element<HTMLSpanElement>('cooldown-text');
const cooldownBar = element<HTMLDivElement>('cooldown-bar');
const connectionState = element<HTMLSpanElement>('connection-state');
const timerLabel = element<HTMLDivElement>('timer');
const respawnLabel = element<HTMLDivElement>('respawn');
const leaderboard = element<HTMLDivElement>('leaderboard');
const leaderboardBody = element<HTMLTableSectionElement>('leaderboard-body');

const renderer = new ArenaRenderer(canvas);
const buffer = new SnapshotBuffer();
const pressedKeys = new Set<string>();

interface Session {
	connection: ArenaConnection;
	welcome: WelcomeMessage | null;
	roster: RosterPlayer[];
	rosterAt: number;
	localHp: number;
	localCd: number;
	localSampleAt: number;
	elapsedMs: number;
	elapsedAt: number;
	alive: boolean;
}

let session: Session | null = null;
let mouseFiring = false;
let fireLatch = 0;
let leaderboardVisible = false;
let inputTimer: number | null = null;

addressInput.value = defaultAddress(window.location) || DEFAULT_ADDRESS;
nameInput.value = `player-${Math.floor(Math.random() * 900 + 100)}`;

function showConnectScreen(message: string | null): void {
	connectScreen.hidden = false;
	hud.hidden = true;
	connectButton.disabled = false;
	connectButton.textContent = 'Connect';
	if (message) {
		connectError.textContent = message;
		connectError.hidden = false;
	} else {
		connectError.hidden = true;
	}
}

function teardown(reason: string | null): void {
	if (inputTimer !== null) {
		window.clearInterval(inputTimer);
		inputTimer = null;
	}
	session?.connection.close();
	session = null;
	buffer.clear();
	pressedKeys.clear();
	mouseFiring = false;
	fireLatch = 0;
	leaderboardVisible = false;
	leaderboard.hidden = true;
	respawnLabel.hidden = true;
	showConnectScreen(reason);
}

function connect(name: string, address: string): void {
	const url = socketUrl(address, window.location.protocol === 'https:');
	connectButton.disabled = true;
	connectButton.textContent = 'Connecting…';
	connectError.hidden = true;

	const connection = new ArenaConnection(url, name, {
		onWelcome: (welcome) => {
			if (!session) {
				return;
			}
			session.welcome = welcome;
			session.localHp = welcome.maxHealth;
			connectScreen.hidden = true;
			hud.hidden = false;
			connectionState.textContent = `connected · ${normaliseAddress(address)}`;
		},
		onReject: (reject) => {
			teardown(reject.reason);
		},
		onState: (state) => {
			if (!session) {
				return;
			}
			const now = performance.now();
			buffer.push(state, now);
			session.elapsedMs = state.elapsedMs;
			session.elapsedAt = now;
			const localId = session.welcome?.id ?? -1;
			const self = state.players.find((player) => player.id === localId);
			session.alive = self !== undefined;
			if (self) {
				session.localHp = self.hp;
				session.localCd = self.cd;
				session.localSampleAt = now;
			} else {
				// The server drops dead players from the snapshot, so hp 0 never arrives on the wire.
				session.localHp = 0;
			}
		},
		onRoster: (roster) => {
			if (!session) {
				return;
			}
			session.roster = roster.players;
			session.rosterAt = performance.now();
		},
		onClose: (reason) => {
			teardown(`${reason}. Reconnect below.`);
		}
	});

	session = {
		connection,
		welcome: null,
		roster: [],
		rosterAt: performance.now(),
		localHp: 0,
		localCd: 0,
		localSampleAt: performance.now(),
		elapsedMs: 0,
		elapsedAt: performance.now(),
		alive: false
	};

	inputTimer = window.setInterval(sendInput, INPUT_SEND_INTERVAL_MS);
}

function sendInput(): void {
	if (!session || (session.connection.status !== 'playing')) {
		return;
	}
	const move = moveVector(pressedKeys);
	const firing = mouseFiring || pressedKeys.has('Space') || (fireLatch > 0);
	if (fireLatch > 0) {
		fireLatch--;
	}
	session.connection.sendInput(move.mx, move.my, firing);
}

function pressFire(): void {
	fireLatch = FIRE_LATCH_SENDS;
	// Send at once so a click that lands between two ticks is never swallowed.
	if (session && (session.connection.status === 'playing')) {
		const move = moveVector(pressedKeys);
		session.connection.sendInput(move.mx, move.my, true);
	}
}

function renderLeaderboard(roster: RosterPlayer[], localId: number): void {
	const rows = [...roster].sort((a, b) => (b.kills - a.kills) || a.name.localeCompare(b.name));
	leaderboardBody.replaceChildren(
		...rows.map((player) => {
			const tr = document.createElement('tr');
			if (player.id === localId) {
				tr.className = 'self';
			}
			const cells: Array<[string, boolean]> = [
				[player.name, false],
				[String(player.kills), false],
				[String(player.deaths), false],
				[player.connected ? 'connected' : 'disconnected', !player.connected],
				[player.ip, false],
				[player.client === 'unity' ? 'Unity' : player.client === 'web' ? 'Web' : player.client, false],
				[player.connected ? `${player.ping} ms` : '—', false]
			];
			for (const [text, offline] of cells) {
				const td = document.createElement('td');
				td.textContent = text;
				if (offline) {
					td.className = 'offline';
				}
				tr.append(td);
			}
			return tr;
		})
	);
}

function frameLoop(): void {
	requestAnimationFrame(frameLoop);
	if (!session?.welcome) {
		return;
	}
	const welcome = session.welcome;
	const now = performance.now();
	const frame = buffer.sample(now);

	const names = new Map(session.roster.map((player) => [player.id, player.name]));
	renderer.render(frame, {
		arena: welcome.arena ?? DEFAULT_ARENA,
		playerRadius: welcome.playerRadius,
		bulletRadius: welcome.bulletRadius,
		localId: welcome.id,
		names
	});

	const hp = session.localHp;
	hpText.textContent = `${hp} / ${welcome.maxHealth}`;
	hpBar.style.width = `${(hp / Math.max(1, welcome.maxHealth)) * 100}%`;

	// The snapshot only arrives 30x/s; counting down locally keeps the bar smooth.
	const cooldown = Math.max(0, session.localCd - (now - session.localSampleAt));
	cooldownText.textContent = cooldown > 0 ? `${formatSeconds(cooldown)} s` : 'ready';
	cooldownBar.style.width = `${(cooldown / Math.max(1, welcome.fireCooldownMs)) * 100}%`;

	timerLabel.textContent = formatElapsed(session.elapsedMs + (now - session.elapsedAt));

	const self = session.roster.find((player) => player.id === welcome.id);
	const respawnIn = self ? Math.max(0, self.respawnIn - (now - session.rosterAt)) : 0;
	if (!session.alive && (respawnIn > 0)) {
		respawnLabel.innerHTML = `<small>RESPAWNING IN</small>${formatSeconds(respawnIn)}`;
		respawnLabel.hidden = false;
	} else {
		respawnLabel.hidden = true;
	}

	if (leaderboardVisible) {
		renderLeaderboard(session.roster, welcome.id);
	}
}

async function refreshServers(): Promise<void> {
	refreshButton.disabled = true;
	serverList.replaceChildren(listItem('Scanning…'));
	const servers = await discoverServers(window.location);
	refreshButton.disabled = false;
	if (servers.length === 0) {
		serverList.replaceChildren(listItem('No servers found. Type an address above.'));
		return;
	}
	// In dev the page host is the Vite server, not the game server; prefer a real one.
	if (!servers.some((server) => server.address === normaliseAddress(addressInput.value))) {
		addressInput.value = servers[0]!.address;
	}
	serverList.replaceChildren(
		...servers.map((server) => {
			const li = document.createElement('li');
			const button = document.createElement('button');
			button.type = 'button';
			const label = document.createElement('span');
			label.textContent = `${server.info.name} — ${server.address}`;
			const meta = document.createElement('span');
			meta.className = 'server-meta';
			meta.textContent = `${server.info.players}/${server.info.maxPlayers}`;
			button.append(label, meta);
			button.addEventListener('click', () => {
				addressInput.value = server.address;
			});
			li.append(button);
			return li;
		})
	);
}

function listItem(text: string): HTMLLIElement {
	const li = document.createElement('li');
	li.className = 'muted';
	li.textContent = text;
	return li;
}

connectForm.addEventListener('submit', (submitEvent) => {
	submitEvent.preventDefault();
	const name = nameInput.value.trim();
	if ((name.length < 1) || (name.length > 20)) {
		showConnectScreen('Name must be 1-20 characters.');
		return;
	}
	connect(name, addressInput.value);
});

refreshButton.addEventListener('click', () => {
	void refreshServers();
});

window.addEventListener('keydown', (keyEvent) => {
	if (keyEvent.code === 'Tab') {
		// The browser would otherwise move focus out of the canvas.
		keyEvent.preventDefault();
		if (session?.welcome) {
			leaderboardVisible = !leaderboardVisible;
			leaderboard.hidden = !leaderboardVisible;
		}
		return;
	}
	if (!session?.welcome || keyEvent.repeat) {
		return;
	}
	if (isMoveKey(keyEvent.code)) {
		keyEvent.preventDefault();
		pressedKeys.add(keyEvent.code);
		return;
	}
	if (keyEvent.code === 'Space') {
		keyEvent.preventDefault();
		pressedKeys.add('Space');
		pressFire();
	}
});

window.addEventListener('keyup', (keyEvent) => {
	pressedKeys.delete(keyEvent.code);
});

window.addEventListener('blur', () => {
	pressedKeys.clear();
	mouseFiring = false;
});

document.addEventListener('visibilitychange', () => {
	if (document.visibilityState !== 'hidden') {
		return;
	}
	// Browsers throttle timers in background tabs, so the last input would otherwise stick
	// and the character would keep walking. Send a stop before we lose the send rate.
	pressedKeys.clear();
	mouseFiring = false;
	fireLatch = 0;
	session?.connection.sendInput(0, 0, false);
});

canvas.addEventListener('mousedown', (mouseEvent) => {
	if ((mouseEvent.button !== 0) || !session?.welcome) {
		return;
	}
	mouseEvent.preventDefault();
	mouseFiring = true;
	pressFire();
});

window.addEventListener('mouseup', (mouseEvent) => {
	if (mouseEvent.button === 0) {
		mouseFiring = false;
	}
});

canvas.addEventListener('contextmenu', (contextEvent) => contextEvent.preventDefault());

window.addEventListener('resize', () => renderer.resize());

showConnectScreen(null);
void refreshServers();
requestAnimationFrame(frameLoop);
