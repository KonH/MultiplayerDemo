using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using MultiplayerDemo.Core;

namespace MultiplayerDemo.Server;

/// <summary>
/// One connected socket. Owns its own send pump so a slow client can never stall the game loop;
/// the queue is bounded and drops the oldest snapshot rather than growing without limit.
/// </summary>
public sealed class ClientSession {
	readonly WebSocket _socket;
	readonly Channel<string> _outbox = Channel.CreateBounded<string>(new BoundedChannelOptions(256) {
		FullMode = BoundedChannelFullMode.DropOldest,
		SingleReader = true
	});

	public ClientSession(WebSocket socket, string ip) {
		_socket = socket;
		Ip = ip;
	}

	public string Ip { get; }
	public SimPlayer? Player { get; set; }
	public bool JoinCompleted => Player != null;

	public int LastPingId { get; set; }
	public long LastPingSentMs { get; set; }
	public int PingMs { get; set; }

	public void Send<T>(T message) {
		SendRaw(JsonSerializer.Serialize(message));
	}

	/// <summary>Broadcasts serialise once and hand the same JSON to every session.</summary>
	public void SendRaw(string json) {
		_outbox.Writer.TryWrite(json);
	}

	public async Task SendPumpAsync(CancellationToken token) {
		try {
			await foreach ( var text in _outbox.Reader.ReadAllAsync(token) ) {
				if ( _socket.State != WebSocketState.Open ) {
					break;
				}
				var bytes = Encoding.UTF8.GetBytes(text);
				await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, token);
			}
		} catch ( OperationCanceledException ) {
			// Shutting down.
		} catch ( WebSocketException ) {
			// The peer vanished; the receive loop reports the disconnect.
		}
	}

	public void CompleteOutbox() {
		_outbox.Writer.TryComplete();
	}
}
