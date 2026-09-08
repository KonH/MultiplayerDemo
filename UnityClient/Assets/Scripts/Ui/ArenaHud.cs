using MultiplayerDemo.Client.Ecs;
using UnityEngine;

namespace MultiplayerDemo.Client.Ui {
	/// <summary>
	/// IMGUI overlay: connect screen, HUD and TAB leaderboard. IMGUI is used deliberately — it needs no
	/// authored UI assets and works unchanged in a WebGL build. Added and bound by
	/// <see cref="ArenaClientBootstrap"/>, never looked up.
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class ArenaHud : MonoBehaviour {
		ArenaClientBootstrap _client;
		GUIStyle _panel;
		GUIStyle _label;
		GUIStyle _title;
		GUIStyle _row;

		public void Bind(ArenaClientBootstrap client) {
			_client = client;
		}

		void OnGUI() {
			if ( (_client == null) || (_client.Link == null) ) {
				return;
			}
			EnsureStyles();
			if ( _client.Link.IsPlaying ) {
				DrawPlayingHud();
				if ( _client.ShowLeaderboard ) {
					DrawLeaderboard();
				}
			} else {
				DrawConnectScreen();
			}
		}

		void EnsureStyles() {
			if ( _panel != null ) {
				return;
			}
			var background = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
			background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.75f));
			background.Apply();
			_panel = new GUIStyle(GUI.skin.box) { padding = new RectOffset(14, 14, 12, 12) };
			_panel.normal.background = background;
			_label = new GUIStyle(GUI.skin.label) { fontSize = 15, richText = true };
			_title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
			_row = new GUIStyle(GUI.skin.label) { fontSize = 13 };
		}

		void DrawConnectScreen() {
			var link = _client.Link;
			var width = 460f;
			var rect = new Rect((Screen.width - width) * 0.5f, 60f, width, Screen.height - 120f);
			GUILayout.BeginArea(rect, _panel);
			GUILayout.Label("Multiplayer Arena", _title);
			GUILayout.Space(8f);

			GUILayout.Label("Player name", _label);
			_client.PlayerName = GUILayout.TextField(_client.PlayerName, 20, GUILayout.Height(22f));

			GUILayout.Space(6f);
			GUILayout.Label("Server address (host:port)", _label);
			_client.Address = GUILayout.TextField(_client.Address, 128, GUILayout.Height(22f));

			GUILayout.Space(10f);
			if ( GUILayout.Button("Connect", GUILayout.Height(30f)) ) {
				_client.Connect(_client.Address, _client.PlayerName);
			}

			GUILayout.Space(12f);
			GUILayout.Label($"<b>Discovered servers</b>  ({_client.Discovery.Description})", _label);
			var servers = _client.Discovery.Servers;
			if ( servers.Count == 0 ) {
				GUILayout.Label("None yet - type an address above.", _row);
			}
			for ( var i = 0; i < servers.Count; i++ ) {
				var server = servers[i];
				if ( GUILayout.Button($"{server.Name}  {server.Address}  ({server.Players} players)") ) {
					_client.Address = server.Address;
				}
			}
			if ( GUILayout.Button("Refresh list") ) {
				_client.Discovery.Refresh();
			}

			GUILayout.Space(12f);
			GUILayout.Label(StatusLine(link), _label);
			GUILayout.EndArea();
		}

		static string StatusLine(ArenaClientLink link) {
			return link.Status switch {
				ArenaClientStatus.Connecting => $"Connecting to {link.Message}...",
				ArenaClientStatus.Joining => "Joining...",
				ArenaClientStatus.Rejected => $"<color=#ff8080>Rejected: {link.Message}</color>",
				ArenaClientStatus.Failed => $"<color=#ff8080>Failed: {link.Message}</color>",
				ArenaClientStatus.Playing => "Connected",
				_ => string.IsNullOrEmpty(link.Message) ? "Not connected" : link.Message
			};
		}

		void DrawPlayingHud() {
			var link = _client.Link;
			GUILayout.BeginArea(new Rect(12f, 12f, 300f, 190f), _panel);
			GUILayout.Label($"<b>{link.PlayerName}</b>", _label);
			if ( _client.TryGetLocalPlayer(out var health, out var cooldown) ) {
				GUILayout.Label($"HP  {health.Value} / {health.Max}", _label);
				GUILayout.Label(
					(cooldown.RemainingMs > 0)
						? $"Fire  {ArenaFormat.Seconds(cooldown.RemainingMs)}"
						: "Fire  ready",
					_label);
			} else {
				var respawnIn = link.LocalRespawnInMs;
				GUILayout.Label("<color=#ff8080>Dead</color>", _label);
				GUILayout.Label($"Respawn in  {ArenaFormat.Seconds(respawnIn)}", _label);
			}
			GUILayout.Label($"Time  {ArenaFormat.Elapsed(_client.ElapsedMs())}", _label);
			GUILayout.Label("WASD / arrows to move, LMB / Space to fire", _row);
			GUILayout.Label("TAB for the leaderboard", _row);
			GUILayout.EndArea();

			GUILayout.BeginArea(new Rect(12f, Screen.height - 34f, 520f, 26f));
			GUILayout.Label(
				$"players {_client.PlayerEntityCount()}   bullets {_client.BulletEntityCount()}   " +
				$"snapshots {link.SnapshotsReceived}",
				_row);
			GUILayout.EndArea();
		}

		void DrawLeaderboard() {
			var link = _client.Link;
			var width = 760f;
			// Title, header row and one row per player, plus the panel's own padding.
			var height = 66f + ((link.Roster.Count + 1) * 25f);
			var rect = new Rect(
				(Screen.width - width) * 0.5f, (Screen.height - height) * 0.35f, width, height);
			GUILayout.BeginArea(rect, _panel);
			GUILayout.Label("Leaderboard", _title);
			DrawRow("Name", "Kills", "Deaths", "Status", "IP", "Client", "Ping");
			foreach ( var player in link.Roster ) {
				DrawRow(
					(player.id == link.Welcome?.id) ? player.name + " (you)" : player.name,
					player.kills.ToString(),
					player.deaths.ToString(),
					player.connected ? "connected" : "disconnected",
					player.ip,
					player.client,
					player.ping + " ms");
			}
			GUILayout.EndArea();
		}

		void DrawRow(
			string name, string kills, string deaths, string status, string ip, string client, string ping) {
			GUILayout.BeginHorizontal();
			GUILayout.Label(name, _row, GUILayout.Width(180f));
			GUILayout.Label(kills, _row, GUILayout.Width(60f));
			GUILayout.Label(deaths, _row, GUILayout.Width(60f));
			GUILayout.Label(status, _row, GUILayout.Width(110f));
			GUILayout.Label(ip, _row, GUILayout.Width(140f));
			GUILayout.Label(client, _row, GUILayout.Width(70f));
			GUILayout.Label(ping, _row, GUILayout.Width(70f));
			GUILayout.EndHorizontal();
		}
	}
}
