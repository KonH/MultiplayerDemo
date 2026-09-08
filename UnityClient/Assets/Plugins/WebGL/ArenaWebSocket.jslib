// Browser-side half of MultiplayerDemo.Client.Net.WebGlArenaConnection.
// Browsers do not expose sockets to managed code, so the WebGL build talks to the arena
// through the page's own WebSocket object and hands frames back one at a time.
mergeInto(LibraryManager.library, {
	$ArenaWs: {
		sockets: {},
		nextHandle: 1
	},

	ArenaWsCreate__deps: ['$ArenaWs', '$UTF8ToString'],
	ArenaWsCreate: function (urlPtr) {
		var url = UTF8ToString(urlPtr);
		var handle = ArenaWs.nextHandle++;
		var entry = { socket: null, queue: [], state: 1, error: '' };
		ArenaWs.sockets[handle] = entry;
		try {
			entry.socket = new WebSocket(url);
		} catch (e) {
			entry.state = 4;
			entry.error = '' + e;
			return handle;
		}
		entry.socket.onopen = function () { entry.state = 2; };
		entry.socket.onmessage = function (event) {
			if (typeof event.data === 'string') { entry.queue.push(event.data); }
		};
		entry.socket.onerror = function () {
			if (entry.state !== 3) { entry.state = 4; entry.error = 'WebSocket error'; }
		};
		entry.socket.onclose = function (event) {
			entry.state = (entry.state === 2 || event.wasClean) ? 3 : 4;
			if (entry.state === 4 && !entry.error) { entry.error = 'WebSocket closed (' + event.code + ')'; }
		};
		return handle;
	},

	ArenaWsSend__deps: ['$ArenaWs', '$UTF8ToString'],
	ArenaWsSend: function (handle, textPtr) {
		var entry = ArenaWs.sockets[handle];
		if (!entry || !entry.socket || entry.socket.readyState !== 1) { return; }
		entry.socket.send(UTF8ToString(textPtr));
	},

	ArenaWsState__deps: ['$ArenaWs'],
	ArenaWsState: function (handle) {
		var entry = ArenaWs.sockets[handle];
		return entry ? entry.state : 0;
	},

	ArenaWsReceive__deps: ['$ArenaWs', '$stringToUTF8', '$lengthBytesUTF8', 'malloc'],
	ArenaWsReceive: function (handle) {
		var entry = ArenaWs.sockets[handle];
		if (!entry || (entry.queue.length === 0)) { return 0; }
		var text = entry.queue.shift();
		var size = lengthBytesUTF8(text) + 1;
		var buffer = _malloc(size);
		stringToUTF8(text, buffer, size);
		return buffer;
	},

	ArenaWsError__deps: ['$ArenaWs', '$stringToUTF8', '$lengthBytesUTF8', 'malloc'],
	ArenaWsError: function (handle) {
		var entry = ArenaWs.sockets[handle];
		if (!entry || !entry.error) { return 0; }
		var size = lengthBytesUTF8(entry.error) + 1;
		var buffer = _malloc(size);
		stringToUTF8(entry.error, buffer, size);
		return buffer;
	},

	ArenaWsFree__deps: ['free'],
	ArenaWsFree: function (pointer) {
		if (pointer) { _free(pointer); }
	},

	ArenaWsClose__deps: ['$ArenaWs'],
	ArenaWsClose: function (handle) {
		var entry = ArenaWs.sockets[handle];
		if (!entry) { return; }
		if (entry.socket && (entry.socket.readyState === 0 || entry.socket.readyState === 1)) {
			entry.state = 3;
			entry.socket.close();
		}
		delete ArenaWs.sockets[handle];
	}
});
