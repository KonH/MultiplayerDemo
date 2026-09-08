using System.Collections.Generic;
using MultiplayerDemo.Client.Net;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace MultiplayerDemo.Client.Ecs {
	/// <summary>
	/// Turns the latest authoritative snapshot into entities: spawns what appeared, updates what moved
	/// and deletes what the server has stopped mentioning.
	/// </summary>
	[UpdateInGroup(typeof(ArenaClientSystemGroup))]
	[UpdateAfter(typeof(ArenaNetworkSystem))]
	public partial class ArenaSnapshotSystem : SystemBase {
		/// <summary>Players and bullets sit half a unit up so a 1x1x1 box rests on the floor.</summary>
		public const float ActorHeight = 0.5f;

		readonly Dictionary<int, Entity> _knownPlayers = new();
		readonly Dictionary<int, Entity> _knownBullets = new();

		EntityQuery _linkQuery;

		protected override void OnCreate() {
			_linkQuery = GetEntityQuery(ComponentType.ReadOnly<ArenaClientLink>());
			RequireForUpdate(_linkQuery);
			RequireForUpdate<ArenaConfig>();
		}

		protected override void OnUpdate() {
			var linkEntity = _linkQuery.GetSingletonEntity();
			var link = EntityManager.GetComponentObject<ArenaClientLink>(linkEntity);
			var assets = EntityManager.GetComponentObject<ArenaRenderAssets>(linkEntity);
			var config = SystemAPI.GetSingleton<ArenaConfig>();

			if ( (link.LatestState != null) && !link.StateApplied ) {
				link.StateApplied = true;
				Apply(link.LatestState, link.LatestStateTime, config, assets);
			}
			RemoveExpired(NetInterpolation.RenderTime(SystemAPI.Time.ElapsedTime));
		}

		void Apply(StateMessage state, double time, ArenaConfig config, ArenaRenderAssets assets) {
			IndexExistingEntities();
			foreach ( var player in state.players ) {
				var position = new float3(player.x, ActorHeight, player.y);
				if ( _knownPlayers.TryGetValue(player.id, out var entity) ) {
					var net = EntityManager.GetComponentData<NetTransform>(entity);
					EntityManager.SetComponentData(entity, NetInterpolation.Push(net, position, time));
				} else {
					entity = CreatePlayer(player.id, position, time, config, assets);
					_knownPlayers[player.id] = entity;
				}
				EntityManager.SetComponentData(entity, new Health { Value = player.hp, Max = config.MaxHealth });
				EntityManager.SetComponentData(entity, new FireCooldown { RemainingMs = player.cd });
				EntityManager.SetComponentData(entity, new Facing { Value = new float2(player.dx, player.dy) });
			}
			foreach ( var bullet in state.bullets ) {
				var position = new float3(bullet.x, ActorHeight, bullet.y);
				if ( _knownBullets.TryGetValue(bullet.id, out var entity) ) {
					var net = EntityManager.GetComponentData<NetTransform>(entity);
					EntityManager.SetComponentData(entity, NetInterpolation.Push(net, position, time));
				} else {
					_knownBullets[bullet.id] = CreateBullet(bullet.id, bullet.o, position, time, assets);
				}
			}
		}

		void IndexExistingEntities() {
			_knownPlayers.Clear();
			_knownBullets.Clear();
			foreach ( var (id, entity) in SystemAPI.Query<RefRO<PlayerId>>().WithEntityAccess() ) {
				_knownPlayers[id.ValueRO.Value] = entity;
			}
			foreach ( var (id, entity) in SystemAPI.Query<RefRO<BulletId>>().WithEntityAccess() ) {
				_knownBullets[id.ValueRO.Value] = entity;
			}
		}

		Entity CreatePlayer(int id, float3 position, double time, ArenaConfig config, ArenaRenderAssets assets) {
			var entity = EntityManager.CreateEntity();
			EntityManager.AddComponentData(entity, new PlayerId { Value = id });
			EntityManager.AddComponentData(entity, NetInterpolation.Create(position, time));
			EntityManager.AddComponentData(entity, new Health { Value = config.MaxHealth, Max = config.MaxHealth });
			EntityManager.AddComponentData(entity, new FireCooldown());
			EntityManager.AddComponentData(entity, new Facing { Value = new float2(0f, 1f) });
			var isLocal = id == config.LocalPlayerId;
			if ( isLocal ) {
				EntityManager.AddComponent<LocalPlayerTag>(entity);
			} else {
				EntityManager.AddComponent<RemotePlayerTag>(entity);
			}
			ArenaEntityFactory.AddRendering(
				EntityManager,
				entity,
				assets.Actors,
				isLocal ? ArenaRenderAssets.LocalMaterial : ArenaRenderAssets.RemoteMaterial,
				ArenaRenderAssets.PlayerMesh,
				assets.PlayerBounds,
				position);
			return entity;
		}

		Entity CreateBullet(int id, int owner, float3 position, double time, ArenaRenderAssets assets) {
			var entity = EntityManager.CreateEntity();
			EntityManager.AddComponentData(entity, new BulletId { Value = id });
			EntityManager.AddComponentData(entity, new BulletOwner { Value = owner });
			EntityManager.AddComponent<BulletTag>(entity);
			EntityManager.AddComponentData(entity, NetInterpolation.Create(position, time));
			ArenaEntityFactory.AddRendering(
				EntityManager,
				entity,
				assets.Actors,
				ArenaRenderAssets.BulletMaterial,
				ArenaRenderAssets.BulletMesh,
				assets.BulletBounds,
				position);
			return entity;
		}

		void RemoveExpired(double renderTime) {
			var doomed = new NativeList<Entity>(Allocator.Temp);
			foreach ( var (net, entity) in SystemAPI.Query<RefRO<NetTransform>>().WithEntityAccess() ) {
				if ( NetInterpolation.IsExpired(net.ValueRO, renderTime) ) {
					doomed.Add(entity);
				}
			}
			if ( doomed.Length > 0 ) {
				EntityManager.DestroyEntity(doomed.AsArray());
			}
			doomed.Dispose();
		}
	}
}
