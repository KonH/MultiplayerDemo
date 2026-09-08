import { describe, expect, it } from 'vitest';
import {
	CANDIDATE_PORTS,
	DEFAULT_ADDRESS,
	defaultAddress,
	discoveryCandidates,
	infoUrl,
	normaliseAddress,
	socketUrl
} from '../src/discovery.js';

const vitePage = { hostname: 'localhost', port: '5173', protocol: 'http:' };
const lanPage = { hostname: '192.168.1.20', port: '8080', protocol: 'http:' };

describe('normaliseAddress', () => {
	it('defaults the port to 8080', () => {
		expect(normaliseAddress('example.com')).toBe('example.com:8080');
	});

	it('keeps an explicit port', () => {
		expect(normaliseAddress('10.0.0.5:8081')).toBe('10.0.0.5:8081');
	});

	it('strips a pasted scheme and path', () => {
		expect(normaliseAddress('http://localhost:8080/ws')).toBe('localhost:8080');
		expect(normaliseAddress('ws://localhost:8082/')).toBe('localhost:8082');
	});

	it('falls back to the default for blank input', () => {
		expect(normaliseAddress('   ')).toBe(DEFAULT_ADDRESS);
		expect(normaliseAddress('http://')).toBe(DEFAULT_ADDRESS);
	});
});

describe('url builders', () => {
	it('builds the info and socket urls from the protocol', () => {
		expect(infoUrl('localhost:8080')).toBe('http://localhost:8080/api/info');
		expect(socketUrl('localhost:8080')).toBe('ws://localhost:8080/ws');
	});

	it('upgrades to tls when the page is https', () => {
		expect(infoUrl('arena.example:443', true)).toBe('https://arena.example:443/api/info');
		expect(socketUrl('arena.example:443', true)).toBe('wss://arena.example:443/ws');
	});
});

describe('defaultAddress', () => {
	it('uses the page host and port', () => {
		expect(defaultAddress(vitePage)).toBe('localhost:5173');
		expect(defaultAddress(lanPage)).toBe('192.168.1.20:8080');
	});

	it('fills in the implicit scheme port', () => {
		expect(defaultAddress({ hostname: 'arena.example', port: '', protocol: 'http:' })).toBe('arena.example:80');
		expect(defaultAddress({ hostname: 'arena.example', port: '', protocol: 'https:' })).toBe('arena.example:443');
	});

	it('falls back to localhost:8080 for file:// and no page', () => {
		expect(defaultAddress(null)).toBe(DEFAULT_ADDRESS);
		expect(defaultAddress({ hostname: '', port: '', protocol: 'file:' })).toBe(DEFAULT_ADDRESS);
	});
});

describe('discoveryCandidates', () => {
	it('probes 8080..8083 on localhost when there is no page host', () => {
		expect(discoveryCandidates(null)).toEqual([
			'localhost:8080',
			'localhost:8081',
			'localhost:8082',
			'localhost:8083'
		]);
		expect(CANDIDATE_PORTS).toEqual([8080, 8081, 8082, 8083]);
	});

	it('adds the page host and the page origin itself', () => {
		expect(discoveryCandidates(lanPage)).toEqual([
			'localhost:8080',
			'localhost:8081',
			'localhost:8082',
			'localhost:8083',
			'192.168.1.20:8080',
			'192.168.1.20:8081',
			'192.168.1.20:8082',
			'192.168.1.20:8083'
		]);
	});

	it('includes the dev-server origin so a same-origin build is still found', () => {
		expect(discoveryCandidates(vitePage)).toContain('localhost:5173');
	});

	it('never repeats an address', () => {
		const candidates = discoveryCandidates({ hostname: 'localhost', port: '8080', protocol: 'http:' });
		expect(new Set(candidates).size).toBe(candidates.length);
	});

	it('honours a custom port list', () => {
		expect(discoveryCandidates(null, [9000])).toEqual(['localhost:9000']);
	});
});
