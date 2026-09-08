using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MultiplayerDemo.Client.Ui {
	/// <summary>
	/// Keyboard and mouse glue. The project's Active Input Handling is "Input System Package (New)",
	/// so the legacy <c>UnityEngine.Input</c> API would throw at runtime; everything goes through
	/// <see cref="Keyboard"/> and <see cref="Mouse"/> instead.
	/// </summary>
	public static class ArenaInput {
		/// <summary>
		/// WASD/arrows to the protocol's `mx`,`my`. Unity `+z` is the server's `+y`, and diagonals are
		/// normalised so a diagonal walk is not faster than a straight one.
		/// </summary>
		public static float2 BuildMove(bool up, bool down, bool left, bool right) {
			var x = (right ? 1f : 0f) - (left ? 1f : 0f);
			var y = (up ? 1f : 0f) - (down ? 1f : 0f);
			var move = new float2(x, y);
			var lengthSquared = math.lengthsq(move);
			return (lengthSquared > 1f) ? math.normalize(move) : move;
		}

		public static float2 ReadMove() {
			var keyboard = Keyboard.current;
			if ( keyboard == null ) {
				return float2.zero;
			}
			return BuildMove(
				keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed,
				keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed,
				keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed,
				keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed);
		}

		/// <summary>
		/// Keeps keyboard firing independent from UI focus while preventing a pointer click on a visible
		/// blocking UI region from also firing in the arena.
		/// </summary>
		public static bool ShouldFire(bool spacePressed, bool leftButtonPressed, bool pointerOverBlockingUi) {
			return spacePressed || (leftButtonPressed && !pointerOverBlockingUi);
		}

		/// <summary>Converts Input System screen coordinates into UI Toolkit's top-left origin.</summary>
		public static Vector2 BuildPanelScreenPosition(Vector2 screenPosition, float screenHeight) {
			return new Vector2(screenPosition.x, screenHeight - screenPosition.y);
		}

		public static bool ReadFire(bool pointerOverBlockingUi = false) {
			var keyboard = Keyboard.current;
			var mouse = Mouse.current;
			var space = (keyboard != null) && keyboard.spaceKey.isPressed;
			var leftButton = (mouse != null) && mouse.leftButton.isPressed;
			return ShouldFire(space, leftButton, pointerOverBlockingUi);
		}

		public static bool ReadLeaderboardToggle() {
			var keyboard = Keyboard.current;
			return (keyboard != null) && keyboard.tabKey.wasPressedThisFrame;
		}
	}
}
