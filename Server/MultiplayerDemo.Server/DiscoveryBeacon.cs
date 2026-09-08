using System.Net;
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
		var endpoint = new IPEndPoint(IPAddress.Broadcast, BeaconPort);
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
		while ( await timer.WaitForNextTickAsync(stoppingToken) ) {
			var payload = JsonSerializer.Serialize(new BeaconMessage {
				Name = _arena.ServerName,
				Port = _gamePort,
				Players = _arena.ConnectedCount
			});
			var bytes = Encoding.UTF8.GetBytes(payload);
			try {
				await client.SendAsync(bytes, bytes.Length, endpoint);
			} catch ( SocketException ) {
				// A missing route or a sleeping adapter is not worth killing the server over.
			}
		}
	}
}
