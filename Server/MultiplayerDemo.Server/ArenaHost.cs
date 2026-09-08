using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using MultiplayerDemo.Core;

namespace MultiplayerDemo.Server;

/// <summary>
/// Owns the simulation and every connected session. All state mutation happens on the single
/// tick loop; socket threads only enqueue work, which keeps the sim free of locks.
/// </summary>
public sealed class ArenaHost : BackgroundService {
	readonly ConcurrentQueue<Action> _pending = new();
	readonly List<ClientSession> _sessions = new();
	readonly GameSim _sim;
	readonly ILogger<ArenaHost> _logger;
	readonly Stopwatch _uptime = Stopwatch.StartNew();

	int _nextPingId = 1;
	double _pingAccumulator;
	double _rosterAccumulator;
	bool _rosterDirty = true;

	public ArenaHost(GameConfig config, ILogger<ArenaHost> logger) {
		_sim = new GameSim(config);
		_logger = logger;
	}

	public GameConfig Config => _sim.Config;
	public string ServerName { get; set; } = "Arena";
	public long UptimeMs => _uptime.ElapsedMilliseconds;

	public int ConnectedCount {
		get {
			var count = 0;
			foreach ( var player in _sim.Players ) {
				if ( player.Connected ) {
					count++;
				}
			}
			return count;
		}
	}

	public async Task HandleSocketAsync(WebSocket socket, string ip, CancellationToken token) {
		var session = new ClientSession(socket, ip);
		_pending.Enqueue(() => _sessions.Add(session));
		var pump = session.SendPumpAsync(token);
		try {
			await ReceiveLoopAsync(session, socket, token);
		} finally {
			_pending.Enqueue(() => RemoveSession(session));
			session.CompleteOutbox();
			await pump;
		}
	}

	async Task ReceiveLoopAsync(ClientSession session, WebSocket socket, CancellationToken token) {
		var buffer = new byte[8 * 1024];
		var text = new StringBuilder();
		while ( (socket.State == WebSocketState.Open) && !token.IsCancellationRequested ) {
			WebSocketReceiveResult result;
			try {
				result = await socket.ReceiveAsync(buffer, token);
			} catch ( Exception ) {
				break;
			}
			if ( result.MessageType == WebSocketMessageType.Close ) {
				break;
			}
			text.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
			if ( !result.EndOfMessage ) {
				continue;
			}
			var payload = text.ToString();
			text.Clear();
			ClientMessage? message;
			try {
				message = JsonSerializer.Deserialize<ClientMessage>(payload);
			} catch ( JsonException ) {
				continue;
			}
			if ( message != null ) {
				_pending.Enqueue(() => Apply(session, message));
			}
		}
	}

	void Apply(ClientSession session, ClientMessage message) {
		switch ( message.T ) {
			case "join":
				HandleJoin(session, message);
				break;
			case "input":
				if ( session.Player is { } player ) {
					_sim.SetInput(player, new PlayerInput(new Vec2(message.Mx, message.My), message.Fire));
				}
				break;
			case "pong":
				if ( message.Id == session.LastPingId ) {
					session.PingMs = (int)(UptimeMs - session.LastPingSentMs);
					if ( session.Player != null ) {
						session.Player.PingMs = session.PingMs;
					}
				}
				break;
		}
	}

	void HandleJoin(ClientSession session, ClientMessage message) {
		if ( session.JoinCompleted ) {
			return;
		}
		var name = (message.Name ?? string.Empty).Trim();
		if ( (name.Length == 0) || (name.Length > 20) ) {
			session.Send(new RejectMessage { Reason = "Name must be 1-20 characters" });
			return;
		}
		if ( ConnectedCount >= Config.MaxPlayers ) {
			session.Send(new RejectMessage { Reason = "Server is full" });
			return;
		}
		var client = message.Client switch {
			"unity" => ClientKind.Unity,
			"web" => ClientKind.Web,
			_ => ClientKind.Unknown
		};
		var existing = _sim.FindPlayerByName(name);
		SimPlayer player;
		if ( existing != null ) {
			if ( existing.Connected ) {
				session.Send(new RejectMessage { Reason = $"Name '{name}' is already taken" });
				return;
			}
			// Same name, previously disconnected: resume that record rather than starting over.
			player = existing;
			player.Client = client;
			player.Ip = session.Ip;
			_sim.SetConnected(player, true);
		} else {
			player = _sim.AddPlayer(name, client, session.Ip);
		}
		session.Player = player;
		session.Send(BuildWelcome(player));
		_rosterDirty = true;
		_logger.LogInformation("{Name} joined from {Ip} ({Client})", player.Name, session.Ip, client);
	}

	void RemoveSession(ClientSession session) {
		_sessions.Remove(session);
		if ( session.Player is { } player ) {
			_sim.SetConnected(player, false);
			_rosterDirty = true;
			_logger.LogInformation("{Name} left", player.Name);
		}
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
		var dt = 1f / Config.TickRate;
		var period = TimeSpan.FromSeconds(dt);
		using var timer = new PeriodicTimer(period);
		while ( await timer.WaitForNextTickAsync(stoppingToken) ) {
			try {
				Step(dt);
			} catch ( Exception e ) {
				_logger.LogError(e, "Tick failed");
			}
		}
	}

	void Step(float dt) {
		while ( _pending.TryDequeue(out var action) ) {
			action();
		}

		_sim.Tick(dt);

		var state = BuildState();
		var stateJson = JsonSerializer.Serialize(state);
		foreach ( var session in _sessions ) {
			if ( session.JoinCompleted ) {
				session.SendRaw(stateJson);
			}
		}

		BroadcastEvents();

		_rosterAccumulator += dt;
		if ( _rosterDirty || (_rosterAccumulator >= 1.0) ) {
			_rosterAccumulator = 0;
			_rosterDirty = false;
			var rosterJson = JsonSerializer.Serialize(BuildRoster());
			foreach ( var session in _sessions ) {
				if ( session.JoinCompleted ) {
					session.SendRaw(rosterJson);
				}
			}
		}

		_pingAccumulator += dt;
		if ( _pingAccumulator >= 1.0 ) {
			_pingAccumulator = 0;
			foreach ( var session in _sessions ) {
				if ( !session.JoinCompleted ) {
					continue;
				}
				session.LastPingId = _nextPingId++;
				session.LastPingSentMs = UptimeMs;
				session.Send(new PingMessage { Id = session.LastPingId });
			}
		}
	}

	void BroadcastEvents() {
		if ( _sim.Events.Count == 0 ) {
			return;
		}
		foreach ( var gameEvent in _sim.Events ) {
			if ( gameEvent.Kind == GameEventKind.Kill ) {
				_rosterDirty = true;
			}
			var json = JsonSerializer.Serialize(new EventMessage {
				Kind = gameEvent.Kind switch {
					GameEventKind.Hit => "hit",
					GameEventKind.Kill => "kill",
					_ => "spawn"
				},
				Victim = gameEvent.VictimId,
				Killer = gameEvent.KillerId
			});
			foreach ( var session in _sessions ) {
				if ( session.JoinCompleted ) {
					session.SendRaw(json);
				}
			}
		}
	}

	WelcomeMessage BuildWelcome(SimPlayer player) => new() {
		Id = player.Id,
		TickRate = Config.TickRate,
		Arena = new ArenaBounds {
			MinX = Config.ArenaMinX,
			MinY = Config.ArenaMinY,
			MaxX = Config.ArenaMaxX,
			MaxY = Config.ArenaMaxY
		},
		PlayerRadius = Config.PlayerRadius,
		BulletRadius = Config.BulletRadius,
		MoveSpeed = Config.MoveSpeed,
		MaxHealth = Config.MaxHealth,
		RespawnMs = (int)(Config.RespawnSeconds * 1000f),
		FireCooldownMs = (int)(Config.FireCooldownSeconds * 1000f),
		StartedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)(_sim.ElapsedSeconds * 1000d),
		ServerNowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
	};

	StateMessage BuildState() {
		var state = new StateMessage {
			Tick = _sim.TickIndex,
			ElapsedMs = (long)(_sim.ElapsedSeconds * 1000d)
		};
		foreach ( var player in _sim.Players ) {
			if ( !player.Alive ) {
				continue;
			}
			state.Players.Add(new StatePlayer {
				Id = player.Id,
				X = Round(player.Position.X),
				Y = Round(player.Position.Y),
				Hp = player.Health,
				Dx = Round(player.Facing.X),
				Dy = Round(player.Facing.Y),
				Cd = (int)(player.FireCooldown * 1000f)
			});
		}
		foreach ( var bullet in _sim.Bullets ) {
			state.Bullets.Add(new StateBullet {
				Id = bullet.Id,
				X = Round(bullet.Position.X),
				Y = Round(bullet.Position.Y),
				O = bullet.OwnerId
			});
		}
		return state;
	}

	RosterMessage BuildRoster() {
		var roster = new RosterMessage();
		foreach ( var player in _sim.Players ) {
			roster.Players.Add(new RosterPlayer {
				Id = player.Id,
				Name = player.Name,
				Kills = player.Kills,
				Deaths = player.Deaths,
				Connected = player.Connected,
				Ip = player.Ip,
				Client = player.Client switch {
					ClientKind.Unity => "unity",
					ClientKind.Web => "web",
					_ => "unknown"
				},
				Ping = player.PingMs,
				RespawnIn = player.Alive ? 0 : (int)(player.RespawnTimer * 1000f)
			});
		}
		roster.Players.Sort((a, b) => {
			var byKills = b.Kills.CompareTo(a.Kills);
			return byKills != 0 ? byKills : string.CompareOrdinal(a.Name, b.Name);
		});
		return roster;
	}

	// Two decimals is well under a pixel at any sane zoom and keeps snapshots small.
	static float Round(float value) => MathF.Round(value, 2);

	public ServerInfo BuildInfo() => new() {
		Name = ServerName,
		Players = ConnectedCount,
		MaxPlayers = Config.MaxPlayers,
		TickRate = Config.TickRate,
		UptimeMs = UptimeMs
	};
}
