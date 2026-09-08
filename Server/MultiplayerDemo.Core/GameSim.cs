namespace MultiplayerDemo.Core;

/// <summary>
/// The single source of truth for the arena. Pure state + rules: no sockets, no threads,
/// no clock of its own. The host decides when to call <see cref="Tick"/>.
/// </summary>
public sealed class GameSim {
	readonly Dictionary<int, SimPlayer> _players = new();
	readonly List<SimBullet> _bullets = new();
	readonly List<GameEvent> _events = new();
	readonly Random _random;

	int _nextPlayerId = 1;
	int _nextBulletId = 1;

	public GameSim(GameConfig config, Random? random = null) {
		Config = config;
		_random = random ?? new Random();
	}

	public GameConfig Config { get; }
	public int TickIndex { get; private set; }
	public double ElapsedSeconds { get; private set; }

	public IReadOnlyCollection<SimPlayer> Players => _players.Values;
	public IReadOnlyList<SimBullet> Bullets => _bullets;

	/// <summary>Events produced by the last <see cref="Tick"/>, for clients that want feedback.</summary>
	public IReadOnlyList<GameEvent> Events => _events;

	public SimPlayer? FindPlayer(int id) => _players.GetValueOrDefault(id);

	public SimPlayer? FindPlayerByName(string name) {
		foreach ( var player in _players.Values ) {
			if ( string.Equals(player.Name, name, StringComparison.OrdinalIgnoreCase) ) {
				return player;
			}
		}
		return null;
	}

	public SimPlayer AddPlayer(string name, ClientKind client, string ip) {
		var player = new SimPlayer {
			Id = _nextPlayerId++,
			Name = name,
			Client = client,
			Ip = ip
		};
		_players[player.Id] = player;
		Spawn(player);
		return player;
	}

	/// <summary>A player who left keeps their record but stops occupying the arena.</summary>
	public void SetConnected(SimPlayer player, bool connected) {
		player.Connected = connected;
		player.Input = PlayerInput.None;
		if ( connected ) {
			Spawn(player);
		} else {
			player.Alive = false;
			player.RespawnTimer = 0f;
		}
	}

	public void SetInput(SimPlayer player, PlayerInput input) {
		var move = input.Move;
		// Clients may send anything; clamp the magnitude so nobody outruns the arena.
		if ( move.LengthSquared > 1f ) {
			move = move.Normalized;
		}
		player.Input = new PlayerInput(move, input.Fire);
	}

	public void Tick(float dt) {
		_events.Clear();
		TickIndex++;
		ElapsedSeconds += dt;

		MovePlayers(dt);
		SeparatePlayers();
		FireBullets();
		MoveBullets(dt);
		UpdateRespawns(dt);
	}

	void MovePlayers(float dt) {
		foreach ( var player in _players.Values ) {
			if ( player.FireCooldown > 0f ) {
				player.FireCooldown = MathF.Max(0f, player.FireCooldown - dt);
			}
			if ( !player.Alive ) {
				continue;
			}
			var move = player.Input.Move;
			if ( move.LengthSquared > 1e-6f ) {
				player.Facing = move.Normalized;
				player.Position = ClampToArena(player.Position + (move * (Config.MoveSpeed * dt)));
			}
		}
	}

	/// <summary>
	/// Pushes overlapping players apart. A few relaxation passes are enough for a handful of
	/// 0.5-radius boxes and keep the result stable regardless of iteration order.
	/// </summary>
	void SeparatePlayers() {
		var alive = new List<SimPlayer>();
		foreach ( var player in _players.Values ) {
			if ( player.Alive ) {
				alive.Add(player);
			}
		}
		var minDistance = Config.PlayerRadius * 2f;
		for ( var pass = 0; pass < 4; pass++ ) {
			var moved = false;
			for ( var i = 0; i < alive.Count; i++ ) {
				for ( var j = i + 1; j < alive.Count; j++ ) {
					var a = alive[i];
					var b = alive[j];
					var delta = b.Position - a.Position;
					var distance = delta.Length;
					if ( distance >= minDistance ) {
						continue;
					}
					var direction = distance > 1e-5f
						? new Vec2(delta.X / distance, delta.Y / distance)
						: new Vec2(1f, 0f);
					var push = (minDistance - distance) * 0.5f;
					a.Position = ClampToArena(a.Position - (direction * push));
					b.Position = ClampToArena(b.Position + (direction * push));
					moved = true;
				}
			}
			if ( !moved ) {
				break;
			}
		}
	}

	void FireBullets() {
		foreach ( var player in _players.Values ) {
			if ( !player.Alive || !player.Input.Fire || (player.FireCooldown > 0f) ) {
				continue;
			}
			var direction = player.Facing.LengthSquared > 1e-6f ? player.Facing.Normalized : new Vec2(0f, 1f);
			_bullets.Add(new SimBullet {
				Id = _nextBulletId++,
				OwnerId = player.Id,
				// Start at the muzzle so a bullet never spawns inside its owner.
				Position = player.Position + (direction * (Config.PlayerRadius + Config.BulletRadius + 0.01f)),
				Velocity = direction * Config.BulletSpeed,
				Life = Config.BulletLifeSeconds
			});
			player.FireCooldown = Config.FireCooldownSeconds;
		}
	}

	void MoveBullets(float dt) {
		for ( var i = _bullets.Count - 1; i >= 0; i-- ) {
			var bullet = _bullets[i];
			var from = bullet.Position;
			var to = from + (bullet.Velocity * dt);
			var hit = FindFirstHit(bullet, from, to);
			if ( hit != null ) {
				ApplyDamage(hit, bullet.OwnerId);
				_bullets.RemoveAt(i);
				continue;
			}
			bullet.Position = to;
			bullet.Life -= dt;
			if ( (bullet.Life <= 0f) || OutsideArena(bullet.Position) ) {
				_bullets.RemoveAt(i);
			}
		}
	}

	/// <summary>
	/// Swept test: at 20 u/s a bullet covers more than a player diameter per tick, so a
	/// point-in-circle check at the new position would tunnel straight through people.
	/// </summary>
	SimPlayer? FindFirstHit(SimBullet bullet, Vec2 from, Vec2 to) {
		var radius = Config.PlayerRadius + Config.BulletRadius;
		SimPlayer? best = null;
		var bestT = float.MaxValue;
		foreach ( var player in _players.Values ) {
			if ( !player.Alive || (player.Id == bullet.OwnerId) ) {
				continue;
			}
			if ( SegmentHitsCircle(from, to, player.Position, radius, out var t) && (t < bestT) ) {
				bestT = t;
				best = player;
			}
		}
		return best;
	}

	static bool SegmentHitsCircle(Vec2 from, Vec2 to, Vec2 center, float radius, out float t) {
		t = 0f;
		var d = to - from;
		var f = from - center;
		var a = d.LengthSquared;
		if ( a < 1e-9f ) {
			return f.LengthSquared <= (radius * radius);
		}
		var b = 2f * ((f.X * d.X) + (f.Y * d.Y));
		var c = f.LengthSquared - (radius * radius);
		if ( c <= 0f ) {
			return true;
		}
		var discriminant = (b * b) - (4f * a * c);
		if ( discriminant < 0f ) {
			return false;
		}
		var root = MathF.Sqrt(discriminant);
		var t0 = (-b - root) / (2f * a);
		if ( (t0 >= 0f) && (t0 <= 1f) ) {
			t = t0;
			return true;
		}
		var t1 = (-b + root) / (2f * a);
		if ( (t1 >= 0f) && (t1 <= 1f) ) {
			t = t1;
			return true;
		}
		return false;
	}

	void ApplyDamage(SimPlayer victim, int killerId) {
		victim.Health -= Config.BulletDamage;
		if ( victim.Health > 0 ) {
			_events.Add(new GameEvent(GameEventKind.Hit, victim.Id, killerId));
			return;
		}
		victim.Health = 0;
		victim.Alive = false;
		victim.Deaths++;
		victim.RespawnTimer = Config.RespawnSeconds;
		victim.Input = PlayerInput.None;
		if ( (killerId != victim.Id) && _players.TryGetValue(killerId, out var killer) ) {
			killer.Kills++;
		}
		_events.Add(new GameEvent(GameEventKind.Kill, victim.Id, killerId));
	}

	void UpdateRespawns(float dt) {
		foreach ( var player in _players.Values ) {
			if ( player.Alive || !player.Connected || (player.RespawnTimer <= 0f) ) {
				continue;
			}
			player.RespawnTimer -= dt;
			if ( player.RespawnTimer <= 0f ) {
				Spawn(player);
			}
		}
	}

	void Spawn(SimPlayer player) {
		player.Position = FindFreeSpot();
		player.Health = Config.MaxHealth;
		player.Alive = true;
		player.RespawnTimer = 0f;
		player.FireCooldown = 0f;
		_events.Add(new GameEvent(GameEventKind.Spawn, player.Id, 0));
	}

	/// <summary>Random point that no living player occupies; falls back to the least crowded candidate.</summary>
	public Vec2 FindFreeSpot() {
		var margin = Config.PlayerRadius + 0.1f;
		var minX = Config.ArenaMinX + margin;
		var maxX = Config.ArenaMaxX - margin;
		var minY = Config.ArenaMinY + margin;
		var maxY = Config.ArenaMaxY - margin;
		var required = Config.PlayerRadius * 2f;
		var best = Vec2.Zero;
		var bestClearance = float.MinValue;
		for ( var attempt = 0; attempt < 200; attempt++ ) {
			var candidate = new Vec2(
				minX + ((float)_random.NextDouble() * (maxX - minX)),
				minY + ((float)_random.NextDouble() * (maxY - minY)));
			var clearance = float.MaxValue;
			foreach ( var other in _players.Values ) {
				if ( !other.Alive ) {
					continue;
				}
				clearance = MathF.Min(clearance, (other.Position - candidate).Length);
			}
			if ( clearance >= required ) {
				return candidate;
			}
			if ( clearance > bestClearance ) {
				bestClearance = clearance;
				best = candidate;
			}
		}
		return best;
	}

	public Vec2 ClampToArena(Vec2 position) {
		var margin = Config.PlayerRadius;
		return new Vec2(
			Math.Clamp(position.X, Config.ArenaMinX + margin, Config.ArenaMaxX - margin),
			Math.Clamp(position.Y, Config.ArenaMinY + margin, Config.ArenaMaxY - margin));
	}

	bool OutsideArena(Vec2 position) =>
		(position.X < Config.ArenaMinX) || (position.X > Config.ArenaMaxX) ||
		(position.Y < Config.ArenaMinY) || (position.Y > Config.ArenaMaxY);
}
