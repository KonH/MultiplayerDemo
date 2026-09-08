using System.Collections.Generic;
using MultiplayerDemo.Client.Ui;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MultiplayerDemo.Client.Tests {
	public sealed class ArenaSceneUiTests {
		const string ScenePath = "Assets/Scenes/Arena.unity";
		const string VisualTreePath = "Assets/UI/ArenaHud/ArenaHud.uxml";
		const string PanelSettingsPath = "Assets/UI/ArenaHud/ArenaHudPanelSettings.asset";

		[Test]
		public void ArenaSceneHasExactlyOneExplicitlyWiredHudDocument() {
			var scene = SceneManager.GetSceneByPath(ScenePath);
			var openedByTest = !scene.IsValid() || !scene.isLoaded;
			if ( openedByTest ) {
				scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
			}

			try {
				var renderers = ComponentsInScene<PanelRenderer>(scene);
				var documents = ComponentsInScene<ArenaHudDocument>(scene);
				var bootstraps = ComponentsInScene<ArenaClientBootstrap>(scene);

				Assert.That(renderers, Has.Count.EqualTo(1), "Arena must contain one PanelRenderer.");
				Assert.That(documents, Has.Count.EqualTo(1), "Arena must contain one ArenaHudDocument.");
				Assert.That(bootstraps, Has.Count.EqualTo(1), "Arena must contain one bootstrap.");
				Assert.That(renderers[0].gameObject, Is.SameAs(documents[0].gameObject));
				Assert.That(renderers[0].visualTreeAsset,
					Is.SameAs(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(VisualTreePath)));
				Assert.That(renderers[0].panelSettings,
					Is.SameAs(AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath)));

				var bootstrap = new SerializedObject(bootstraps[0]);
				var documentProperty = bootstrap.FindProperty("_hudDocument");
				Assert.NotNull(documentProperty, "Bootstrap must serialize its HUD dependency.");
				Assert.That(documentProperty.objectReferenceValue, Is.SameAs(documents[0]));

				foreach ( var behaviour in ComponentsInScene<MonoBehaviour>(scene) ) {
					Assert.That(behaviour.GetType().Name, Is.Not.EqualTo("ArenaHud"),
						"The retired runtime IMGUI HUD must not remain in the scene.");
				}
			}
			finally {
				if ( openedByTest ) {
					EditorSceneManager.CloseScene(scene, true);
				}
			}
		}

		static List<T> ComponentsInScene<T>(Scene scene) where T : Component {
			var components = new List<T>();
			foreach ( var root in scene.GetRootGameObjects() ) {
				components.AddRange(root.GetComponentsInChildren<T>(true));
			}
			return components;
		}
	}
}
