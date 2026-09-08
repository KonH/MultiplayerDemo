import type { ServerInfo } from './protocol.js';

/** Browsers cannot receive the UDP beacon, so discovery is a probe of these ports instead. */
export const CANDIDATE_PORTS = [8080, 8081, 8082, 8083] as const;

export const DEFAULT_ADDRESS = 'localhost:8080';

export const PROBE_TIMEOUT_MS = 500;

export interface DiscoveredServer {
	address: string;
	info: ServerInfo;
}

/** Strips any scheme/path a user pasted and defaults the port to 8080. */
export function normaliseAddress(raw: string): string {
	let text = raw.trim();
	if (text.length === 0) {
		return DEFAULT_ADDRESS;
	}
	text = text.replace(/^[a-z]+:\/\//i, '');
	const slash = text.indexOf('/');
	if (slash >= 0) {
		text = text.slice(0, slash);
	}
	if (text.length === 0) {
		return DEFAULT_ADDRESS;
	}
	return text.includes(':') ? text : `${text}:8080`;
}

export function infoUrl(address: string, secure = false): string {
	return `${secure ? 'https' : 'http'}://${normaliseAddress(address)}/api/info`;
}

export function socketUrl(address: string, secure = false): string {
	return `${secure ? 'wss' : 'ws'}://${normaliseAddress(address)}/ws`;
}

export interface PageLocation {
	hostname: string;
	/** May be empty for the default port of the scheme. */
	port: string;
	protocol: string;
}

/** Address prefilled into the connect screen: the page's own host when it serves the client. */
export function defaultAddress(page: PageLocation | null): string {
	if (!page || (page.hostname.length === 0) || (page.protocol === 'file:')) {
		return DEFAULT_ADDRESS;
	}
	if (page.port.length > 0) {
		return `${page.hostname}:${page.port}`;
	}
	return `${page.hostname}:${page.protocol === 'https:' ? 443 : 80}`;
}

/**
 * `localhost` plus the page's own host across every candidate port, in that order, with the
 * page's exact origin appended so a client served straight from the server finds it even on
 * a port outside the candidate list. Duplicates are removed, first occurrence wins.
 */
export function discoveryCandidates(
	page: PageLocation | null,
	ports: readonly number[] = CANDIDATE_PORTS
): string[] {
	const hosts = ['localhost'];
	if (page && (page.hostname.length > 0) && !hosts.includes(page.hostname) && (page.protocol !== 'file:')) {
		hosts.push(page.hostname);
	}
	const result: string[] = [];
	for (const host of hosts) {
		for (const port of ports) {
			result.push(`${host}:${port}`);
		}
	}
	const own = defaultAddress(page);
	if (page && (page.protocol !== 'file:')) {
		result.push(own);
	}
	return [...new Set(result)];
}

export async function probeServer(
	address: string,
	timeoutMs = PROBE_TIMEOUT_MS,
	secure = false
): Promise<DiscoveredServer | null> {
	const controller = new AbortController();
	const timer = setTimeout(() => controller.abort(), timeoutMs);
	try {
		const response = await fetch(infoUrl(address, secure), {
			signal: controller.signal,
			cache: 'no-store'
		});
		if (!response.ok) {
			return null;
		}
		const info = (await response.json()) as ServerInfo;
		if (typeof info?.wsPath !== 'string') {
			return null;
		}
		return { address: normaliseAddress(address), info };
	} catch {
		return null;
	} finally {
		clearTimeout(timer);
	}
}

export async function discoverServers(
	page: PageLocation | null,
	timeoutMs = PROBE_TIMEOUT_MS
): Promise<DiscoveredServer[]> {
	const secure = page?.protocol === 'https:';
	const results = await Promise.all(
		discoveryCandidates(page).map((address) => probeServer(address, timeoutMs, secure))
	);
	return results.filter((entry): entry is DiscoveredServer => entry !== null);
}
