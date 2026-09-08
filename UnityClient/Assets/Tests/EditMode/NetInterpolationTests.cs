using MultiplayerDemo.Client.Ecs;
using NUnit.Framework;
using Unity.Mathematics;

namespace MultiplayerDemo.Client.Tests {
	public sealed class NetInterpolationTests {
		[Test]
		public void RenderTime_LagsByTheInterpolationDelay() {
			Assert.AreEqual(9.9, NetInterpolation.RenderTime(10.0), 1e-9);
			Assert.AreEqual(0.1, NetInterpolation.InterpolationDelay, 1e-9);
		}

		[Test]
		public void AnEntityThatJustAppearedSitsExactlyWhereTheServerPutIt() {
			// Appearing: one snapshot only, so there is nothing to interpolate between.
			var net = NetInterpolation.Create(new float3(4f, 0.5f, -2f), 10.0);
			var sampled = NetInterpolation.Sample(net, NetInterpolation.RenderTime(10.0));
			Assert.AreEqual(4f, sampled.x, 1e-4f);
			Assert.AreEqual(0.5f, sampled.y, 1e-4f);
			Assert.AreEqual(-2f, sampled.z, 1e-4f);
		}

		[Test]
		public void SampleInterpolatesBetweenTwoSnapshots() {
			var net = NetInterpolation.Create(new float3(0f, 0.5f, 0f), 10.0);
			net = NetInterpolation.Push(net, new float3(10f, 0.5f, 0f), 11.0);

			Assert.AreEqual(0f, NetInterpolation.Sample(net, 10.0).x, 1e-4f);
			Assert.AreEqual(5f, NetInterpolation.Sample(net, 10.5).x, 1e-4f);
			Assert.AreEqual(10f, NetInterpolation.Sample(net, 11.0).x, 1e-4f);
		}

		[Test]
		public void SampleClampsInsteadOfExtrapolating() {
			var net = NetInterpolation.Create(new float3(0f, 0.5f, 0f), 10.0);
			net = NetInterpolation.Push(net, new float3(10f, 0.5f, 0f), 11.0);

			Assert.AreEqual(0f, NetInterpolation.Sample(net, 9.0).x, 1e-4f);
			// No client-side prediction: past the last snapshot the entity simply stops.
			Assert.AreEqual(10f, NetInterpolation.Sample(net, 99.0).x, 1e-4f);
		}

		[Test]
		public void PushRollsTheTargetIntoThePreviousSlot() {
			var net = NetInterpolation.Create(new float3(0f, 0.5f, 0f), 10.0);
			net = NetInterpolation.Push(net, new float3(1f, 0.5f, 0f), 11.0);
			net = NetInterpolation.Push(net, new float3(2f, 0.5f, 0f), 12.0);

			Assert.AreEqual(1f, net.Previous.x, 1e-4f);
			Assert.AreEqual(2f, net.Target.x, 1e-4f);
			Assert.AreEqual(11.0, net.PreviousTime, 1e-9);
			Assert.AreEqual(12.0, net.TargetTime, 1e-9);
			Assert.AreEqual(1.5f, NetInterpolation.Sample(net, 11.5).x, 1e-4f);
		}

		[Test]
		public void PushIgnoresAnOutOfOrderSnapshotButKeepsTheEntityAlive() {
			var net = NetInterpolation.Create(new float3(0f, 0.5f, 0f), 10.0);
			net = NetInterpolation.Push(net, new float3(5f, 0.5f, 0f), 11.0);
			var stale = NetInterpolation.Push(net, new float3(-99f, 0.5f, 0f), 10.5);

			Assert.AreEqual(5f, stale.Target.x, 1e-4f);
			Assert.AreEqual(11.0, stale.TargetTime, 1e-9);
			Assert.AreEqual(11.0, stale.LastSeenTime, 1e-9);
		}

		[Test]
		public void AnEntityStillInTheSnapshotIsNeverExpired() {
			var net = NetInterpolation.Create(new float3(0f, 0.5f, 0f), 10.0);
			net = NetInterpolation.Push(net, new float3(1f, 0.5f, 0f), 11.0);
			Assert.IsFalse(NetInterpolation.IsExpired(net, NetInterpolation.RenderTime(11.0)));
		}

		[Test]
		public void AnEntityThatDisappearedExpiresOnceInterpolationRunsPastIt() {
			// Disappearing: the server stopped listing it (killed, or the bullet was consumed).
			var net = NetInterpolation.Create(new float3(0f, 0.5f, 0f), 10.0);
			net = NetInterpolation.Push(net, new float3(1f, 0.5f, 0f), 11.0);

			Assert.IsFalse(NetInterpolation.IsExpired(net, NetInterpolation.RenderTime(11.2)));
			Assert.IsTrue(NetInterpolation.IsExpired(net, NetInterpolation.RenderTime(11.5)));
		}
	}
}
