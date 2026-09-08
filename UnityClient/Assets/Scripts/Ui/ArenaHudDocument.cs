using System.Collections.Generic;
using MultiplayerDemo.Client.Ecs;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace MultiplayerDemo.Client.Ui {
	/// <summary>Binds the authored Arena HUD document to the explicitly supplied client runtime.</summary>
	[RequireComponent(typeof(PanelRenderer))]
	[DisallowMultipleComponent]
	public sealed class ArenaHudDocument : MonoBehaviour {
		readonly List<ArenaDiscoveryPresentationInput> _servers = new();
		readonly List<ArenaRosterPresentationInput> _roster = new();

		PanelRenderer _document;
		ArenaClientBootstrap _client;
		VisualElement _root;
		ArenaHudView _view;

		void Awake() {
			_document = GetComponent<PanelRenderer>();
			_document.RegisterUIReloadCallback(OnUIReload);
		}

		void OnDestroy() {
			if ( _document != null ) {
				_document.UnregisterUIReloadCallback(OnUIReload);
			}
			_view = null;
			_root = null;
			_client = null;
		}

		void Update() {
			Refresh();
		}

		public void Bind(ArenaClientBootstrap client) {
			_client = client;
			TryBindView();
		}

		public bool IsPointerOverBlockingUi() {
			if ( (_view == null) || (Mouse.current == null) || (_view.Root.panel == null) ) {
				return false;
			}
			var screenPosition = ArenaInput.BuildPanelScreenPosition(
				Mouse.current.position.ReadValue(), Screen.height);
			var panelPosition = RuntimePanelUtils.ScreenToPanel(_view.Root.panel, screenPosition);
			return _view.IsPointerOverBlockingElement(panelPosition);
		}

		void OnUIReload(PanelRenderer _, VisualElement rootElement) {
			_root = rootElement;
			_view = null;
			TryBindView();
		}

		void TryBindView() {
			if ( (_root == null) || (_client == null) ) {
				return;
			}
			_view = new ArenaHudView(
				_root,
				HandlePlayerNameChanged,
				HandleAddressChanged,
				HandleServerSelected,
				HandleConnect,
				HandleDiscoveryRefresh);
			Refresh();
		}

		void Refresh() {
			var client = _client;
			var view = _view;
			var link = client?.Link;
			if ( (client == null) || (view == null) || (link == null) ) {
				return;
			}

			_servers.Clear();
			var discovery = client.Discovery;
			if ( discovery != null ) {
				foreach ( var server in discovery.Servers ) {
					_servers.Add(new ArenaDiscoveryPresentationInput(
						server.Name, server.Address, server.Players));
				}
			}

			_roster.Clear();
			foreach ( var player in link.Roster ) {
				_roster.Add(new ArenaRosterPresentationInput(
					player.id,
					player.name,
					player.kills,
					player.deaths,
					player.connected,
					player.ip,
					player.client,
					player.ping));
			}

			var hasLocalPlayer = client.TryGetLocalPlayer(out var health, out var cooldown);
			var playerName = link.IsPlaying ? link.PlayerName : client.PlayerName;
			view.Refresh(ArenaHudProjector.Project(new ArenaHudPresentationInput(
				new ArenaConnectionPresentationInput(
					playerName,
					client.Address,
					link.Status,
					link.Message,
					discovery?.Description ?? string.Empty,
					_servers),
				new ArenaGameplayPresentationInput(
					hasLocalPlayer,
					health.Value,
					health.Max,
					cooldown.RemainingMs,
					link.LocalRespawnInMs,
					client.ElapsedMs(),
					client.PlayerEntityCount(),
					client.BulletEntityCount(),
					link.SnapshotsReceived),
				new ArenaLeaderboardPresentationInput(
					client.ShowLeaderboard,
					link.Welcome?.id,
					_roster))));
		}

		void HandlePlayerNameChanged(string playerName) {
			if ( _client != null ) {
				_client.PlayerName = playerName ?? string.Empty;
			}
		}

		void HandleAddressChanged(string address) {
			if ( _client != null ) {
				_client.Address = address ?? string.Empty;
			}
		}

		void HandleServerSelected(string address) {
			HandleAddressChanged(address);
		}

		void HandleConnect() {
			var client = _client;
			client?.Connect(client.Address, client.PlayerName);
		}

		void HandleDiscoveryRefresh() {
			_client?.Discovery?.Refresh();
		}
	}
}
