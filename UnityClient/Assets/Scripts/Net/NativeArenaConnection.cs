#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MultiplayerDemo.Client.Net {
	/// <summary>
	/// <c>ClientWebSocket</c> driven by two background tasks. Received frames land in a lock-free queue
	/// that the ECS network system drains on the main thread, so nothing touches Unity from a worker.
	/// </summary>
	public sealed class NativeArenaConnection : IArenaConnection {
		readonly ConcurrentQueue<string> _received = new();
		readonly ConcurrentQueue<string> _outgoing = new();
		readonly SemaphoreSlim _outgoingSignal = new(0);
		readonly CancellationTokenSource _cancellation = new();

		ClientWebSocket _socket;
		volatile string _error = string.Empty;
		int _state = (int)ArenaConnectionState.Idle;

		public ArenaConnectionState State => (ArenaConnectionState)Volatile.Read(ref _state);

		public string Error => _error;

		public void Connect(string url) {
			if ( State != ArenaConnectionState.Idle ) {
				return;
			}
			SetState(ArenaConnectionState.Connecting);
			_socket = new ClientWebSocket();
			_ = RunAsync(url);
		}

		public void Send(string text) {
			if ( string.IsNullOrEmpty(text) ) {
				return;
			}
			_outgoing.Enqueue(text);
			_outgoingSignal.Release();
		}

		public bool TryDequeue(out string message) => _received.TryDequeue(out message);

		public void Close() {
			if ( (State == ArenaConnectionState.Closed) || (State == ArenaConnectionState.Failed) ) {
				return;
			}
			SetState(ArenaConnectionState.Closed);
			try {
				_cancellation.Cancel();
			} catch ( ObjectDisposedException ) {
				// Already torn down.
			}
		}

		public void Dispose() {
			Close();
			_cancellation.Dispose();
			_outgoingSignal.Dispose();
			_socket?.Dispose();
		}

		async Task RunAsync(string url) {
			try {
				await _socket.ConnectAsync(new Uri(url), _cancellation.Token);
			} catch ( Exception e ) {
				Fail(e.Message);
				return;
			}
			SetState(ArenaConnectionState.Open);
			var send = SendLoopAsync();
			await ReceiveLoopAsync();
			_outgoingSignal.Release();
			await send;
		}

		async Task ReceiveLoopAsync() {
			var buffer = new byte[16 * 1024];
			var text = new StringBuilder();
			try {
				while ( (_socket.State == WebSocketState.Open) && !_cancellation.IsCancellationRequested ) {
					var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellation.Token);
					if ( result.MessageType == WebSocketMessageType.Close ) {
						break;
					}
					text.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
					if ( !result.EndOfMessage ) {
						continue;
					}
					_received.Enqueue(text.ToString());
					text.Clear();
				}
			} catch ( OperationCanceledException ) {
				// Close() was called.
			} catch ( Exception e ) {
				Fail(e.Message);
				return;
			}
			if ( State == ArenaConnectionState.Open ) {
				SetState(ArenaConnectionState.Closed);
			}
		}

		async Task SendLoopAsync() {
			try {
				while ( !_cancellation.IsCancellationRequested ) {
					await _outgoingSignal.WaitAsync(_cancellation.Token);
					while ( _outgoing.TryDequeue(out var payload) ) {
						if ( _socket.State != WebSocketState.Open ) {
							return;
						}
						var bytes = Encoding.UTF8.GetBytes(payload);
						await _socket.SendAsync(
							new ArraySegment<byte>(bytes),
							WebSocketMessageType.Text,
							true,
							_cancellation.Token);
					}
				}
			} catch ( OperationCanceledException ) {
				// Close() was called.
			} catch ( Exception e ) {
				Fail(e.Message);
			}
		}

		void Fail(string message) {
			_error = message;
			SetState(ArenaConnectionState.Failed);
		}

		void SetState(ArenaConnectionState state) => Volatile.Write(ref _state, (int)state);
	}
}
#endif
