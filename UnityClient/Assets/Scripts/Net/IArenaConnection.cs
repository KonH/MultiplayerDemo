using System;

namespace MultiplayerDemo.Client.Net {
	public enum ArenaConnectionState {
		Idle,
		Connecting,
		Open,
		Closed,
		Failed
	}

	/// <summary>
	/// One WebSocket, two runtimes. The native path uses <c>ClientWebSocket</c>; WebGL goes through
	/// a `.jslib` bridge because browsers do not expose sockets to managed code. Everything the rest
	/// of the client needs fits behind these five members, and every received frame is handed over on
	/// the main thread through <see cref="TryDequeue"/>.
	/// </summary>
	public interface IArenaConnection : IDisposable {
		ArenaConnectionState State { get; }
		string Error { get; }
		void Connect(string url);
		void Send(string text);
		bool TryDequeue(out string message);
		void Close();
	}
}
