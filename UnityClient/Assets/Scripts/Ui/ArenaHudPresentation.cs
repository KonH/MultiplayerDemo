using System;
using System.Collections.Generic;
using MultiplayerDemo.Client.Ecs;

namespace MultiplayerDemo.Client.Ui {
	public readonly struct ArenaDiscoveryPresentationInput {
		public string Name { get; }
		public string Address { get; }
		public int Players { get; }

		public ArenaDiscoveryPresentationInput(string name, string address, int players) {
			Name = name ?? string.Empty;
			Address = address ?? string.Empty;
			Players = players;
		}
	}

	public readonly struct ArenaRosterPresentationInput {
		public int Id { get; }
		public string Name { get; }
		public int Kills { get; }
		public int Deaths { get; }
		public bool Connected { get; }
		public string Ip { get; }
		public string Client { get; }
		public int Ping { get; }

		public ArenaRosterPresentationInput(
			int id, string name, int kills, int deaths, bool connected, string ip, string client, int ping) {
			Id = id;
			Name = name ?? string.Empty;
			Kills = kills;
			Deaths = deaths;
			Connected = connected;
			Ip = ip ?? string.Empty;
			Client = client ?? string.Empty;
			Ping = ping;
		}
	}

	public readonly struct ArenaConnectionPresentationInput {
		public string PlayerName { get; }
		public string Address { get; }
		public ArenaClientStatus Status { get; }
		public string Message { get; }
		public string DiscoveryDescription { get; }
		public IReadOnlyList<ArenaDiscoveryPresentationInput> Servers { get; }

		public ArenaConnectionPresentationInput(
			string playerName,
			string address,
			ArenaClientStatus status,
			string message,
			string discoveryDescription,
			IReadOnlyList<ArenaDiscoveryPresentationInput> servers) {
			PlayerName = playerName ?? string.Empty;
			Address = address ?? string.Empty;
			Status = status;
			Message = message ?? string.Empty;
			DiscoveryDescription = discoveryDescription ?? string.Empty;
			Servers = servers ?? Array.Empty<ArenaDiscoveryPresentationInput>();
		}
	}

	public readonly struct ArenaGameplayPresentationInput {
		public bool HasLocalPlayer { get; }
		public int Health { get; }
		public int MaxHealth { get; }
		public int FireCooldownMs { get; }
		public int RespawnMs { get; }
		public long ElapsedMs { get; }
		public int PlayerCount { get; }
		public int BulletCount { get; }
		public int SnapshotsReceived { get; }

		public ArenaGameplayPresentationInput(
			bool hasLocalPlayer,
			int health,
			int maxHealth,
			int fireCooldownMs,
			int respawnMs,
			long elapsedMs,
			int playerCount,
			int bulletCount,
			int snapshotsReceived) {
			HasLocalPlayer = hasLocalPlayer;
			Health = health;
			MaxHealth = maxHealth;
			FireCooldownMs = fireCooldownMs;
			RespawnMs = respawnMs;
			ElapsedMs = elapsedMs;
			PlayerCount = playerCount;
			BulletCount = bulletCount;
			SnapshotsReceived = snapshotsReceived;
		}
	}

	public readonly struct ArenaLeaderboardPresentationInput {
		public bool Visible { get; }
		public int? LocalPlayerId { get; }
		public IReadOnlyList<ArenaRosterPresentationInput> Roster { get; }

		public ArenaLeaderboardPresentationInput(
			bool visible, int? localPlayerId, IReadOnlyList<ArenaRosterPresentationInput> roster) {
			Visible = visible;
			LocalPlayerId = localPlayerId;
			Roster = roster ?? Array.Empty<ArenaRosterPresentationInput>();
		}
	}

	public readonly struct ArenaHudPresentationInput {
		public ArenaConnectionPresentationInput Connection { get; }
		public ArenaGameplayPresentationInput Gameplay { get; }
		public ArenaLeaderboardPresentationInput Leaderboard { get; }

		public ArenaHudPresentationInput(
			ArenaConnectionPresentationInput connection,
			ArenaGameplayPresentationInput gameplay,
			ArenaLeaderboardPresentationInput leaderboard) {
			Connection = connection;
			Gameplay = gameplay;
			Leaderboard = leaderboard;
		}
	}

	public sealed class ArenaDiscoveryPresentationState {
		public string Label { get; }
		public string Address { get; }

		internal ArenaDiscoveryPresentationState(string label, string address) {
			Label = label;
			Address = address;
		}
	}

	public sealed class ArenaConnectPresentationState {
		public bool Visible { get; }
		public string PlayerName { get; }
		public string Address { get; }
		public string DiscoveryDescription { get; }
		public IReadOnlyList<ArenaDiscoveryPresentationState> Servers { get; }
		public string StatusText { get; }
		public bool IsStatusError { get; }

		internal ArenaConnectPresentationState(
			bool visible,
			string playerName,
			string address,
			string discoveryDescription,
			IReadOnlyList<ArenaDiscoveryPresentationState> servers,
			string statusText,
			bool isStatusError) {
			Visible = visible;
			PlayerName = playerName;
			Address = address;
			DiscoveryDescription = discoveryDescription;
			Servers = servers;
			StatusText = statusText;
			IsStatusError = isStatusError;
		}
	}

	public sealed class ArenaGameplayPresentationState {
		public bool Visible { get; }
		public string PlayerName { get; }
		public string HealthText { get; }
		public string FireText { get; }
		public string LifeStatusText { get; }
		public string RespawnText { get; }
		public string TimeText { get; }
		public string DebugText { get; }

		internal ArenaGameplayPresentationState(
			bool visible,
			string playerName,
			string healthText,
			string fireText,
			string lifeStatusText,
			string respawnText,
			string timeText,
			string debugText) {
			Visible = visible;
			PlayerName = playerName;
			HealthText = healthText;
			FireText = fireText;
			LifeStatusText = lifeStatusText;
			RespawnText = respawnText;
			TimeText = timeText;
			DebugText = debugText;
		}
	}

	public sealed class ArenaRosterPresentationState {
		public string Name { get; }
		public string Kills { get; }
		public string Deaths { get; }
		public string Status { get; }
		public string Ip { get; }
		public string Client { get; }
		public string Ping { get; }

		internal ArenaRosterPresentationState(
			string name, string kills, string deaths, string status, string ip, string client, string ping) {
			Name = name;
			Kills = kills;
			Deaths = deaths;
			Status = status;
			Ip = ip;
			Client = client;
			Ping = ping;
		}
	}

	public sealed class ArenaLeaderboardPresentationState {
		public bool Visible { get; }
		public IReadOnlyList<ArenaRosterPresentationState> Rows { get; }

		internal ArenaLeaderboardPresentationState(
			bool visible, IReadOnlyList<ArenaRosterPresentationState> rows) {
			Visible = visible;
			Rows = rows;
		}
	}

	public sealed class ArenaHudPresentationState {
		public ArenaConnectPresentationState Connect { get; }
		public ArenaGameplayPresentationState Hud { get; }
		public ArenaLeaderboardPresentationState Leaderboard { get; }

		internal ArenaHudPresentationState(
			ArenaConnectPresentationState connect,
			ArenaGameplayPresentationState hud,
			ArenaLeaderboardPresentationState leaderboard) {
			Connect = connect;
			Hud = hud;
			Leaderboard = leaderboard;
		}
	}

	/// <summary>Maps runtime snapshots made of plain values into text and rows consumed by the UI view.</summary>
	public static class ArenaHudProjector {
		public static ArenaHudPresentationState Project(ArenaHudPresentationInput input) {
			var playing = input.Connection.Status == ArenaClientStatus.Playing;
			var servers = ProjectServers(input.Connection.Servers);
			var rows = ProjectRoster(input.Leaderboard.Roster, input.Leaderboard.LocalPlayerId);
			var statusText = StatusText(input.Connection.Status, input.Connection.Message);

			var healthText = string.Empty;
			var fireText = string.Empty;
			var lifeStatusText = string.Empty;
			var respawnText = string.Empty;
			if ( input.Gameplay.HasLocalPlayer ) {
				healthText = $"HP  {input.Gameplay.Health} / {input.Gameplay.MaxHealth}";
				fireText = input.Gameplay.FireCooldownMs > 0
					? $"Fire  {ArenaFormat.Seconds(input.Gameplay.FireCooldownMs)}"
					: "Fire  ready";
			} else {
				lifeStatusText = "Dead";
				respawnText = $"Respawn in  {ArenaFormat.Seconds(input.Gameplay.RespawnMs)}";
			}

			return new ArenaHudPresentationState(
				new ArenaConnectPresentationState(
					!playing,
					input.Connection.PlayerName,
					input.Connection.Address,
					input.Connection.DiscoveryDescription,
					servers,
					statusText,
					(input.Connection.Status == ArenaClientStatus.Rejected) ||
					(input.Connection.Status == ArenaClientStatus.Failed)),
				new ArenaGameplayPresentationState(
					playing,
					input.Connection.PlayerName,
					healthText,
					fireText,
					lifeStatusText,
					respawnText,
					$"Time  {ArenaFormat.Elapsed(input.Gameplay.ElapsedMs)}",
					$"players {input.Gameplay.PlayerCount}   bullets {input.Gameplay.BulletCount}   " +
					$"snapshots {input.Gameplay.SnapshotsReceived}"),
				new ArenaLeaderboardPresentationState(
					playing && input.Leaderboard.Visible,
					rows));
		}

		static IReadOnlyList<ArenaDiscoveryPresentationState> ProjectServers(
			IReadOnlyList<ArenaDiscoveryPresentationInput> servers) {
			if ( servers.Count == 0 ) {
				return Array.Empty<ArenaDiscoveryPresentationState>();
			}
			var projected = new ArenaDiscoveryPresentationState[servers.Count];
			for ( var i = 0; i < servers.Count; i++ ) {
				var server = servers[i];
				projected[i] = new ArenaDiscoveryPresentationState(
					$"{server.Name}  {server.Address}  ({server.Players} players)", server.Address);
			}
			return projected;
		}

		static IReadOnlyList<ArenaRosterPresentationState> ProjectRoster(
			IReadOnlyList<ArenaRosterPresentationInput> roster, int? localPlayerId) {
			if ( roster.Count == 0 ) {
				return Array.Empty<ArenaRosterPresentationState>();
			}
			var projected = new ArenaRosterPresentationState[roster.Count];
			for ( var i = 0; i < roster.Count; i++ ) {
				var player = roster[i];
				var name = (localPlayerId.HasValue && (player.Id == localPlayerId.Value))
					? player.Name + " (you)"
					: player.Name;
				projected[i] = new ArenaRosterPresentationState(
					name,
					player.Kills.ToString(),
					player.Deaths.ToString(),
					player.Connected ? "connected" : "disconnected",
					player.Ip,
					player.Client,
					player.Ping + " ms");
			}
			return projected;
		}

		static string StatusText(ArenaClientStatus status, string message) {
			return status switch {
				ArenaClientStatus.Connecting => $"Connecting to {message}...",
				ArenaClientStatus.Joining => "Joining...",
				ArenaClientStatus.Rejected => $"Rejected: {message}",
				ArenaClientStatus.Failed => $"Failed: {message}",
				ArenaClientStatus.Playing => "Connected",
				_ => string.IsNullOrEmpty(message) ? "Not connected" : message
			};
		}
	}
}
