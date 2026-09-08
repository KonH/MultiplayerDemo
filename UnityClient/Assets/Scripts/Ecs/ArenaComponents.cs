using Unity.Entities;
using Unity.Mathematics;

namespace MultiplayerDemo.Client.Ecs {
	/// <summary>Everything the server told us about the arena in its welcome message.</summary>
	public struct ArenaConfig : IComponentData {
		public float MinX;
		public float MinY;
		public float MaxX;
		public float MaxY;
		public float PlayerRadius;
		public float BulletRadius;
		public float MoveSpeed;
		public int MaxHealth;
		public int RespawnMs;
		public int FireCooldownMs;
		public int TickRate;
		public int LocalPlayerId;

		public float Width => MaxX - MinX;
		public float Depth => MaxY - MinY;
	}

	/// <summary>Authoritative clock, straight from the latest snapshot.</summary>
	public struct ArenaClock : IComponentData {
		public int Tick;
		public long ElapsedMs;
	}

	/// <summary>
	/// The two snapshots an entity is being interpolated between, plus the last time the server
	/// mentioned it at all — which is how we know when to delete it.
	/// </summary>
	public struct NetTransform : IComponentData {
		public float3 Previous;
		public float3 Target;
		public double PreviousTime;
		public double TargetTime;
		public double LastSeenTime;
	}

	public struct PlayerId : IComponentData {
		public int Value;
	}

	public struct BulletId : IComponentData {
		public int Value;
	}

	public struct BulletOwner : IComponentData {
		public int Value;
	}

	public struct Health : IComponentData {
		public int Value;
		public int Max;
	}

	public struct FireCooldown : IComponentData {
		public int RemainingMs;
	}

	public struct Facing : IComponentData {
		public float2 Value;
	}

	public struct LocalPlayerTag : IComponentData { }

	public struct RemotePlayerTag : IComponentData { }

	public struct BulletTag : IComponentData { }

	public struct ArenaFloorTag : IComponentData { }

	/// <summary>Written every frame by the input glue, read by <c>ArenaInputSystem</c>.</summary>
	public struct LocalInputState : IComponentData {
		public float2 Move;
		public bool Fire;
	}
}
