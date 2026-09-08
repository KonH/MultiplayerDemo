using System.Collections.Generic;

namespace MultiplayerDemo.Client.Net {
	public struct DiscoveredServer {
		public string Name;
		public string Host;
		public int Port;
		public int Players;
		public float LastSeen;

		public string Address => $"{Host}:{Port}";
	}

	/// <summary>
	/// Two ways to find a server, one interface. Native builds listen to the UDP beacon; WebGL cannot
	/// receive UDP at all and probes <c>/api/info</c> on a handful of candidate ports instead.
	/// </summary>
	public interface IServerDiscovery {
		IReadOnlyList<DiscoveredServer> Servers { get; }
		string Description { get; }
		void Begin();
		void Refresh();
		void Poll(float now);
		void Stop();
	}
}
