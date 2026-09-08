#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;

namespace MultiplayerDemo.Client.Net {
	/// <summary>
	/// WebGL half of <see cref="IArenaConnection"/>. All the work happens in
	/// Assets/Plugins/WebGL/ArenaWebSocket.jslib; this type only owns the handle and marshals strings.
	/// </summary>
	public sealed class WebGlArenaConnection : IArenaConnection {
		[DllImport("__Internal")] static extern int ArenaWsCreate(string url);
		[DllImport("__Internal")] static extern void ArenaWsSend(int handle, string text);
		[DllImport("__Internal")] static extern int ArenaWsState(int handle);
		[DllImport("__Internal")] static extern IntPtr ArenaWsReceive(int handle);
		[DllImport("__Internal")] static extern IntPtr ArenaWsError(int handle);
		[DllImport("__Internal")] static extern void ArenaWsFree(IntPtr pointer);
		[DllImport("__Internal")] static extern void ArenaWsClose(int handle);

		int _handle;
		string _error = string.Empty;

		public ArenaConnectionState State {
			get {
				if ( _handle == 0 ) {
					return ArenaConnectionState.Idle;
				}
				var state = (ArenaConnectionState)ArenaWsState(_handle);
				if ( state == ArenaConnectionState.Failed ) {
					_error = ReadString(ArenaWsError(_handle)) ?? _error;
				}
				return state;
			}
		}

		public string Error => _error;

		public void Connect(string url) {
			if ( _handle != 0 ) {
				return;
			}
			_handle = ArenaWsCreate(url);
		}

		public void Send(string text) {
			if ( (_handle != 0) && !string.IsNullOrEmpty(text) ) {
				ArenaWsSend(_handle, text);
			}
		}

		public bool TryDequeue(out string message) {
			message = null;
			if ( _handle == 0 ) {
				return false;
			}
			message = ReadString(ArenaWsReceive(_handle));
			return message != null;
		}

		public void Close() {
			if ( _handle == 0 ) {
				return;
			}
			ArenaWsClose(_handle);
			_handle = 0;
		}

		public void Dispose() => Close();

		static string ReadString(IntPtr pointer) {
			if ( pointer == IntPtr.Zero ) {
				return null;
			}
			var value = Marshal.PtrToStringUTF8(pointer);
			ArenaWsFree(pointer);
			return value;
		}
	}
}
#endif
