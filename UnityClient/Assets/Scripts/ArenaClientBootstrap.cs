using MultiplayerDemo.Client.Ecs;
using MultiplayerDemo.Client.Net;
using MultiplayerDemo.Client.Ui;
using Unity.Entities;
using UnityEngine;

namespace MultiplayerDemo.Client {
	/// <summary>
	/// The composition root. It owns the socket and the discovery service, injects them into the ECS
	/// world as a managed singleton component, and forwards keyboard/mouse state into ECS. Nothing else
	/// in the client constructs a dependency, and nothing looks one up through a static or the scene.
	/// </summary>
	[DisallowMultipleComponent]
	public sealed class ArenaClientBootstrap : MonoBehaviour {
		[SerializeField] Camera _camera;
		[SerializeField] string _defaultAddress = "localhost:8080";
		[SerializeField] string _defaultPlayerName = "UnityPlayer";
		[SerializeField] float _cameraPitch = 78f;

		World _world;
		Entity _linkEntity;
		ArenaClientLink _link;
		ArenaRenderAssets _assets;
		IServerDiscovery _discovery;
		bool _cameraFramed;
		float _framedAspect;

		public ArenaClientLink Link => _link;
		public IServerDiscovery Discovery => _discovery;
		public string Address { get; set; } = string.Empty;
		public string PlayerName { get; set; } = string.Empty;
		public bool ShowLeaderboard { get; set; }

		public bool HasArena => (_world != null) && _world.EntityManager.HasComponent<ArenaConfig>(_linkEntity);

		void Awake() {
			Address = _defaultAddress;
			PlayerName = _defaultPlayerName;

			_world = World.DefaultGameObjectInjectionWorld;
			if ( _world == null ) {
				Debug.LogError("No default ECS world; the Entities package failed to initialise.");
				enabled = false;
				return;
			}

			_assets = ArenaRenderAssets.Create();
			_link = new ArenaClientLink();
			var entityManager = _world.EntityManager;
			_linkEntity = entityManager.CreateEntity();
			entityManager.AddComponentObject(_linkEntity, _link);
			entityManager.AddComponentObject(_linkEntity, _assets);
			entityManager.AddComponentData(_linkEntity, new LocalInputState());

			_discovery = CreateDiscovery();
			_discovery.Begin();

			gameObject.AddComponent<ArenaHud>().Bind(this);
		}

		void Update() {
			if ( _world == null ) {
				return;
			}
			_discovery.Poll(Time.realtimeSinceStartup);
			if ( ArenaInput.ReadLeaderboardToggle() ) {
				ShowLeaderboard = !ShowLeaderboard;
			}
			PushInput();
			// Re-frame when the viewport aspect changes too, so a resized window still fits the arena.
			if ( _link.IsPlaying && HasArena &&
				(!_cameraFramed || ((_camera != null) && !Mathf.Approximately(_camera.aspect, _framedAspect))) ) {
				FrameCamera(_world.EntityManager.GetComponentData<ArenaConfig>(_linkEntity));
				_cameraFramed = true;
			}
			if ( !_link.IsPlaying && (_link.Status != ArenaClientStatus.Joining) && _cameraFramed ) {
				// The session ended (closed, rejected or failed): tear the arena down, keep the HUD.
				ResetWorld();
			}
		}

		void OnDestroy() {
			_discovery?.Stop();
			_link?.Connection?.Dispose();
		}

		public void Connect(string address, string playerName) {
			if ( _world == null ) {
				return;
			}
			var url = ArenaProtocol.BuildSocketUrl(address);
			if ( url.Length == 0 ) {
				_link.Status = ArenaClientStatus.Failed;
				_link.Message = "Enter a server address";
				return;
			}
			var trimmedName = (playerName ?? string.Empty).Trim();
			if ( trimmedName.Length == 0 ) {
				_link.Status = ArenaClientStatus.Failed;
				_link.Message = "Enter a player name";
				return;
			}
			Disconnect();
			_link.Connection = CreateConnection();
			_link.PlayerName = trimmedName;
			_link.JoinSent = false;
			_link.Status = ArenaClientStatus.Connecting;
			_link.Message = url;
			_link.Connection.Connect(url);
		}

		public void Disconnect() {
			_link.Connection?.Dispose();
			_link.Connection = null;
			_link.Welcome = null;
			_link.LatestState = null;
			_link.StateApplied = true;
			_link.JoinSent = false;
			_link.Roster.Clear();
			_link.Status = ArenaClientStatus.Disconnected;
			ResetWorld();
		}

		public bool TryGetLocalPlayer(out Health health, out FireCooldown cooldown) {
			health = default;
			cooldown = default;
			if ( _world == null ) {
				return false;
			}
			var entityManager = _world.EntityManager;
			using var query = entityManager.CreateEntityQuery(
				ComponentType.ReadOnly<LocalPlayerTag>(),
				ComponentType.ReadOnly<Health>(),
				ComponentType.ReadOnly<FireCooldown>());
			if ( query.IsEmpty ) {
				return false;
			}
			var entity = query.GetSingletonEntity();
			health = entityManager.GetComponentData<Health>(entity);
			cooldown = entityManager.GetComponentData<FireCooldown>(entity);
			return true;
		}

		public long ElapsedMs() {
			if ( (_world == null) || !_world.EntityManager.HasComponent<ArenaClock>(_linkEntity) ) {
				return 0;
			}
			return _world.EntityManager.GetComponentData<ArenaClock>(_linkEntity).ElapsedMs;
		}

		public int PlayerEntityCount() {
			if ( _world == null ) {
				return 0;
			}
			using var query = _world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerId>());
			return query.CalculateEntityCount();
		}

		public int BulletEntityCount() {
			if ( _world == null ) {
				return 0;
			}
			using var query = _world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<BulletTag>());
			return query.CalculateEntityCount();
		}

		void PushInput() {
			if ( !_link.IsPlaying ) {
				return;
			}
			var entityManager = _world.EntityManager;
			var state = entityManager.GetComponentData<LocalInputState>(_linkEntity);
			state.Move = ArenaInput.ReadMove();
			// Latch the trigger: the send system runs at 30 Hz and would otherwise drop a one-frame tap.
			state.Fire = state.Fire || ArenaInput.ReadFire();
			entityManager.SetComponentData(_linkEntity, state);
		}

		void ResetWorld() {
			if ( _world == null ) {
				return;
			}
			var entityManager = _world.EntityManager;
			using ( var actors = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetTransform>()) ) {
				entityManager.DestroyEntity(actors);
			}
			using ( var floors = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ArenaFloorTag>()) ) {
				entityManager.DestroyEntity(floors);
			}
			if ( entityManager.HasComponent<ArenaConfig>(_linkEntity) ) {
				entityManager.RemoveComponent<ArenaConfig>(_linkEntity);
			}
			if ( entityManager.HasComponent<ArenaClock>(_linkEntity) ) {
				entityManager.RemoveComponent<ArenaClock>(_linkEntity);
			}
			entityManager.SetComponentData(_linkEntity, new LocalInputState());
			_assets.FloorReady = false;
			_cameraFramed = false;
		}

		/// <summary>
		/// Pulls the camera back just far enough to hold the whole arena. Both axes have to be
		/// checked: fitting on the vertical FOV alone overshoots on a wide window, and the arena
		/// foreshortens by sin(pitch) on screen, so the depth that must fit is not its full size.
		/// </summary>
		void FrameCamera(ArenaConfig config) {
			if ( _camera == null ) {
				return;
			}
			var center = new Vector3(
				(config.MinX + config.MaxX) * 0.5f, 0f, (config.MinY + config.MaxY) * 0.5f);
			var pitch = Mathf.Clamp(_cameraPitch, 30f, 89f);
			var halfFov = _camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
			var tanVertical = Mathf.Tan(halfFov);
			var tanHorizontal = tanVertical * Mathf.Max(0.1f, _camera.aspect);
			var halfDepthOnScreen = (config.Depth * 0.5f) * Mathf.Sin(pitch * Mathf.Deg2Rad);
			var distance = Mathf.Max(halfDepthOnScreen / tanVertical, (config.Width * 0.5f) / tanHorizontal) * 1.08f;
			var rotation = Quaternion.Euler(pitch, 0f, 0f);
			_camera.transform.SetPositionAndRotation(center - ((rotation * Vector3.forward) * distance), rotation);
			_camera.farClipPlane = Mathf.Max(_camera.farClipPlane, distance * 3f);
			_framedAspect = _camera.aspect;
		}

		static IArenaConnection CreateConnection() {
#if UNITY_WEBGL && !UNITY_EDITOR
			return new WebGlArenaConnection();
#else
			return new NativeArenaConnection();
#endif
		}

		IServerDiscovery CreateDiscovery() {
#if UNITY_WEBGL && !UNITY_EDITOR
			return new HttpProbeServerDiscovery(this);
#else
			return new BeaconServerDiscovery();
#endif
		}
	}
}
