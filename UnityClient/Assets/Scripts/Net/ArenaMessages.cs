using System;
using System.Collections.Generic;

namespace MultiplayerDemo.Client.Net {
	// Wire shapes for docs/specs/26_09_08_09_multiplayer_arena/protocol.md.
	// Field names match the JSON keys verbatim because UnityEngine.JsonUtility maps by field name.

	[Serializable]
	public sealed class MessageEnvelope {
		public string t;
	}

	[Serializable]
	public sealed class ArenaBoundsMessage {
		public float minX;
		public float minY;
		public float maxX;
		public float maxY;
	}

	[Serializable]
	public sealed class WelcomeMessage {
		public string t;
		public int id;
		public int tickRate;
		public ArenaBoundsMessage arena;
		public float playerRadius;
		public float bulletRadius;
		public float moveSpeed;
		public int maxHealth;
		public int respawnMs;
		public int fireCooldownMs;
		public long startedAtMs;
		public long serverNowMs;
	}

	[Serializable]
	public sealed class RejectMessage {
		public string t;
		public string reason;
	}

	[Serializable]
	public sealed class StatePlayerMessage {
		public int id;
		public float x;
		public float y;
		public int hp;
		public float dx;
		public float dy;
		public int cd;
	}

	[Serializable]
	public sealed class StateBulletMessage {
		public int id;
		public float x;
		public float y;
		public int o;
	}

	[Serializable]
	public sealed class StateMessage {
		public string t;
		public int tick;
		public long elapsedMs;
		public List<StatePlayerMessage> players = new();
		public List<StateBulletMessage> bullets = new();
	}

	[Serializable]
	public sealed class RosterPlayerMessage {
		public int id;
		public string name;
		public int kills;
		public int deaths;
		public bool connected;
		public string ip;
		public string client;
		public int ping;
		public int respawnIn;
	}

	[Serializable]
	public sealed class RosterMessage {
		public string t;
		public List<RosterPlayerMessage> players = new();
	}

	[Serializable]
	public sealed class PingMessage {
		public string t;
		public int id;
	}

	[Serializable]
	public sealed class EventMessage {
		public string t;
		public string kind;
		public int victim;
		public int killer;
	}

	[Serializable]
	public sealed class ServerInfoMessage {
		public string name;
		public int players;
		public int maxPlayers;
		public int version;
		public int tickRate;
		public string wsPath;
		public long uptimeMs;
	}

	[Serializable]
	public sealed class BeaconMessage {
		public string magic;
		public string name;
		public int port;
		public int players;
	}

	// Client -> server payloads.

	[Serializable]
	public sealed class JoinMessage {
		public string t = "join";
		public string name;
		public string client = "unity";
	}

	[Serializable]
	public sealed class InputMessage {
		public string t = "input";
		public float mx;
		public float my;
		public bool fire;
	}

	[Serializable]
	public sealed class PongMessage {
		public string t = "pong";
		public int id;
	}
}
