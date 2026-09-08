import {
	parseServerMessage,
	type EventMessage,
	type RejectMessage,
	type RosterMessage,
	type StateMessage,
	type WelcomeMessage
} from './protocol.js';

export interface ConnectionHandlers {
	onOpen?: () => void;
	onWelcome?: (message: WelcomeMessage) => void;
	onReject?: (message: RejectMessage) => void;
	onState?: (message: StateMessage) => void;
	onRoster?: (message: RosterMessage) => void;
	onEvent?: (message: EventMessage) => void;
	onClose?: (reason: string) => void;
}

export type ConnectionStatus = 'connecting' | 'joining' | 'playing' | 'closed';

/**
 * One WebSocket session. Deliberately free of DOM APIs so the headless bot in `tools/` runs
 * the exact code path the browser does.
 */
export class ArenaConnection {
	readonly #socket: WebSocket;
	readonly #handlers: ConnectionHandlers;
	readonly #name: string;
	#status: ConnectionStatus = 'connecting';
	#closeReported = false;

	constructor(url: string, name: string, handlers: ConnectionHandlers) {
		this.#name = name;
		this.#handlers = handlers;
		this.#socket = new WebSocket(url);
		this.#socket.addEventListener('open', () => {
			this.#status = 'joining';
			this.send({ t: 'join', name: this.#name, client: 'web' });
			this.#handlers.onOpen?.();
		});
		this.#socket.addEventListener('message', (event: MessageEvent) => {
			if (typeof event.data !== 'string') {
				return;
			}
			this.#handle(event.data);
		});
		this.#socket.addEventListener('close', () => this.#reportClose('Connection closed'));
		this.#socket.addEventListener('error', () => this.#reportClose('Connection error'));
	}

	get status(): ConnectionStatus {
		return this.#status;
	}

	get isOpen(): boolean {
		return this.#socket.readyState === WebSocket.OPEN;
	}

	send(message: unknown): void {
		if (this.isOpen) {
			this.#socket.send(JSON.stringify(message));
		}
	}

	sendInput(mx: number, my: number, fire: boolean): void {
		if (this.#status === 'playing') {
			this.send({ t: 'input', mx, my, fire });
		}
	}

	close(): void {
		this.#status = 'closed';
		this.#closeReported = true;
		try {
			this.#socket.close();
		} catch {
			// Already closing.
		}
	}

	#handle(raw: string): void {
		const message = parseServerMessage(raw);
		if (!message) {
			return;
		}
		switch (message.t) {
			case 'welcome':
				this.#status = 'playing';
				this.#handlers.onWelcome?.(message);
				break;
			case 'reject':
				this.#handlers.onReject?.(message);
				break;
			case 'state':
				this.#handlers.onState?.(message);
				break;
			case 'roster':
				this.#handlers.onRoster?.(message);
				break;
			case 'ping':
				// Answered immediately: the server measures round trip from this echo.
				this.send({ t: 'pong', id: message.id });
				break;
			case 'event':
				this.#handlers.onEvent?.(message);
				break;
		}
	}

	#reportClose(reason: string): void {
		if (this.#closeReported) {
			return;
		}
		this.#closeReported = true;
		this.#status = 'closed';
		this.#handlers.onClose?.(reason);
	}
}
