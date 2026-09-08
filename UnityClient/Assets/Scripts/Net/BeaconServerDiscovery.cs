#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MultiplayerDemo.Client.Net {
	/// <summary>
	/// Listens for the server's UDP broadcast on port 47777 and keeps a live list of what answered.
	/// The socket lives on a background thread; entries are merged on the main thread by Poll.
	/// </summary>
	public sealed class BeaconServerDiscovery : IServerDiscovery {
		const float EntryLifetime = 5f;

		readonly ConcurrentQueue<DiscoveredServer> _incoming = new();
		readonly List<DiscoveredServer> _servers = new();

		UdpClient _socket;
		Thread _thread;
		volatile bool _running;
		string _description = "Listening for LAN servers on UDP 47777";

		public IReadOnlyList<DiscoveredServer> Servers => _servers;

		public string Description => _description;

		public void Begin() {
			if ( _running ) {
				return;
			}
			try {
				_socket = new UdpClient();
				_socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
				_socket.Client.Bind(new IPEndPoint(IPAddress.Any, ArenaProtocol.BeaconPort));
			} catch ( Exception e ) {
				_description = "UDP discovery unavailable: " + e.Message;
				_socket?.Dispose();
				_socket = null;
				return;
			}
			_running = true;
			_thread = new Thread(ReceiveLoop) { IsBackground = true, Name = "ArenaBeacon" };
			_thread.Start();
		}

		public void Refresh() {
			// The beacon repeats every second, so a refresh only has to forget the stale entries.
			_servers.Clear();
		}

		public void Poll(float now) {
			while ( _incoming.TryDequeue(out var found) ) {
				found.LastSeen = now;
				var index = _servers.FindIndex(entry =>
					(entry.Port == found.Port) && string.Equals(entry.Host, found.Host, StringComparison.Ordinal));
				if ( index >= 0 ) {
					_servers[index] = found;
				} else {
					_servers.Add(found);
				}
			}
			for ( var i = _servers.Count - 1; i >= 0; i-- ) {
				if ( (now - _servers[i].LastSeen) > EntryLifetime ) {
					_servers.RemoveAt(i);
				}
			}
		}

		public void Stop() {
			_running = false;
			try {
				_socket?.Close();
			} catch ( Exception ) {
				// Closing a socket that already died is fine.
			}
			_socket = null;
			_thread = null;
		}

		void ReceiveLoop() {
			var remote = new IPEndPoint(IPAddress.Any, 0);
			while ( _running ) {
				byte[] payload;
				try {
					payload = _socket.Receive(ref remote);
				} catch ( Exception ) {
					return;
				}
				var beacon = ArenaProtocol.ParseBeacon(Encoding.UTF8.GetString(payload));
				if ( beacon == null ) {
					continue;
				}
				_incoming.Enqueue(new DiscoveredServer {
					Name = beacon.name,
					// The beacon carries no host: the sender's address is the only truthful one.
					Host = remote.Address.ToString(),
					Port = beacon.port,
					Players = beacon.players
				});
			}
		}
	}
}
#endif
