namespace MultiplayerDemo.Core;

/// <summary>Every tunable rule of the arena. Shipped to clients inside the welcome message.</summary>
public sealed class GameConfig {
	public float ArenaMinX { get; init; } = -20f;
	public float ArenaMinY { get; init; } = -20f;
	public float ArenaMaxX { get; init; } = 20f;
	public float ArenaMaxY { get; init; } = 20f;

	public float PlayerRadius { get; init; } = 0.5f;
	public float MoveSpeed { get; init; } = 6f;

	public float BulletRadius { get; init; } = 0.15f;
	public float BulletSpeed { get; init; } = 20f;
	public float BulletLifeSeconds { get; init; } = 2f;

	public int MaxHealth { get; init; } = 5;
	public int BulletDamage { get; init; } = 1;
	public float FireCooldownSeconds { get; init; } = 0.5f;
	public float RespawnSeconds { get; init; } = 5f;

	public int TickRate { get; init; } = 30;
	public int MaxPlayers { get; init; } = 32;
}
