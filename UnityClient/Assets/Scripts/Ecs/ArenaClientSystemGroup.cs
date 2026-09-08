using Unity.Entities;

namespace MultiplayerDemo.Client.Ecs {
	/// <summary>
	/// Explicit home for every client system, so the order network -> snapshot -> interpolate -> input
	/// is declared once instead of being inferred from attributes scattered over the world.
	/// </summary>
	[UpdateInGroup(typeof(SimulationSystemGroup))]
	public partial class ArenaClientSystemGroup : ComponentSystemGroup { }
}
