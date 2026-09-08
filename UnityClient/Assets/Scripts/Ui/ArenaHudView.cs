using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MultiplayerDemo.Client.Ui {
	/// <summary>Plain UI Toolkit view: given projected state, update the authored visual tree.</summary>
	public sealed class ArenaHudView {
		const string ErrorClass = "arena-error-label";

		readonly Label _discoveryDescription;
		readonly VisualElement _serverList;
		readonly Label _connectStatus;
		readonly Label _hudPlayerName;
		readonly Label _health;
		readonly Label _fire;
		readonly Label _lifeStatus;
		readonly Label _respawn;
		readonly Label _time;
		readonly Label _debug;
		readonly ScrollView _leaderboardRows;
		readonly Action<string> _serverSelected;

		public VisualElement Root { get; }
		public VisualElement ConnectRoot { get; }
		public VisualElement HudRoot { get; }
		public VisualElement LeaderboardRoot { get; }
		public TextField PlayerNameField { get; }
		public TextField AddressField { get; }
		public Button ConnectButton { get; }
		public Button RefreshButton { get; }
		public IReadOnlyList<VisualElement> BlockingPanels { get; }

		public ArenaHudView(
			VisualElement root,
			Action<string> playerNameChanged = null,
			Action<string> addressChanged = null,
			Action<string> serverSelected = null,
			Action connect = null,
			Action refresh = null) {
			Root = root ?? throw new ArgumentNullException(nameof(root));
			ConnectRoot = Require<VisualElement>(root, "connect-root");
			HudRoot = Require<VisualElement>(root, "hud-root");
			LeaderboardRoot = Require<VisualElement>(root, "leaderboard-root");
			var connectPanel = Require<VisualElement>(root, "connect-panel");
			var hudPanel = Require<VisualElement>(root, "hud-panel");
			var leaderboardPanel = Require<VisualElement>(root, "leaderboard-panel");
			BlockingPanels = new[] { connectPanel, hudPanel, leaderboardPanel };

			PlayerNameField = Require<TextField>(root, "player-name-field");
			AddressField = Require<TextField>(root, "address-field");
			ConnectButton = Require<Button>(root, "connect-button");
			RefreshButton = Require<Button>(root, "refresh-button");
			_discoveryDescription = Require<Label>(root, "discovery-description-label");
			_serverList = Require<VisualElement>(root, "server-list");
			_connectStatus = Require<Label>(root, "connect-status-label");
			_hudPlayerName = Require<Label>(root, "hud-player-name-label");
			_health = Require<Label>(root, "health-label");
			_fire = Require<Label>(root, "fire-label");
			_lifeStatus = Require<Label>(root, "life-status-label");
			_respawn = Require<Label>(root, "respawn-label");
			_time = Require<Label>(root, "time-label");
			_debug = Require<Label>(root, "debug-label");
			_leaderboardRows = Require<ScrollView>(root, "leaderboard-rows");
			_serverSelected = serverSelected;

			PlayerNameField.RegisterValueChangedCallback(evt => playerNameChanged?.Invoke(evt.newValue));
			AddressField.RegisterValueChangedCallback(evt => addressChanged?.Invoke(evt.newValue));
			ConnectButton.OnClick(connect);
			RefreshButton.OnClick(refresh);
		}

		public void Refresh(ArenaHudPresentationState state) {
			if ( state == null ) {
				throw new ArgumentNullException(nameof(state));
			}

			SetDisplayed(ConnectRoot, state.Connect.Visible);
			SetDisplayed(HudRoot, state.Hud.Visible);
			SetDisplayed(LeaderboardRoot, state.Leaderboard.Visible);
			PlayerNameField.SetValueWithoutNotify(state.Connect.PlayerName);
			AddressField.SetValueWithoutNotify(state.Connect.Address);
			_discoveryDescription.text = $"Discovered servers  ({state.Connect.DiscoveryDescription})";
			_connectStatus.text = state.Connect.StatusText;
			_connectStatus.EnableInClassList(ErrorClass, state.Connect.IsStatusError);
			RefreshServers(state.Connect.Servers);

			_hudPlayerName.text = state.Hud.PlayerName;
			SetLabel(_health, state.Hud.HealthText);
			SetLabel(_fire, state.Hud.FireText);
			SetLabel(_lifeStatus, state.Hud.LifeStatusText);
			SetLabel(_respawn, state.Hud.RespawnText);
			_time.text = state.Hud.TimeText;
			_debug.text = state.Hud.DebugText;
			RefreshLeaderboard(state.Leaderboard.Rows);
		}

		/// <summary>Checks only the three authored panels; full-screen template wrappers never block fire.</summary>
		public bool IsPointerOverBlockingElement(Vector2 panelPosition) {
			return (IsDisplayed(BlockingPanels[0]) && BlockingPanels[0].worldBound.Contains(panelPosition)) ||
				(IsDisplayed(BlockingPanels[1]) && BlockingPanels[1].worldBound.Contains(panelPosition)) ||
				(IsDisplayed(BlockingPanels[2]) && BlockingPanels[2].worldBound.Contains(panelPosition));
		}

		void RefreshServers(IReadOnlyList<ArenaDiscoveryPresentationState> servers) {
			if ( ServersMatch(servers) ) {
				return;
			}
			_serverList.Clear();
			if ( servers.Count == 0 ) {
				var empty = new Label("None yet - type an address above.");
				empty.AddToClassList("arena-muted-label");
				empty.AddToClassList("server-list-empty");
				_serverList.Add(empty);
				return;
			}
			foreach ( var server in servers ) {
				var button = new Button { text = server.Label, userData = server.Address };
				button.AddToClassList("arena-button");
				button.AddToClassList("server-list-button");
				var address = server.Address;
				button.OnClick(() => _serverSelected?.Invoke(address));
				_serverList.Add(button);
			}
		}

		bool ServersMatch(IReadOnlyList<ArenaDiscoveryPresentationState> servers) {
			if ( servers.Count == 0 ) {
				return (_serverList.childCount == 1) && (_serverList[0] is Label label) &&
					label.ClassListContains("server-list-empty");
			}
			if ( _serverList.childCount != servers.Count ) {
				return false;
			}
			for ( var i = 0; i < servers.Count; i++ ) {
				if ( !(_serverList[i] is Button button) || (button.text != servers[i].Label) ||
					!Equals(button.userData, servers[i].Address) ) {
					return false;
				}
			}
			return true;
		}

		void RefreshLeaderboard(IReadOnlyList<ArenaRosterPresentationState> rows) {
			var container = _leaderboardRows.contentContainer;
			if ( LeaderboardMatches(container, rows) ) {
				return;
			}
			container.Clear();
			for ( var i = 0; i < rows.Count; i++ ) {
				var row = rows[i];
				var element = new VisualElement();
				element.AddToClassList("leaderboard-row");
				if ( (i & 1) != 0 ) {
					element.AddToClassList("leaderboard-row--alternate");
				}
				AddCell(element, row.Name, "name");
				AddCell(element, row.Kills, "kills");
				AddCell(element, row.Deaths, "deaths");
				AddCell(element, row.Status, "status");
				AddCell(element, row.Ip, "ip");
				AddCell(element, row.Client, "client");
				AddCell(element, row.Ping, "ping");
				container.Add(element);
			}
		}

		static bool LeaderboardMatches(VisualElement container, IReadOnlyList<ArenaRosterPresentationState> rows) {
			if ( container.childCount != rows.Count ) {
				return false;
			}
			for ( var i = 0; i < rows.Count; i++ ) {
				var element = container[i];
				if ( element.childCount != 7 ) {
					return false;
				}
				var row = rows[i];
				if ( !CellEquals(element, 0, row.Name) || !CellEquals(element, 1, row.Kills) ||
					!CellEquals(element, 2, row.Deaths) || !CellEquals(element, 3, row.Status) ||
					!CellEquals(element, 4, row.Ip) || !CellEquals(element, 5, row.Client) ||
					!CellEquals(element, 6, row.Ping) ) {
					return false;
				}
			}
			return true;
		}

		static bool CellEquals(VisualElement row, int index, string value) {
			return (row[index] is Label label) && (label.text == value);
		}

		static void AddCell(VisualElement row, string text, string column) {
			var label = new Label(text);
			label.AddToClassList("leaderboard-cell");
			label.AddToClassList("leaderboard-cell--" + column);
			row.Add(label);
		}

		static void SetLabel(Label label, string text) {
			label.text = text;
			SetDisplayed(label, !string.IsNullOrEmpty(text));
		}

		static void SetDisplayed(VisualElement element, bool displayed) {
			element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;
		}

		static bool IsDisplayed(VisualElement element) {
			for ( var current = element; current != null; current = current.parent ) {
				if ( (current.style.display.value == DisplayStyle.None) || !current.visible ) {
					return false;
				}
			}
			return true;
		}

		static T Require<T>(VisualElement root, string name) where T : VisualElement {
			var element = root.Q<T>(name);
			if ( element == null ) {
				throw new InvalidOperationException($"Arena HUD UXML is missing required {typeof(T).Name} '{name}'.");
			}
			return element;
		}
	}

	static class VisualElementClickExtensions {
		public static void OnClick(this VisualElement element, Action handler) {
			if ( handler == null ) {
				return;
			}
			element.RegisterCallback<PointerUpEvent>(evt => {
				if ( (evt.button != 0) || !element.enabledInHierarchy ) {
					return;
				}
				if ( !element.ContainsPoint(evt.localPosition) ) {
					return;
				}
				handler();
			});
		}
	}
}
