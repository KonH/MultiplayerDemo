using MultiplayerDemo.Client.Net;
using NUnit.Framework;

namespace MultiplayerDemo.Client.Tests {
	/// <summary>Parses the exact payloads from docs/specs/.../protocol.md.</summary>
	public sealed class ArenaProtocolTests {
		const string WelcomeJson =
			"{\"t\":\"welcome\",\"id\":3,\"tickRate\":30," +
			"\"arena\":{\"minX\":-20,\"minY\":-20,\"maxX\":20,\"maxY\":20}," +
			"\"playerRadius\":0.5,\"bulletRadius\":0.15,\"moveSpeed\":6," +
			"\"maxHealth\":5,\"respawnMs\":5000,\"fireCooldownMs\":500," +
			"\"startedAtMs\":1690000000000,\"serverNowMs\":1690000012345}";

		const string StateJson =
			"{\"t\":\"state\",\"tick\":123,\"elapsedMs\":41000," +
			"\"players\":[{\"id\":3,\"x\":1.5,\"y\":-2.0,\"hp\":4,\"dx\":0,\"dy\":1,\"cd\":250}]," +
			"\"bullets\":[{\"id\":88,\"x\":3.0,\"y\":1.0,\"o\":3}]}";

		const string RosterJson =
			"{\"t\":\"roster\",\"players\":[{\"id\":3,\"name\":\"kon\",\"kills\":2,\"deaths\":1," +
			"\"connected\":true,\"ip\":\"127.0.0.1\",\"client\":\"web\",\"ping\":12,\"respawnIn\":3200}]}";

		[Test]
		public void ReadType_ReturnsDiscriminator() {
			Assert.AreEqual("welcome", ArenaProtocol.ReadType(WelcomeJson));
			Assert.AreEqual("state", ArenaProtocol.ReadType(StateJson));
			Assert.AreEqual("roster", ArenaProtocol.ReadType(RosterJson));
			Assert.AreEqual("ping", ArenaProtocol.ReadType("{\"t\":\"ping\",\"id\":42}"));
			Assert.AreEqual(string.Empty, ArenaProtocol.ReadType(string.Empty));
		}

		[Test]
		public void ParseWelcome_ReadsArenaAndRules() {
			var welcome = ArenaProtocol.ParseWelcome(WelcomeJson);
			Assert.NotNull(welcome);
			Assert.AreEqual(3, welcome.id);
			Assert.AreEqual(30, welcome.tickRate);
			Assert.AreEqual(-20f, welcome.arena.minX);
			Assert.AreEqual(20f, welcome.arena.maxY);
			Assert.AreEqual(0.5f, welcome.playerRadius);
			Assert.AreEqual(0.15f, welcome.bulletRadius);
			Assert.AreEqual(6f, welcome.moveSpeed);
			Assert.AreEqual(5, welcome.maxHealth);
			Assert.AreEqual(5000, welcome.respawnMs);
			Assert.AreEqual(500, welcome.fireCooldownMs);
			Assert.AreEqual(1690000012345L, welcome.serverNowMs);
		}

		[Test]
		public void ParseState_ReadsPlayersAndBullets() {
			var state = ArenaProtocol.ParseState(StateJson);
			Assert.NotNull(state);
			Assert.AreEqual(123, state.tick);
			Assert.AreEqual(41000L, state.elapsedMs);
			Assert.AreEqual(1, state.players.Count);
			var player = state.players[0];
			Assert.AreEqual(3, player.id);
			Assert.AreEqual(1.5f, player.x);
			Assert.AreEqual(-2f, player.y);
			Assert.AreEqual(4, player.hp);
			Assert.AreEqual(1f, player.dy);
			Assert.AreEqual(250, player.cd);
			Assert.AreEqual(1, state.bullets.Count);
			Assert.AreEqual(88, state.bullets[0].id);
			Assert.AreEqual(3, state.bullets[0].o);
		}

		[Test]
		public void ParseState_EmptyCollectionsAreNeverNull() {
			var state = ArenaProtocol.ParseState("{\"t\":\"state\",\"tick\":1,\"elapsedMs\":0}");
			Assert.NotNull(state);
			Assert.AreEqual(0, state.players.Count);
			Assert.AreEqual(0, state.bullets.Count);
		}

		[Test]
		public void ParseRoster_ReadsEveryLeaderboardColumn() {
			var roster = ArenaProtocol.ParseRoster(RosterJson);
			Assert.NotNull(roster);
			Assert.AreEqual(1, roster.players.Count);
			var player = roster.players[0];
			Assert.AreEqual(3, player.id);
			Assert.AreEqual("kon", player.name);
			Assert.AreEqual(2, player.kills);
			Assert.AreEqual(1, player.deaths);
			Assert.IsTrue(player.connected);
			Assert.AreEqual("127.0.0.1", player.ip);
			Assert.AreEqual("web", player.client);
			Assert.AreEqual(12, player.ping);
			Assert.AreEqual(3200, player.respawnIn);
		}

		[Test]
		public void ParsePingAndEvent() {
			var ping = ArenaProtocol.ParsePing("{\"t\":\"ping\",\"id\":42}");
			Assert.NotNull(ping);
			Assert.AreEqual(42, ping.id);

			var kill = ArenaProtocol.ParseEvent("{\"t\":\"event\",\"kind\":\"kill\",\"victim\":3,\"killer\":5}");
			Assert.NotNull(kill);
			Assert.AreEqual("kill", kill.kind);
			Assert.AreEqual(3, kill.victim);
			Assert.AreEqual(5, kill.killer);

			var spawn = ArenaProtocol.ParseEvent("{\"t\":\"event\",\"kind\":\"spawn\",\"victim\":3}");
			Assert.NotNull(spawn);
			Assert.AreEqual("spawn", spawn.kind);
			Assert.AreEqual(0, spawn.killer);
		}

		[Test]
		public void ParseReject_ReadsReason() {
			var reject = ArenaProtocol.ParseReject("{\"t\":\"reject\",\"reason\":\"Name already taken\"}");
			Assert.NotNull(reject);
			Assert.AreEqual("Name already taken", reject.reason);
		}

		[Test]
		public void ParseBeacon_RequiresTheMagic() {
			var good = ArenaProtocol.ParseBeacon(
				"{\"magic\":\"MPDEMO1\",\"name\":\"Arena\",\"port\":8080,\"players\":2}");
			Assert.NotNull(good);
			Assert.AreEqual(8080, good.port);
			Assert.AreEqual(2, good.players);
			Assert.IsNull(ArenaProtocol.ParseBeacon("{\"magic\":\"NOPE\",\"port\":8080}"));
		}

		[Test]
		public void ParseServerInfo_ReadsHttpDescriptor() {
			var info = ArenaProtocol.ParseServerInfo(
				"{\"name\":\"Arena\",\"players\":2,\"maxPlayers\":32,\"version\":1," +
				"\"tickRate\":30,\"wsPath\":\"/ws\",\"uptimeMs\":12345}");
			Assert.NotNull(info);
			Assert.AreEqual("Arena", info.name);
			Assert.AreEqual(32, info.maxPlayers);
			Assert.AreEqual("/ws", info.wsPath);
		}

		[Test]
		public void BuildJoin_MatchesTheContract() {
			Assert.AreEqual("{\"t\":\"join\",\"name\":\"kon\",\"client\":\"unity\"}",
				ArenaProtocol.BuildJoin("kon"));
		}

		[Test]
		public void BuildPong_EchoesTheId() {
			Assert.AreEqual("{\"t\":\"pong\",\"id\":42}", ArenaProtocol.BuildPong(42));
		}

		[Test]
		public void BuildInput_RoundTripsThroughTheServerShape() {
			var json = ArenaProtocol.BuildInput(1f, -1f, true);
			StringAssert.Contains("\"t\":\"input\"", json);
			StringAssert.Contains("\"mx\":1", json);
			StringAssert.Contains("\"my\":-1", json);
			StringAssert.Contains("\"fire\":true", json);
		}

		[Test]
		public void BuildSocketUrl_AcceptsEveryFormThePlayerMightType() {
			Assert.AreEqual("ws://localhost:8080/ws", ArenaProtocol.BuildSocketUrl("localhost:8080"));
			Assert.AreEqual("ws://localhost:8080/ws", ArenaProtocol.BuildSocketUrl(" localhost:8080/ "));
			Assert.AreEqual("ws://localhost:8080/ws", ArenaProtocol.BuildSocketUrl("http://localhost:8080"));
			Assert.AreEqual("ws://localhost:8080/ws", ArenaProtocol.BuildSocketUrl("ws://localhost:8080/ws"));
			Assert.AreEqual("wss://arena.example/ws", ArenaProtocol.BuildSocketUrl("https://arena.example"));
			Assert.AreEqual(string.Empty, ArenaProtocol.BuildSocketUrl("  "));
		}

		[Test]
		public void BuildInfoUrl_PointsAtTheDiscoveryEndpoint() {
			Assert.AreEqual("http://localhost:8080/api/info", ArenaProtocol.BuildInfoUrl("localhost:8080"));
			Assert.AreEqual("http://localhost:8080/api/info", ArenaProtocol.BuildInfoUrl("ws://localhost:8080/ws"));
		}
	}
}
