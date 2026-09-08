using MultiplayerDemo.Client.Net;
using Unity.Entities;
using Unity.Mathematics;

namespace MultiplayerDemo.Client.Ecs {
	/// <summary>
	/// The only system that touches the socket. It drains the connection's queue, turns frames into
	/// ECS state and answers the server's pings. Managed on purpose: JSON and sockets are not Burst work.
	/// </summary>
	[UpdateInGroup(typeof(ArenaClientSystemGroup), OrderFirst = true)]
	public partial class ArenaNetworkSystem : SystemBase {
		EntityQuery _linkQuery;
		EntityQuery _floorQuery;

		protected override void OnCreate() {
			_linkQuery = GetEntityQuery(ComponentType.ReadOnly<ArenaClientLink>());
			_floorQuery = GetEntityQuery(ComponentType.ReadOnly<ArenaFloorTag>());
			RequireForUpdate(_linkQuery);
		}

		protected override void OnUpdate() {
			var entity = _linkQuery.GetSingletonEntity();
			var link = EntityManager.GetComponentObject<ArenaClientLink>(entity);
			var connection = link.Connection;
			if ( connection == null ) {
				return;
			}

			TrackConnectionState(link, connection);
			while ( connection.TryDequeue(out var json) ) {
				Handle(entity, link, json);
			}
		}

		static void TrackConnectionState(ArenaClientLink link, IArenaConnection connection) {
			switch ( connection.State ) {
				case ArenaConnectionState.Connecting:
					if ( link.Status == ArenaClientStatus.Disconnected ) {
						link.Status = ArenaClientStatus.Connecting;
					}
					break;
				case ArenaConnectionState.Open:
					if ( !link.JoinSent ) {
						connection.Send(ArenaProtocol.BuildJoin(link.PlayerName));
						link.JoinSent = true;
						link.Status = ArenaClientStatus.Joining;
					}
					break;
				case ArenaConnectionState.Failed:
					link.Status = ArenaClientStatus.Failed;
					link.Message = connection.Error;
					break;
				case ArenaConnectionState.Closed:
					if ( (link.Status != ArenaClientStatus.Rejected) && (link.Status != ArenaClientStatus.Failed) ) {
						link.Status = ArenaClientStatus.Disconnected;
						link.Message = "Connection closed";
					}
					break;
			}
		}

		void Handle(Entity entity, ArenaClientLink link, string json) {
			switch ( ArenaProtocol.ReadType(json) ) {
				case ArenaProtocol.TypeWelcome:
					HandleWelcome(entity, link, json);
					break;
				case ArenaProtocol.TypeReject:
					var reject = ArenaProtocol.ParseReject(json);
					link.Status = ArenaClientStatus.Rejected;
					link.Message = reject?.reason ?? "Rejected";
					link.Connection.Close();
					break;
				case ArenaProtocol.TypeState:
					var state = ArenaProtocol.ParseState(json);
					if ( state == null ) {
						break;
					}
					link.LatestState = state;
					link.LatestStateTime = SystemAPI.Time.ElapsedTime;
					link.StateApplied = false;
					link.SnapshotsReceived++;
					ArenaEntityFactory.SetOrAdd(EntityManager, entity, new ArenaClock {
						Tick = state.tick,
						ElapsedMs = state.elapsedMs
					});
					break;
				case ArenaProtocol.TypeRoster:
					var roster = ArenaProtocol.ParseRoster(json);
					if ( roster == null ) {
						break;
					}
					link.Roster.Clear();
					link.Roster.AddRange(roster.players);
					link.RosterRevision++;
					break;
				case ArenaProtocol.TypePing:
					var ping = ArenaProtocol.ParsePing(json);
					if ( ping != null ) {
						link.Connection.Send(ArenaProtocol.BuildPong(ping.id));
					}
					break;
				case ArenaProtocol.TypeEvent:
					if ( ArenaProtocol.ParseEvent(json) != null ) {
						link.EventsReceived++;
					}
					break;
			}
		}

		void HandleWelcome(Entity entity, ArenaClientLink link, string json) {
			var welcome = ArenaProtocol.ParseWelcome(json);
			if ( welcome == null ) {
				return;
			}
			link.Welcome = welcome;
			link.Status = ArenaClientStatus.Playing;
			link.Message = string.Empty;
			var config = new ArenaConfig {
				MinX = welcome.arena.minX,
				MinY = welcome.arena.minY,
				MaxX = welcome.arena.maxX,
				MaxY = welcome.arena.maxY,
				PlayerRadius = welcome.playerRadius,
				BulletRadius = welcome.bulletRadius,
				MoveSpeed = welcome.moveSpeed,
				MaxHealth = welcome.maxHealth,
				RespawnMs = welcome.respawnMs,
				FireCooldownMs = welcome.fireCooldownMs,
				TickRate = welcome.tickRate,
				LocalPlayerId = welcome.id
			};
			ArenaEntityFactory.SetOrAdd(EntityManager, entity, config);
			ArenaEntityFactory.SetOrAdd(EntityManager, entity, new ArenaClock());
			CreateFloor(entity, config);
		}

		void CreateFloor(Entity linkEntity, ArenaConfig config) {
			EntityManager.DestroyEntity(_floorQuery);
			var assets = EntityManager.GetComponentObject<ArenaRenderAssets>(linkEntity);
			assets.EnsureFloor(config.Width, config.Depth);
			var floor = EntityManager.CreateEntity();
			EntityManager.AddComponent<ArenaFloorTag>(floor);
			// The floor slab is 0.5 thick and sits just below zero, so a 1x1x1 player rests on top of it.
			var position = new float3((config.MinX + config.MaxX) * 0.5f, -0.25f, (config.MinY + config.MaxY) * 0.5f);
			ArenaEntityFactory.AddRendering(
				EntityManager, floor, assets.Floor, 0, 0, assets.FloorBounds, position);
		}
	}
}
