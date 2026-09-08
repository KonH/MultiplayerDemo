using UnityEngine;

namespace MultiplayerDemo.Client.Net {
	/// <summary>
	/// The only place that knows how the wire format is encoded. Pure functions so the EditMode
	/// tests can exercise the whole protocol without a socket or an ECS world.
	/// </summary>
	public static class ArenaProtocol {
		public const string TypeWelcome = "welcome";
		public const string TypeReject = "reject";
		public const string TypeState = "state";
		public const string TypeRoster = "roster";
		public const string TypePing = "ping";
		public const string TypeEvent = "event";

		public const int BeaconPort = 47777;
		public const string BeaconMagic = "MPDEMO1";

		/// <summary>Returns the `t` discriminator, or an empty string when the payload is not a message.</summary>
		public static string ReadType(string json) {
			if ( string.IsNullOrEmpty(json) ) {
				return string.Empty;
			}
			var envelope = TryParse<MessageEnvelope>(json);
			return envelope?.t ?? string.Empty;
		}

		public static WelcomeMessage ParseWelcome(string json) {
			var message = TryParse<WelcomeMessage>(json);
			if ( message?.arena == null ) {
				return null;
			}
			return message;
		}

		public static RejectMessage ParseReject(string json) => TryParse<RejectMessage>(json);

		public static StateMessage ParseState(string json) {
			var message = TryParse<StateMessage>(json);
			if ( message == null ) {
				return null;
			}
			message.players ??= new();
			message.bullets ??= new();
			return message;
		}

		public static RosterMessage ParseRoster(string json) {
			var message = TryParse<RosterMessage>(json);
			if ( message == null ) {
				return null;
			}
			message.players ??= new();
			return message;
		}

		public static PingMessage ParsePing(string json) => TryParse<PingMessage>(json);

		public static EventMessage ParseEvent(string json) => TryParse<EventMessage>(json);

		public static ServerInfoMessage ParseServerInfo(string json) => TryParse<ServerInfoMessage>(json);

		public static BeaconMessage ParseBeacon(string json) {
			var message = TryParse<BeaconMessage>(json);
			return (message?.magic == BeaconMagic) ? message : null;
		}

		public static string BuildJoin(string playerName) =>
			JsonUtility.ToJson(new JoinMessage { name = playerName });

		public static string BuildInput(float mx, float my, bool fire) =>
			JsonUtility.ToJson(new InputMessage { mx = mx, my = my, fire = fire });

		public static string BuildPong(int id) => JsonUtility.ToJson(new PongMessage { id = id });

		/// <summary>`ws://host:port/ws` from whatever the player typed into the address box.</summary>
		public static string BuildSocketUrl(string address) {
			var trimmed = (address ?? string.Empty).Trim();
			if ( trimmed.Length == 0 ) {
				return string.Empty;
			}
			if ( trimmed.StartsWith("ws://") || trimmed.StartsWith("wss://") ) {
				return trimmed.EndsWith("/ws") ? trimmed : (trimmed.TrimEnd('/') + "/ws");
			}
			if ( trimmed.StartsWith("http://") ) {
				trimmed = "ws://" + trimmed.Substring("http://".Length);
			} else if ( trimmed.StartsWith("https://") ) {
				trimmed = "wss://" + trimmed.Substring("https://".Length);
			} else {
				trimmed = "ws://" + trimmed;
			}
			return trimmed.TrimEnd('/') + "/ws";
		}

		public static string BuildInfoUrl(string address) {
			var trimmed = (address ?? string.Empty).Trim();
			if ( trimmed.Length == 0 ) {
				return string.Empty;
			}
			if ( trimmed.StartsWith("ws://") ) {
				trimmed = "http://" + trimmed.Substring("ws://".Length);
			} else if ( trimmed.StartsWith("wss://") ) {
				trimmed = "https://" + trimmed.Substring("wss://".Length);
			} else if ( !trimmed.StartsWith("http://") && !trimmed.StartsWith("https://") ) {
				trimmed = "http://" + trimmed;
			}
			if ( trimmed.EndsWith("/ws") ) {
				trimmed = trimmed.Substring(0, trimmed.Length - "/ws".Length);
			}
			return trimmed.TrimEnd('/') + "/api/info";
		}

		static T TryParse<T>(string json) where T : class {
			if ( string.IsNullOrEmpty(json) ) {
				return null;
			}
			try {
				return JsonUtility.FromJson<T>(json);
			} catch ( System.Exception ) {
				// Malformed frames are the server's problem, never a reason to kill the client.
				return null;
			}
		}
	}
}
