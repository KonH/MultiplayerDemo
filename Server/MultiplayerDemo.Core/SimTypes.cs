namespace MultiplayerDemo.Core;

public enum ClientKind {
	Unknown = 0,
	Unity = 1,
	Web = 2
}

/// <summary>What a client asks for on a given tick. Sanitised by the simulation, never trusted.</summary>
public readonly struct PlayerInput {
	public readonly Vec2 Move;
	public readonly bool Fire;

	public PlayerInput(Vec2 move, bool fire) {
		Move = move;
		Fire = fire;
	}

	public static readonly PlayerInput None = new(Vec2.Zero, false);
}

/// <summary>Authoritative state of one player. Lives for the whole server session, even across reconnects.</summary>
public sealed class SimPlayer {
	public int Id { get; init; }
	public string Name { get; init; } = string.Empty;
	public ClientKind Client { get; set; }
	public string Ip { get; set; } = string.Empty;

	public bool Connected { get; set; } = true;
	public int PingMs { get; set; }

	public Vec2 Position { get; set; }
	public Vec2 Facing { get; set; } = new(0f, 1f);
	public int Health { get; set; }
	public bool Alive { get; set; }

	public float FireCooldown { get; set; }
	public float RespawnTimer { get; set; }

	public int Kills { get; set; }
	public int Deaths { get; set; }

	public PlayerInput Input { get; set; } = PlayerInput.None;
}

public sealed class SimBullet {
	public int Id { get; init; }
	public int OwnerId { get; init; }
	public Vec2 Position { get; set; }
	public Vec2 Velocity { get; init; }
	public float Life { get; set; }
}

public enum GameEventKind {
	Hit,
	Kill,
	Spawn
}

public readonly struct GameEvent {
	public readonly GameEventKind Kind;
	public readonly int VictimId;
	public readonly int KillerId;

	public GameEvent(GameEventKind kind, int victimId, int killerId) {
		Kind = kind;
		VictimId = victimId;
		KillerId = killerId;
	}
}
