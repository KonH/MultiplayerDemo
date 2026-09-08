using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
// The system source generator does not carry file-scoped usings into its generated partial, so the
// query below names LocalTransform in full.

namespace MultiplayerDemo.Client.Ecs {
	/// <summary>
	/// Writes <see cref="LocalTransform"/> from the interpolated snapshot pair. There is no client-side
	/// prediction anywhere in this client: this is the only thing that decides where anything is drawn.
	/// </summary>
	[UpdateInGroup(typeof(ArenaClientSystemGroup))]
	[UpdateAfter(typeof(ArenaSnapshotSystem))]
	[BurstCompile]
	public partial struct ArenaInterpolationSystem : ISystem {
		[BurstCompile]
		public void OnCreate(ref SystemState state) {
			state.RequireForUpdate<ArenaConfig>();
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state) {
			var renderTime = NetInterpolation.RenderTime(SystemAPI.Time.ElapsedTime);
			foreach ( var (net, transform) in
				SystemAPI.Query<RefRO<NetTransform>, RefRW<Unity.Transforms.LocalTransform>>() ) {
				transform.ValueRW.Position = NetInterpolation.Sample(net.ValueRO, renderTime);
			}
		}
	}
}
