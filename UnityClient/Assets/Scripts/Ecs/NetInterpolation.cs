using Unity.Mathematics;

namespace MultiplayerDemo.Client.Ecs {
	/// <summary>
	/// The whole of the client's "rendering" logic: there is no prediction, so an entity is simply
	/// drawn where the server said it was <see cref="InterpolationDelay"/> seconds ago. Pure static
	/// maths so the EditMode tests can cover it, and Burst-friendly so the system can compile.
	/// </summary>
	public static class NetInterpolation {
		/// <summary>One tick of headroom at 30 Hz plus a little jitter budget.</summary>
		public const double InterpolationDelay = 0.1;

		/// <summary>How far past its last sighting an entity is kept before it is deleted.</summary>
		public const double RemovalGrace = 0.15;

		public static double RenderTime(double now) => now - InterpolationDelay;

		public static NetTransform Create(float3 position, double time) => new() {
			Previous = position,
			Target = position,
			PreviousTime = time,
			TargetTime = time,
			LastSeenTime = time
		};

		/// <summary>Rolls the current target back into the previous slot and takes the new sample.</summary>
		public static NetTransform Push(NetTransform current, float3 position, double time) {
			if ( time <= current.TargetTime ) {
				// An out-of-order or duplicate snapshot: keep the entity alive, ignore the sample.
				current.LastSeenTime = math.max(current.LastSeenTime, time);
				return current;
			}
			return new NetTransform {
				Previous = current.Target,
				Target = position,
				PreviousTime = current.TargetTime,
				TargetTime = time,
				LastSeenTime = time
			};
		}

		public static float3 Sample(NetTransform transform, double renderTime) {
			var span = transform.TargetTime - transform.PreviousTime;
			if ( span <= 1e-6 ) {
				return transform.Target;
			}
			var k = math.clamp((renderTime - transform.PreviousTime) / span, 0.0, 1.0);
			return math.lerp(transform.Previous, transform.Target, (float)k);
		}

		/// <summary>True once the interpolation clock has run past the last snapshot that mentioned it.</summary>
		public static bool IsExpired(NetTransform transform, double renderTime) =>
			renderTime > (transform.LastSeenTime + RemovalGrace);
	}
}
