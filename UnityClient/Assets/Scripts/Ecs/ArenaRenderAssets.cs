using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace MultiplayerDemo.Client.Ecs {
	/// <summary>
	/// Runtime-built meshes and URP materials, held as a managed singleton so the systems that spawn
	/// entities can reach them without a static.
	/// </summary>
	public sealed class ArenaRenderAssets : IComponentData {
		public const int PlayerMesh = 0;
		public const int BulletMesh = 1;
		public const int LocalMaterial = 0;
		public const int RemoteMaterial = 1;
		public const int BulletMaterial = 2;

		public RenderMeshArray Actors;
		public RenderMeshArray Floor;
		public AABB PlayerBounds;
		public AABB BulletBounds;
		public AABB FloorBounds;
		public bool FloorReady;

		Material _floorMaterial;

		public static ArenaRenderAssets Create() {
			var playerMesh = MeshFactory.CreateBox("ArenaPlayer", 1f, 1f, 1f);
			var bulletMesh = MeshFactory.CreateBox("ArenaBullet", 0.3f, 0.3f, 0.3f);
			var assets = new ArenaRenderAssets {
				PlayerBounds = ToBounds(playerMesh),
				BulletBounds = ToBounds(bulletMesh),
				_floorMaterial = CreateMaterial("ArenaFloor", new Color(0.16f, 0.17f, 0.20f))
			};
			assets.Actors = RenderMeshArray.CreateWithDeduplication(
				new List<Material> {
					CreateMaterial("ArenaLocalPlayer", new Color(0.15f, 0.85f, 0.25f)),
					CreateMaterial("ArenaRemotePlayer", new Color(0.90f, 0.20f, 0.18f)),
					CreateMaterial("ArenaBullet", new Color(0.98f, 0.85f, 0.15f))
				},
				new List<Mesh> { playerMesh, bulletMesh });
			return assets;
		}

		/// <summary>The floor is sized from the server's arena bounds, so it can only be built on welcome.</summary>
		public void EnsureFloor(float width, float depth) {
			if ( FloorReady ) {
				return;
			}
			var mesh = MeshFactory.CreateBox("ArenaFloor", width, 0.5f, depth);
			FloorBounds = ToBounds(mesh);
			Floor = RenderMeshArray.CreateWithDeduplication(
				new List<Material> { _floorMaterial },
				new List<Mesh> { mesh });
			FloorReady = true;
		}

		static AABB ToBounds(Mesh mesh) {
			var bounds = mesh.bounds;
			return new AABB {
				Center = new float3(bounds.center.x, bounds.center.y, bounds.center.z),
				Extents = new float3(bounds.extents.x, bounds.extents.y, bounds.extents.z)
			};
		}

		static Material CreateMaterial(string name, Color color) {
			var shader = Shader.Find("Universal Render Pipeline/Lit") ??
				Shader.Find("Universal Render Pipeline/Unlit");
			if ( shader == null ) {
				Debug.LogError("URP Lit shader not found; the project must use the Universal Render Pipeline.");
				return null;
			}
			var material = new Material(shader) {
				name = name,
				enableInstancing = true,
				hideFlags = HideFlags.HideAndDontSave
			};
			if ( material.HasProperty("_BaseColor") ) {
				material.SetColor("_BaseColor", color);
			}
			if ( material.HasProperty("_Smoothness") ) {
				material.SetFloat("_Smoothness", 0.1f);
			}
			return material;
		}
	}
}
