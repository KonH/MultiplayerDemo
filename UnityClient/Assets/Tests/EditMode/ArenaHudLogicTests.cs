using MultiplayerDemo.Client.Ui;
using NUnit.Framework;
using Unity.Mathematics;

namespace MultiplayerDemo.Client.Tests {
	public sealed class ArenaHudLogicTests {
		[Test]
		public void ElapsedFormatsAsHoursMinutesSeconds() {
			Assert.AreEqual("00:00:00", ArenaFormat.Elapsed(0));
			Assert.AreEqual("00:00:41", ArenaFormat.Elapsed(41_000));
			Assert.AreEqual("00:01:00", ArenaFormat.Elapsed(60_000));
			Assert.AreEqual("01:00:00", ArenaFormat.Elapsed(3_600_000));
			Assert.AreEqual("12:34:56", ArenaFormat.Elapsed(((12 * 3600) + (34 * 60) + 56) * 1000L));
			Assert.AreEqual("100:00:00", ArenaFormat.Elapsed(100L * 3_600_000L));
		}

		[Test]
		public void ElapsedClampsNegativeInput() {
			Assert.AreEqual("00:00:00", ArenaFormat.Elapsed(-5_000));
		}

		[Test]
		public void SecondsFormatsCountdowns() {
			Assert.AreEqual("0.5 s", ArenaFormat.Seconds(500));
			Assert.AreEqual("3.2 s", ArenaFormat.Seconds(3_200));
			Assert.AreEqual("0.0 s", ArenaFormat.Seconds(-10));
		}

		[Test]
		public void BuildMoveMapsKeysToTheProtocolVector() {
			Assert.AreEqual(new float2(0f, 0f), ArenaInput.BuildMove(false, false, false, false));
			Assert.AreEqual(new float2(0f, 1f), ArenaInput.BuildMove(true, false, false, false));
			Assert.AreEqual(new float2(0f, -1f), ArenaInput.BuildMove(false, true, false, false));
			Assert.AreEqual(new float2(-1f, 0f), ArenaInput.BuildMove(false, false, true, false));
			Assert.AreEqual(new float2(1f, 0f), ArenaInput.BuildMove(false, false, false, true));
		}

		[Test]
		public void OppositeKeysCancelOut() {
			Assert.AreEqual(new float2(0f, 0f), ArenaInput.BuildMove(true, true, true, true));
			Assert.AreEqual(new float2(0f, 0f), ArenaInput.BuildMove(true, true, false, false));
		}

		[Test]
		public void DiagonalsAreNormalised() {
			var move = ArenaInput.BuildMove(true, false, false, true);
			Assert.AreEqual(0.70710678f, move.x, 1e-5f);
			Assert.AreEqual(0.70710678f, move.y, 1e-5f);
			Assert.AreEqual(1f, math.length(move), 1e-5f);
		}
	}
}
