using System;
using MultiplayerDemo.Client.Ecs;
using MultiplayerDemo.Client.Ui;
using NUnit.Framework;

namespace MultiplayerDemo.Client.Tests {
	public sealed class ArenaHudPresentationTests {
		[TestCase(ArenaClientStatus.Disconnected, "", "Not connected", false)]
		[TestCase(ArenaClientStatus.Disconnected, "Socket closed", "Socket closed", false)]
		[TestCase(ArenaClientStatus.Connecting, "ws://localhost:8080/ws", "Connecting to ws://localhost:8080/ws...", false)]
		[TestCase(ArenaClientStatus.Joining, "", "Joining...", false)]
		[TestCase(ArenaClientStatus.Rejected, "Server full", "Rejected: Server full", true)]
		[TestCase(ArenaClientStatus.Failed, "Connection refused", "Failed: Connection refused", true)]
		public void ConnectionStatesAreDisplayReady(
			ArenaClientStatus status, string message, string expectedText, bool expectedError) {
			var state = Project(connection: new ArenaConnectionPresentationInput(
				"Player", "localhost:8080", status, message, "LAN", Array.Empty<ArenaDiscoveryPresentationInput>()));

			Assert.IsTrue(state.Connect.Visible);
			Assert.IsFalse(state.Hud.Visible);
			Assert.AreEqual(expectedText, state.Connect.StatusText);
			Assert.AreEqual(expectedError, state.Connect.IsStatusError);
		}

		[Test]
		public void DiscoveryRowsIncludeSelectionAddressAndPlayerCount() {
			var servers = new[] {
				new ArenaDiscoveryPresentationInput("Local arena", "192.168.1.10:8080", 3)
			};
			var state = Project(connection: new ArenaConnectionPresentationInput(
				"Player", "localhost:8080", ArenaClientStatus.Disconnected, "", "LAN beacon", servers));

			Assert.AreEqual("Player", state.Connect.PlayerName);
			Assert.AreEqual("localhost:8080", state.Connect.Address);
			Assert.AreEqual("LAN beacon", state.Connect.DiscoveryDescription);
			Assert.AreEqual(1, state.Connect.Servers.Count);
			Assert.AreEqual("Local arena  192.168.1.10:8080  (3 players)", state.Connect.Servers[0].Label);
			Assert.AreEqual("192.168.1.10:8080", state.Connect.Servers[0].Address);
		}

		[Test]
		public void PlayingAliveStateFormatsHealthCooldownAndCounters() {
			var gameplay = new ArenaGameplayPresentationInput(
				hasLocalPlayer: true,
				health: 7,
				maxHealth: 10,
				fireCooldownMs: 500,
				respawnMs: 0,
				elapsedMs: 3_661_000,
				playerCount: 4,
				bulletCount: 2,
				snapshotsReceived: 120);

			var state = Project(status: ArenaClientStatus.Playing, gameplay: gameplay);

			Assert.IsFalse(state.Connect.Visible);
			Assert.IsTrue(state.Hud.Visible);
			Assert.AreEqual("Player", state.Hud.PlayerName);
			Assert.AreEqual("HP  7 / 10", state.Hud.HealthText);
			Assert.AreEqual("Fire  0.5 s", state.Hud.FireText);
			Assert.AreEqual("", state.Hud.LifeStatusText);
			Assert.AreEqual("", state.Hud.RespawnText);
			Assert.AreEqual("Time  01:01:01", state.Hud.TimeText);
			Assert.AreEqual("players 4   bullets 2   snapshots 120", state.Hud.DebugText);
		}

		[Test]
		public void CooldownReadyIsDisplayReady() {
			var gameplay = new ArenaGameplayPresentationInput(true, 10, 10, 0, 0, 0, 1, 0, 1);

			var state = Project(status: ArenaClientStatus.Playing, gameplay: gameplay);

			Assert.AreEqual("Fire  ready", state.Hud.FireText);
		}

		[Test]
		public void DeadStateFormatsRespawnAndOmitsAliveValues() {
			var gameplay = new ArenaGameplayPresentationInput(false, 0, 10, 0, 3_200, 42_000, 3, 1, 90);

			var state = Project(status: ArenaClientStatus.Playing, gameplay: gameplay);

			Assert.AreEqual("Dead", state.Hud.LifeStatusText);
			Assert.AreEqual("Respawn in  3.2 s", state.Hud.RespawnText);
			Assert.AreEqual("", state.Hud.HealthText);
			Assert.AreEqual("", state.Hud.FireText);
		}

		[Test]
		public void LeaderboardVisibilityFollowsPlayingAndToggleState() {
			var hidden = Project(
				status: ArenaClientStatus.Playing,
				leaderboard: new ArenaLeaderboardPresentationInput(false, 7, Array.Empty<ArenaRosterPresentationInput>()));
			var visible = Project(
				status: ArenaClientStatus.Playing,
				leaderboard: new ArenaLeaderboardPresentationInput(true, 7, Array.Empty<ArenaRosterPresentationInput>()));
			var disconnected = Project(
				leaderboard: new ArenaLeaderboardPresentationInput(true, 7, Array.Empty<ArenaRosterPresentationInput>()));

			Assert.IsFalse(hidden.Leaderboard.Visible);
			Assert.IsTrue(visible.Leaderboard.Visible);
			Assert.IsFalse(disconnected.Leaderboard.Visible);
		}

		[Test]
		public void RosterRowsContainAllColumnsAndMarkLocalPlayer() {
			var roster = new[] {
				new ArenaRosterPresentationInput(7, "Player", 5, 2, true, "127.0.0.1", "unity", 18),
				new ArenaRosterPresentationInput(9, "Bot", 1, 4, false, "10.0.0.9", "web", 44)
			};
			var state = Project(
				status: ArenaClientStatus.Playing,
				leaderboard: new ArenaLeaderboardPresentationInput(true, 7, roster));

			Assert.AreEqual(2, state.Leaderboard.Rows.Count);
			Assert.AreEqual("Player (you)", state.Leaderboard.Rows[0].Name);
			Assert.AreEqual("5", state.Leaderboard.Rows[0].Kills);
			Assert.AreEqual("2", state.Leaderboard.Rows[0].Deaths);
			Assert.AreEqual("connected", state.Leaderboard.Rows[0].Status);
			Assert.AreEqual("127.0.0.1", state.Leaderboard.Rows[0].Ip);
			Assert.AreEqual("unity", state.Leaderboard.Rows[0].Client);
			Assert.AreEqual("18 ms", state.Leaderboard.Rows[0].Ping);
			Assert.AreEqual("disconnected", state.Leaderboard.Rows[1].Status);
		}

		static ArenaHudPresentationState Project(
			ArenaClientStatus status = ArenaClientStatus.Disconnected,
			ArenaConnectionPresentationInput? connection = null,
			ArenaGameplayPresentationInput? gameplay = null,
			ArenaLeaderboardPresentationInput? leaderboard = null) {
			return ArenaHudProjector.Project(new ArenaHudPresentationInput(
				connection ?? new ArenaConnectionPresentationInput(
					"Player", "localhost:8080", status, "", "LAN", Array.Empty<ArenaDiscoveryPresentationInput>()),
				gameplay ?? new ArenaGameplayPresentationInput(false, 0, 0, 0, 0, 0, 0, 0, 0),
				leaderboard ?? new ArenaLeaderboardPresentationInput(
					false, null, Array.Empty<ArenaRosterPresentationInput>())));
		}
	}
}
