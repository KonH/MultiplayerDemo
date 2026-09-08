using System;
using System.Linq;
using MultiplayerDemo.Client.Ecs;
using MultiplayerDemo.Client.Ui;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MultiplayerDemo.Client.Tests {
	public sealed class ArenaHudViewTests {
		const string AssetPath = "Assets/UI/ArenaHud/ArenaHud.uxml";
		EditorWindow _window;

		[TearDown]
		public void TearDown() {
			if ( _window != null ) {
				_window.Close();
			}
		}

		[Test]
		public void RefreshUpdatesVisibilityAndTextWithoutEmittingFieldChanges() {
			string changedName = null;
			string changedAddress = null;
			var nameChanges = 0;
			var addressChanges = 0;
			var view = CreateView(
				value => {
					changedName = value;
					nameChanges++;
				},
				value => {
					changedAddress = value;
					addressChanges++;
				});
			var state = Project(
				"Pilot", "arena.test:8080", ArenaClientStatus.Playing,
				new ArenaGameplayPresentationInput(true, 7, 10, 0, 0, 65_000, 2, 3, 40));

			view.Refresh(state);

			Assert.AreEqual(DisplayStyle.None, view.ConnectRoot.style.display.value);
			Assert.AreEqual(DisplayStyle.Flex, view.HudRoot.style.display.value);
			Assert.AreEqual("Pilot", view.PlayerNameField.value);
			Assert.AreEqual("arena.test:8080", view.AddressField.value);
			Assert.AreEqual("Pilot", view.Root.Q<Label>("hud-player-name-label").text);
			Assert.AreEqual("HP  7 / 10", view.Root.Q<Label>("health-label").text);
			Assert.AreEqual("Fire  ready", view.Root.Q<Label>("fire-label").text);
			Assert.AreEqual("Time  00:01:05", view.Root.Q<Label>("time-label").text);
			Assert.AreEqual("players 2   bullets 3   snapshots 40", view.Root.Q<Label>("debug-label").text);
			Assert.AreEqual(0, nameChanges);
			Assert.AreEqual(0, addressChanges);

			SendChange(view.PlayerNameField, "Edited pilot");
			SendChange(view.AddressField, "edited.test:9090");
			Assert.AreEqual(1, nameChanges);
			Assert.AreEqual(1, addressChanges);
			Assert.AreEqual("Edited pilot", changedName);
			Assert.AreEqual("edited.test:9090", changedAddress);

			view.Refresh(Project("Edited pilot", "edited.test:9090", ArenaClientStatus.Disconnected));
			Assert.AreEqual("Edited pilot", view.PlayerNameField.value);
			Assert.AreEqual("edited.test:9090", view.AddressField.value);
			Assert.AreEqual(1, nameChanges);
			Assert.AreEqual(1, addressChanges);
		}

		static void SendChange(TextField field, string value) {
			field.value = value;
		}

		[Test]
		public void DiscoveryRowsShowEmptyStateAndCarrySelectionAddresses() {
			var view = CreateView();
			view.Refresh(Project("Pilot", "localhost:8080", ArenaClientStatus.Disconnected));

			var serverList = view.Root.Q<VisualElement>("server-list");
			Assert.AreEqual(1, serverList.childCount);
			Assert.AreEqual("None yet - type an address above.", ((Label)serverList[0]).text);
			Assert.IsTrue(serverList[0].ClassListContains("server-list-empty"));

			var servers = new[] {
				new ArenaDiscoveryPresentationInput("Alpha", "10.0.0.1:8080", 2),
				new ArenaDiscoveryPresentationInput("Beta", "10.0.0.2:8080", 4)
			};
			view.Refresh(Project("Pilot", "localhost:8080", ArenaClientStatus.Disconnected, servers: servers));

			var buttons = serverList.Children().Cast<Button>().ToArray();
			Assert.AreEqual(2, buttons.Length);
			Assert.AreEqual("Alpha  10.0.0.1:8080  (2 players)", buttons[0].text);
			Assert.AreEqual("10.0.0.1:8080", buttons[0].userData);
			Assert.AreEqual("10.0.0.2:8080", buttons[1].userData);
			Assert.IsTrue(buttons.All(button => button.ClassListContains("server-list-button")));

			view.Refresh(Project("Pilot", "localhost:8080", ArenaClientStatus.Disconnected));
			Assert.AreEqual(1, serverList.childCount);
			Assert.IsInstanceOf<Label>(serverList[0]);
		}

		[Test]
		public void LeaderboardRefreshReplacesStaleRowsAndRendersSevenCells() {
			var view = CreateView();
			var initialRoster = new[] {
				new ArenaRosterPresentationInput(7, "Pilot", 5, 2, true, "127.0.0.1", "unity", 18),
				new ArenaRosterPresentationInput(9, "Bot", 1, 4, false, "10.0.0.9", "web", 44)
			};
			view.Refresh(Project(
				"Pilot", "localhost:8080", ArenaClientStatus.Playing,
				leaderboardVisible: true, localPlayerId: 7, roster: initialRoster));

			var rows = view.Root.Q<ScrollView>("leaderboard-rows").contentContainer;
			Assert.AreEqual(DisplayStyle.Flex, view.LeaderboardRoot.style.display.value);
			Assert.AreEqual(2, rows.childCount);
			Assert.AreEqual(7, rows[0].childCount);
			Assert.AreEqual("Pilot (you)", ((Label)rows[0][0]).text);
			Assert.AreEqual("5", ((Label)rows[0][1]).text);
			Assert.AreEqual("2", ((Label)rows[0][2]).text);
			Assert.AreEqual("connected", ((Label)rows[0][3]).text);
			Assert.AreEqual("127.0.0.1", ((Label)rows[0][4]).text);
			Assert.AreEqual("unity", ((Label)rows[0][5]).text);
			Assert.AreEqual("18 ms", ((Label)rows[0][6]).text);
			Assert.IsTrue(rows[1].ClassListContains("leaderboard-row--alternate"));

			var replacement = new[] {
				new ArenaRosterPresentationInput(11, "New", 0, 0, true, "::1", "unity", 3)
			};
			view.Refresh(Project(
				"Pilot", "localhost:8080", ArenaClientStatus.Playing,
				leaderboardVisible: true, roster: replacement));

			Assert.AreEqual(1, rows.childCount);
			Assert.AreEqual("New", ((Label)rows[0][0]).text);
			Assert.AreEqual(7, rows[0].Query<Label>().ToList().Count);
		}

		[Test]
		public void ConstructorExposesControlsAndOnlyAuthoredBlockingPanels() {
			var view = CreateView();

			Assert.NotNull(view.ConnectButton);
			Assert.NotNull(view.RefreshButton);
			Assert.NotNull(view.PlayerNameField);
			Assert.NotNull(view.AddressField);
			CollectionAssert.AreEqual(
				new[] { "connect-panel", "hud-panel", "leaderboard-panel" },
				view.BlockingPanels.Select(panel => panel.name).ToArray());
			Assert.Throws<InvalidOperationException>(() => new ArenaHudView(new VisualElement()));
		}

		[Test]
		public void RefreshHidesEmptyHudValuesAndMarksErrorStatus() {
			var view = CreateView();
			view.Refresh(Project("Pilot", "localhost:8080", ArenaClientStatus.Failed));

			var health = view.Root.Q<Label>("health-label");
			var fire = view.Root.Q<Label>("fire-label");
			var lifeStatus = view.Root.Q<Label>("life-status-label");
			var respawn = view.Root.Q<Label>("respawn-label");
			var status = view.Root.Q<Label>("connect-status-label");
			Assert.AreEqual(DisplayStyle.None, health.style.display.value);
			Assert.AreEqual(DisplayStyle.None, fire.style.display.value);
			Assert.AreEqual(DisplayStyle.Flex, lifeStatus.style.display.value);
			Assert.AreEqual(DisplayStyle.Flex, respawn.style.display.value);
			Assert.IsTrue(status.ClassListContains("arena-error-label"));

			view.Refresh(Project(
				"Pilot", "localhost:8080", ArenaClientStatus.Playing,
				new ArenaGameplayPresentationInput(true, 10, 10, 0, 0, 0, 1, 0, 1)));
			Assert.AreEqual(DisplayStyle.Flex, health.style.display.value);
			Assert.AreEqual(DisplayStyle.Flex, fire.style.display.value);
			Assert.AreEqual(DisplayStyle.None, lifeStatus.style.display.value);
			Assert.AreEqual(DisplayStyle.None, respawn.style.display.value);
			Assert.IsFalse(status.ClassListContains("arena-error-label"));
		}

		ArenaHudView CreateView(Action<string> onNameChanged = null, Action<string> onAddressChanged = null) {
			var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AssetPath);
			Assert.NotNull(asset, $"Missing UI asset at {AssetPath}");
			var root = asset.CloneTree();
			if ( (onNameChanged != null) || (onAddressChanged != null) ) {
				_window = ScriptableObject.CreateInstance<EditorWindow>();
				_window.Show();
				_window.rootVisualElement.Add(root);
			}
			return new ArenaHudView(
				root,
				onNameChanged,
				onAddressChanged,
				_ => { },
				() => { },
				() => { });
		}

		static ArenaHudPresentationState Project(
			string playerName,
			string address,
			ArenaClientStatus status,
			ArenaGameplayPresentationInput? gameplay = null,
			ArenaDiscoveryPresentationInput[] servers = null,
			bool leaderboardVisible = false,
			int? localPlayerId = null,
			ArenaRosterPresentationInput[] roster = null) {
			return ArenaHudProjector.Project(new ArenaHudPresentationInput(
				new ArenaConnectionPresentationInput(
					playerName, address, status, "", "LAN servers", servers ?? Array.Empty<ArenaDiscoveryPresentationInput>()),
				gameplay ?? new ArenaGameplayPresentationInput(false, 0, 0, 0, 0, 0, 0, 0, 0),
				new ArenaLeaderboardPresentationInput(
					leaderboardVisible, localPlayerId, roster ?? Array.Empty<ArenaRosterPresentationInput>())));
		}
	}
}
