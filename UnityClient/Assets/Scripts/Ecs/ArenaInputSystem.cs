using MultiplayerDemo.Client.Net;
using Unity.Entities;

namespace MultiplayerDemo.Client.Ecs {
	/// <summary>
	/// Ships <see cref="LocalInputState"/> to the server at a fixed 30 Hz, well under the 60/s the
	/// protocol allows, and clears the fire latch so a single-frame tap is never lost or repeated.
	/// </summary>
	[UpdateInGroup(typeof(ArenaClientSystemGroup), OrderLast = true)]
	public partial class ArenaInputSystem : SystemBase {
		const double SendInterval = 1.0 / 30.0;

		EntityQuery _linkQuery;
		double _nextSendTime;

		protected override void OnCreate() {
			_linkQuery = GetEntityQuery(ComponentType.ReadOnly<ArenaClientLink>());
			RequireForUpdate(_linkQuery);
			RequireForUpdate<LocalInputState>();
		}

		protected override void OnUpdate() {
			var link = EntityManager.GetComponentObject<ArenaClientLink>(_linkQuery.GetSingletonEntity());
			if ( !link.IsPlaying || (link.Connection == null) ) {
				return;
			}
			var now = SystemAPI.Time.ElapsedTime;
			if ( now < _nextSendTime ) {
				return;
			}
			_nextSendTime = now + SendInterval;
			var input = SystemAPI.GetSingleton<LocalInputState>();
			link.Connection.Send(ArenaProtocol.BuildInput(input.Move.x, input.Move.y, input.Fire));
			SystemAPI.SetSingleton(new LocalInputState { Move = input.Move, Fire = false });
		}
	}
}
