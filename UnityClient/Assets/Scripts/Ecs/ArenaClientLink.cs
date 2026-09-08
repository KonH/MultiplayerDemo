using System.Collections.Generic;
using MultiplayerDemo.Client.Net;
using Unity.Entities;

namespace MultiplayerDemo.Client.Ecs {
	public enum ArenaClientStatus {
		Disconnected,
		Connecting,
		Joining,
		Playing,
		Rejected,
		Failed
	}

	/// <summary>
	/// The seam between the composition root and the ECS world: the bootstrap MonoBehaviour builds the
	/// connection, hangs it on this managed component and adds it to the world's singleton entity.
	/// Nothing in the client reaches for a static or a scene lookup to find it.
	/// </summary>
	public sealed class ArenaClientLink : IComponentData {
		public IArenaConnection Connection;
		public string PlayerName = string.Empty;
		public ArenaClientStatus Status = ArenaClientStatus.Disconnected;
		public string Message = string.Empty;

		public WelcomeMessage Welcome;
		public StateMessage LatestState;
		public double LatestStateTime;
		public bool StateApplied;

		public readonly List<RosterPlayerMessage> Roster = new();
		public int RosterRevision;

		public bool JoinSent;
		public int SnapshotsReceived;
		public int EventsReceived;

		public bool IsPlaying => Status == ArenaClientStatus.Playing;

		/// <summary>Respawn countdown for the local player, straight from the roster.</summary>
		public int LocalRespawnInMs {
			get {
				if ( Welcome == null ) {
					return 0;
				}
				foreach ( var player in Roster ) {
					if ( player.id == Welcome.id ) {
						return player.respawnIn;
					}
				}
				return 0;
			}
		}
	}
}
