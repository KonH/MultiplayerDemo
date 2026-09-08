using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace MultiplayerDemo.Client.Net {
	/// <summary>
	/// Browser-friendly discovery: a browser cannot receive the UDP beacon, so probe /api/info on a
	/// short list of candidate ports on the page host and on localhost.
	/// </summary>
	public sealed class HttpProbeServerDiscovery : IServerDiscovery {
		static readonly int[] CandidatePorts = { 8080, 8081, 8082, 8083 };

		readonly List<DiscoveredServer> _servers = new();
		readonly MonoBehaviour _runner;

		bool _probing;
		string _description = "Probing /api/info on ports 8080-8083";

		public HttpProbeServerDiscovery(MonoBehaviour runner) {
			_runner = runner;
		}

		public IReadOnlyList<DiscoveredServer> Servers => _servers;

		public string Description => _description;

		public void Begin() => Refresh();

		public void Refresh() {
			if ( _probing ) {
				return;
			}
			_probing = true;
			_servers.Clear();
			_runner.StartCoroutine(ProbeAll());
		}

		public void Poll(float now) {
			// Nothing to drain: the coroutine already runs on the main thread.
		}

		public void Stop() {
			_probing = false;
		}

		IEnumerator ProbeAll() {
			_description = "Probing /api/info on ports 8080-8083...";
			foreach ( var host in CandidateHosts() ) {
				foreach ( var port in CandidatePorts ) {
					yield return Probe(host, port);
				}
			}
			_probing = false;
			_description = (_servers.Count > 0)
				? $"Found {_servers.Count} server(s) via /api/info"
				: "No server answered /api/info on ports 8080-8083";
		}

		IEnumerator Probe(string host, int port) {
			using var request = UnityWebRequest.Get($"http://{host}:{port}/api/info");
			request.timeout = 2;
			yield return request.SendWebRequest();
			if ( request.result != UnityWebRequest.Result.Success ) {
				yield break;
			}
			var info = ArenaProtocol.ParseServerInfo(request.downloadHandler.text);
			if ( (info == null) || string.IsNullOrEmpty(info.wsPath) ) {
				yield break;
			}
			_servers.Add(new DiscoveredServer {
				Name = info.name,
				Host = host,
				Port = port,
				Players = info.players,
				LastSeen = Time.realtimeSinceStartup
			});
		}

		static IEnumerable<string> CandidateHosts() {
			yield return "localhost";
			var pageHost = PageHost();
			if ( !string.IsNullOrEmpty(pageHost) &&
				!string.Equals(pageHost, "localhost", StringComparison.OrdinalIgnoreCase) ) {
				yield return pageHost;
			}
		}

		static string PageHost() {
			var url = Application.absoluteURL;
			if ( string.IsNullOrEmpty(url) ) {
				return null;
			}
			try {
				return new Uri(url).Host;
			} catch ( UriFormatException ) {
				return null;
			}
		}
	}
}
