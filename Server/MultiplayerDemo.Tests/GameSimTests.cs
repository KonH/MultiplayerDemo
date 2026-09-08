using MultiplayerDemo.Core;
using Xunit;

namespace MultiplayerDemo.Tests;

public sealed class GameSimTests {
	const float Dt = 1f / 30f;

	static GameSim NewSim(GameConfig? config = null) => new(config ?? new GameConfig(), new Random(1234));

	static SimPlayer PlaceAt(GameSim sim, string name, float x, float y) {
		var player = sim.AddPlayer(name, ClientKind.Web, "127.0.0.1");
		player.Position = new Vec2(x, y);
		return player;
	}

	static void Run(GameSim sim, float seconds) {
		var ticks = (int)MathF.Round(seconds / Dt);
		for ( var i = 0; i < ticks; i++ ) {
			sim.Tick(Dt);
		}
	}

	[Fact]
	public void PlayerStartsWithFullHealthAndAlive() {
		var sim = NewSim();
		var player = sim.AddPlayer("a", ClientKind.Unity, "127.0.0.1");

		Assert.True(player.Alive);
		Assert.Equal(sim.Config.MaxHealth, player.Health);
		Assert.Equal(0, player.Kills);
		Assert.Equal(0, player.Deaths);
	}

	[Fact]
	public void MovementFollowsInputAtConfiguredSpeed() {
		var sim = NewSim();
		var player = PlaceAt(sim, "a", 0f, 0f);
		sim.SetInput(player, new PlayerInput(new Vec2(1f, 0f), false));

		Run(sim, 1f);

		Assert.Equal(sim.Config.MoveSpeed, player.Position.X, 1);
		Assert.Equal(0f, player.Position.Y, 3);
	}

	[Fact]
	public void DiagonalInputIsNormalisedSoNobodyMovesFaster() {
		var sim = NewSim();
		var player = PlaceAt(sim, "a", 0f, 0f);
		sim.SetInput(player, new PlayerInput(new Vec2(1f, 1f), false));

		Run(sim, 1f);

		Assert.Equal(sim.Config.MoveSpeed, player.Position.Length, 1);
	}

	[Fact]
	public void PlayerCannotLeaveTheArena() {
		var sim = NewSim();
		var player = PlaceAt(sim, "a", 0f, 0f);
		sim.SetInput(player, new PlayerInput(new Vec2(1f, 1f), false));

		Run(sim, 20f);

		var limitX = sim.Config.ArenaMaxX - sim.Config.PlayerRadius;
		var limitY = sim.Config.ArenaMaxY - sim.Config.PlayerRadius;
		Assert.Equal(limitX, player.Position.X, 3);
		Assert.Equal(limitY, player.Position.Y, 3);
	}

	[Fact]
	public void PlayersDoNotOverlapWhenOneWalksIntoAnother() {
		var sim = NewSim();
		var walker = PlaceAt(sim, "walker", -3f, 0f);
		var wall = PlaceAt(sim, "wall", 0f, 0f);
		sim.SetInput(walker, new PlayerInput(new Vec2(1f, 0f), false));

		Run(sim, 2f);

		var distance = (wall.Position - walker.Position).Length;
		Assert.True(distance >= (sim.Config.PlayerRadius * 2f) - 0.01f, $"players overlapped, distance {distance}");
	}

	[Fact]
	public void FiringRespectsCooldown() {
		var sim = NewSim();
		var player = PlaceAt(sim, "a", 0f, 0f);
		sim.SetInput(player, new PlayerInput(Vec2.Zero, true));

		sim.Tick(Dt);
		Assert.Single(sim.Bullets);
		Assert.True(player.FireCooldown > 0f);

		// Still inside the 0.5 s window: no second bullet.
		Run(sim, 0.3f);
		Assert.Single(sim.Bullets);

		Run(sim, 0.3f);
		Assert.Equal(2, sim.Bullets.Count);
	}

	[Fact]
	public void BulletFliesTowardsTheLastMovementDirection() {
		var sim = NewSim();
		var player = PlaceAt(sim, "a", 0f, 0f);
		sim.SetInput(player, new PlayerInput(new Vec2(-1f, 0f), false));
		sim.Tick(Dt);
		sim.SetInput(player, new PlayerInput(Vec2.Zero, true));
		sim.Tick(Dt);

		var bullet = Assert.Single(sim.Bullets);
		Assert.True(bullet.Velocity.X < 0f);
		Assert.Equal(0f, bullet.Velocity.Y, 3);
	}

	[Fact]
	public void BulletNeverHitsItsOwner() {
		var sim = NewSim();
		var player = PlaceAt(sim, "a", 0f, 0f);
		sim.SetInput(player, new PlayerInput(Vec2.Zero, true));
		sim.Tick(Dt);
		sim.SetInput(player, new PlayerInput(Vec2.Zero, false));

		Run(sim, 1f);

		Assert.Equal(sim.Config.MaxHealth, player.Health);
	}

	[Fact]
	public void HitDealsOneDamageAndConsumesTheBullet() {
		var sim = NewSim();
		var shooter = PlaceAt(sim, "shooter", 0f, 0f);
		var target = PlaceAt(sim, "target", 0f, 4f);
		shooter.Facing = new Vec2(0f, 1f);
		sim.SetInput(shooter, new PlayerInput(Vec2.Zero, true));
		sim.Tick(Dt);
		sim.SetInput(shooter, new PlayerInput(Vec2.Zero, false));

		Run(sim, 0.4f);

		Assert.Equal(sim.Config.MaxHealth - 1, target.Health);
		Assert.Empty(sim.Bullets);
	}

	[Fact]
	public void FastBulletDoesNotTunnelThroughAPlayer() {
		// One tick moves a bullet further than a player diameter; the swept test must still catch it.
		var config = new GameConfig { BulletSpeed = 200f };
		var sim = new GameSim(config, new Random(1));
		var shooter = PlaceAt(sim, "shooter", 0f, 0f);
		var target = PlaceAt(sim, "target", 0f, 10f);
		shooter.Facing = new Vec2(0f, 1f);
		sim.SetInput(shooter, new PlayerInput(Vec2.Zero, true));

		Run(sim, 0.5f);

		Assert.True(target.Health < config.MaxHealth, "fast bullet passed straight through the target");
	}

	[Fact]
	public void FiveHitsKillAndScoreAKillAndADeath() {
		var sim = NewSim();
		var shooter = PlaceAt(sim, "shooter", 0f, 0f);
		var target = PlaceAt(sim, "target", 0f, 4f);
		shooter.Facing = new Vec2(0f, 1f);
		sim.SetInput(shooter, new PlayerInput(Vec2.Zero, true));

		// Target holds still; keep the shooter in place so every shot lands.
		for ( var i = 0; i < 120; i++ ) {
			shooter.Position = new Vec2(0f, 0f);
			target.Position = new Vec2(0f, 4f);
			sim.Tick(Dt);
			if ( !target.Alive ) {
				break;
			}
		}

		Assert.False(target.Alive);
		Assert.Equal(0, target.Health);
		Assert.Equal(1, target.Deaths);
		Assert.Equal(1, shooter.Kills);
	}

	[Fact]
	public void DeadPlayerRespawnsAfterTheConfiguredDelayWithFullHealth() {
		var sim = NewSim();
		var target = PlaceAt(sim, "target", 0f, 4f);
		Kill(sim, target);

		Assert.False(target.Alive);
		Run(sim, sim.Config.RespawnSeconds - 1f);
		Assert.False(target.Alive);

		Run(sim, 1.2f);
		Assert.True(target.Alive);
		Assert.Equal(sim.Config.MaxHealth, target.Health);
	}

	[Fact]
	public void RespawnPointIsNotOccupiedByAnotherPlayer() {
		var config = new GameConfig { ArenaMinX = -3f, ArenaMaxX = 3f, ArenaMinY = -3f, ArenaMaxY = 3f };
		var sim = new GameSim(config, new Random(7));
		var blockers = new List<SimPlayer>();
		for ( var i = 0; i < 4; i++ ) {
			blockers.Add(sim.AddPlayer($"blocker{i}", ClientKind.Web, "127.0.0.1"));
		}
		var victim = sim.AddPlayer("victim", ClientKind.Web, "127.0.0.1");
		Kill(sim, victim);
		Run(sim, config.RespawnSeconds + 0.2f);

		Assert.True(victim.Alive);
		foreach ( var blocker in blockers ) {
			var distance = (blocker.Position - victim.Position).Length;
			Assert.True(distance >= (config.PlayerRadius * 2f) - 0.01f, $"respawned on top of {blocker.Name}");
		}
	}

	[Fact]
	public void DisconnectedPlayerLeavesTheArenaButKeepsTheirRecord() {
		var sim = NewSim();
		var player = PlaceAt(sim, "a", 0f, 0f);
		player.Kills = 3;
		player.Deaths = 2;

		sim.SetConnected(player, false);
		Run(sim, 10f);

		Assert.False(player.Alive);
		Assert.False(player.Connected);
		Assert.Equal(3, player.Kills);
		Assert.Equal(2, player.Deaths);
		Assert.Contains(sim.Players, p => p.Id == player.Id);
	}

	[Fact]
	public void ReconnectingRestoresTheSameRecord() {
		var sim = NewSim();
		var player = PlaceAt(sim, "a", 0f, 0f);
		player.Kills = 5;
		sim.SetConnected(player, false);

		var found = sim.FindPlayerByName("A");
		Assert.NotNull(found);
		sim.SetConnected(found, true);

		Assert.True(found.Alive);
		Assert.Equal(5, found.Kills);
	}

	[Fact]
	public void ElapsedTimeAdvancesWithTicks() {
		var sim = NewSim();
		Run(sim, 2f);

		Assert.Equal(2d, sim.ElapsedSeconds, 1);
	}

	[Fact]
	public void KillEventNamesBothSides() {
		var sim = NewSim();
		var shooter = PlaceAt(sim, "shooter", 0f, 0f);
		var target = PlaceAt(sim, "target", 0f, 4f);
		var kill = KillAndCaptureEvent(sim, shooter, target);

		Assert.Equal(GameEventKind.Kill, kill.Kind);
		Assert.Equal(target.Id, kill.VictimId);
		Assert.Equal(shooter.Id, kill.KillerId);
	}

	/// <summary>Puts a player into the dead state directly, for tests about what happens afterwards.</summary>
	static void Kill(GameSim sim, SimPlayer victim) {
		victim.Health = 0;
		victim.Alive = false;
		victim.Deaths++;
		victim.RespawnTimer = sim.Config.RespawnSeconds;
	}

	static GameEvent KillAndCaptureEvent(GameSim sim, SimPlayer shooter, SimPlayer target) {
		shooter.Facing = new Vec2(0f, 1f);
		sim.SetInput(shooter, new PlayerInput(Vec2.Zero, true));
		for ( var i = 0; i < 200; i++ ) {
			shooter.Position = new Vec2(0f, 0f);
			if ( target.Alive ) {
				target.Position = new Vec2(0f, 4f);
			}
			sim.Tick(Dt);
			foreach ( var gameEvent in sim.Events ) {
				if ( gameEvent.Kind == GameEventKind.Kill ) {
					return gameEvent;
				}
			}
		}
		throw new InvalidOperationException("target never died");
	}
}
