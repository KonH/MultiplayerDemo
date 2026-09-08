using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MultiplayerDemo.Server;

/// <summary>
/// UDP broadcast so native clients can list servers on the LAN without typing an address.
/// Browsers cannot receive UDP, so they discover servers by probing /api/info instead.
/// </summary>
public sealed class DiscoveryBeacon : BackgroundService {
	public const int BeaconPort = 47777;

	readonly ArenaHost _arena;
	readonly ILogger<DiscoveryBeacon> _logger;
	readonly int _gamePort;

	public DiscoveryBeacon(ArenaHost arena, ILogger<DiscoveryBeacon> logger, IConfiguration configuration) {
		_arena = arena;
		_logger = logger;
		_gamePort = configuration.GetValue("port", 8080);
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
		using var client = new UdpClient();
		try {
			client.EnableBroadcast = true;
		} catch ( SocketException e ) {
			_logger.LogWarning(e, "Broadcast unavailable; LAN discovery is disabled");
			return;
		}
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
		while ( await timer.WaitForNextTickAsync(stoppingToken) ) {
			var payload = JsonSerializer.Serialize(new BeaconMessage {
				Name = _arena.ServerName,
				Port = _gamePort,
				Players = _arena.ConnectedCount
			});
			var bytes = Encoding.UTF8.GetBytes(payload);
			foreach ( var target in BroadcastTargets() ) {
				try {
					await client.SendAsync(bytes, bytes.Length, target);
				} catch ( SocketException ) {
					// A missing route or a sleeping adapter is not worth killing the server over.
				}
			}
		}
	}

	/// <summary>
	/// A single send to 255.255.255.255 leaves on whichever interface the routing table picks,
	/// which on a machine with virtual adapters is rarely the LAN one. Send a directed broadcast
	/// per interface instead, plus loopback so clients on this machine always see the server.
	/// </summary>
	static IEnumerable<IPEndPoint> BroadcastTargets() {
		yield return new IPEndPoint(IPAddress.Loopback, BeaconPort);
		yield return new IPEndPoint(IPAddress.Broadcast, BeaconPort);
		foreach ( var adapter in NetworkInterface.GetAllNetworkInterfaces() ) {
			if ( (adapter.OperationalStatus != OperationalStatus.Up) ||
				(adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) ) {
				continue;
			}
			foreach ( var unicast in adapter.GetIPProperties().UnicastAddresses ) {
				if ( unicast.Address.AddressFamily != AddressFamily.InterNetwork ) {
					continue;
				}
				var broadcast = DirectedBroadcast(unicast.Address, unicast.IPv4Mask);
				if ( broadcast != null ) {
					yield return new IPEndPoint(broadcast, BeaconPort);
				}
			}
		}
	}

	static IPAddress? DirectedBroadcast(IPAddress address, IPAddress? mask) {
		if ( mask == null ) {
			return null;
		}
		var addressBytes = address.GetAddressBytes();
		var maskBytes = mask.GetAddressBytes();
		if ( addressBytes.Length != maskBytes.Length ) {
			return null;
		}
		var result = new byte[addressBytes.Length];
		for ( var i = 0; i < result.Length; i++ ) {
			result[i] = (byte)(addressBytes[i] | ~maskBytes[i]);
		}
		return new IPAddress(result);
	}
}
