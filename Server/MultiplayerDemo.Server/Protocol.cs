using System.Text.Json.Serialization;

namespace MultiplayerDemo.Server;

// Wire shapes for docs/specs/26_09_08_09_multiplayer_arena/protocol.md.
// Field names are deliberately short: snapshots go out 30 times a second.

public sealed class ClientMessage {
	[JsonPropertyName("t")] public string T { get; set; } = string.Empty;
	[JsonPropertyName("name")] public string? Name { get; set; }
	[JsonPropertyName("client")] public string? Client { get; set; }
	[JsonPropertyName("mx")] public float Mx { get; set; }
	[JsonPropertyName("my")] public float My { get; set; }
	[JsonPropertyName("fire")] public bool Fire { get; set; }
	[JsonPropertyName("id")] public int Id { get; set; }
}

public sealed class ArenaBounds {
	[JsonPropertyName("minX")] public float MinX { get; set; }
	[JsonPropertyName("minY")] public float MinY { get; set; }
	[JsonPropertyName("maxX")] public float MaxX { get; set; }
	[JsonPropertyName("maxY")] public float MaxY { get; set; }
}

public sealed class WelcomeMessage {
	[JsonPropertyName("t")] public string T => "welcome";
	[JsonPropertyName("id")] public int Id { get; set; }
	[JsonPropertyName("tickRate")] public int TickRate { get; set; }
	[JsonPropertyName("arena")] public ArenaBounds Arena { get; set; } = new();
	[JsonPropertyName("playerRadius")] public float PlayerRadius { get; set; }
	[JsonPropertyName("bulletRadius")] public float BulletRadius { get; set; }
	[JsonPropertyName("moveSpeed")] public float MoveSpeed { get; set; }
	[JsonPropertyName("maxHealth")] public int MaxHealth { get; set; }
	[JsonPropertyName("respawnMs")] public int RespawnMs { get; set; }
	[JsonPropertyName("fireCooldownMs")] public int FireCooldownMs { get; set; }
	[JsonPropertyName("startedAtMs")] public long StartedAtMs { get; set; }
	[JsonPropertyName("serverNowMs")] public long ServerNowMs { get; set; }
}

public sealed class RejectMessage {
	[JsonPropertyName("t")] public string T => "reject";
	[JsonPropertyName("reason")] public string Reason { get; set; } = string.Empty;
}

public sealed class StatePlayer {
	[JsonPropertyName("id")] public int Id { get; set; }
	[JsonPropertyName("x")] public float X { get; set; }
	[JsonPropertyName("y")] public float Y { get; set; }
	[JsonPropertyName("hp")] public int Hp { get; set; }
	[JsonPropertyName("dx")] public float Dx { get; set; }
	[JsonPropertyName("dy")] public float Dy { get; set; }
	[JsonPropertyName("cd")] public int Cd { get; set; }
}

public sealed class StateBullet {
	[JsonPropertyName("id")] public int Id { get; set; }
	[JsonPropertyName("x")] public float X { get; set; }
	[JsonPropertyName("y")] public float Y { get; set; }
	[JsonPropertyName("o")] public int O { get; set; }
}

public sealed class StateMessage {
	[JsonPropertyName("t")] public string T => "state";
	[JsonPropertyName("tick")] public int Tick { get; set; }
	[JsonPropertyName("elapsedMs")] public long ElapsedMs { get; set; }
	[JsonPropertyName("players")] public List<StatePlayer> Players { get; set; } = new();
	[JsonPropertyName("bullets")] public List<StateBullet> Bullets { get; set; } = new();
}

public sealed class RosterPlayer {
	[JsonPropertyName("id")] public int Id { get; set; }
	[JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
	[JsonPropertyName("kills")] public int Kills { get; set; }
	[JsonPropertyName("deaths")] public int Deaths { get; set; }
	[JsonPropertyName("connected")] public bool Connected { get; set; }
	[JsonPropertyName("ip")] public string Ip { get; set; } = string.Empty;
	[JsonPropertyName("client")] public string Client { get; set; } = string.Empty;
	[JsonPropertyName("ping")] public int Ping { get; set; }
	[JsonPropertyName("respawnIn")] public int RespawnIn { get; set; }
}

public sealed class RosterMessage {
	[JsonPropertyName("t")] public string T => "roster";
	[JsonPropertyName("players")] public List<RosterPlayer> Players { get; set; } = new();
}

public sealed class PingMessage {
	[JsonPropertyName("t")] public string T => "ping";
	[JsonPropertyName("id")] public int Id { get; set; }
}

public sealed class EventMessage {
	[JsonPropertyName("t")] public string T => "event";
	[JsonPropertyName("kind")] public string Kind { get; set; } = string.Empty;
	[JsonPropertyName("victim")] public int Victim { get; set; }
	[JsonPropertyName("killer")] public int Killer { get; set; }
}

public sealed class ServerInfo {
	[JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
	[JsonPropertyName("players")] public int Players { get; set; }
	[JsonPropertyName("maxPlayers")] public int MaxPlayers { get; set; }
	[JsonPropertyName("version")] public int Version => 1;
	[JsonPropertyName("tickRate")] public int TickRate { get; set; }
	[JsonPropertyName("wsPath")] public string WsPath => "/ws";
	[JsonPropertyName("uptimeMs")] public long UptimeMs { get; set; }
}

public sealed class BeaconMessage {
	[JsonPropertyName("magic")] public string Magic => "MPDEMO1";
	[JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
	[JsonPropertyName("port")] public int Port { get; set; }
	[JsonPropertyName("players")] public int Players { get; set; }
}
