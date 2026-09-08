import { describe, expect, it } from 'vitest';
import { parseServerMessage } from '../src/protocol.js';

describe('parseServerMessage', () => {
	it('accepts every documented server message', () => {
		const frames = [
			'{"t":"welcome","id":3,"tickRate":30}',
			'{"t":"reject","reason":"Name already taken"}',
			'{"t":"state","tick":1,"elapsedMs":0,"players":[],"bullets":[]}',
			'{"t":"roster","players":[]}',
			'{"t":"ping","id":42}',
			'{"t":"event","kind":"kill","victim":3,"killer":5}'
		];
		for (const frame of frames) {
			expect(parseServerMessage(frame)).not.toBeNull();
		}
	});

	it('returns null rather than throwing on junk', () => {
		expect(parseServerMessage('not json')).toBeNull();
		expect(parseServerMessage('null')).toBeNull();
		expect(parseServerMessage('[1,2,3]')).toBeNull();
		expect(parseServerMessage('{"t":"unknown"}')).toBeNull();
		expect(parseServerMessage('{}')).toBeNull();
	});
});
