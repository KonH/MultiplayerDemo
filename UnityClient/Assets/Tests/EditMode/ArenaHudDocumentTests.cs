using MultiplayerDemo.Client.Ui;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace MultiplayerDemo.Client.Tests {
	public sealed class ArenaHudDocumentTests {
		GameObject _gameObject;

		[TearDown]
		public void TearDown() {
			if ( _gameObject != null ) {
				Object.DestroyImmediate(_gameObject);
			}
		}

		[Test]
		public void AddingDocumentAddsRequiredPanelRenderer() {
			_gameObject = new GameObject("Arena HUD document test");

			var document = _gameObject.AddComponent<ArenaHudDocument>();

			Assert.NotNull(document);
			Assert.NotNull(_gameObject.GetComponent<PanelRenderer>());
		}

		[Test]
		public void PointerQueryReturnsFalseBeforePanelRootIsAvailable() {
			_gameObject = new GameObject("Arena HUD document test");
			var document = _gameObject.AddComponent<ArenaHudDocument>();

			Assert.IsFalse(document.IsPointerOverBlockingUi());
		}

		[Test]
		public void DestroyAfterAwakeUnregistersSafely() {
			_gameObject = new GameObject("Arena HUD document test");
			_gameObject.AddComponent<ArenaHudDocument>();

			Assert.DoesNotThrow(() => Object.DestroyImmediate(_gameObject));
			_gameObject = null;
		}
	}
}
