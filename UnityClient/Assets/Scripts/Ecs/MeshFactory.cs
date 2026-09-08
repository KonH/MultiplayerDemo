using UnityEngine;

namespace MultiplayerDemo.Client.Ecs {
	/// <summary>
	/// The whole project ships without a single authored art asset, so every mesh the client draws is
	/// a box built here at runtime.
	/// </summary>
	public static class MeshFactory {
		public static Mesh CreateBox(string name, float sizeX, float sizeY, float sizeZ) {
			var x = sizeX * 0.5f;
			var y = sizeY * 0.5f;
			var z = sizeZ * 0.5f;
			var vertices = new[] {
				// -Z
				new Vector3(-x, -y, -z), new Vector3(-x, y, -z), new Vector3(x, y, -z), new Vector3(x, -y, -z),
				// +Z
				new Vector3(x, -y, z), new Vector3(x, y, z), new Vector3(-x, y, z), new Vector3(-x, -y, z),
				// -X
				new Vector3(-x, -y, z), new Vector3(-x, y, z), new Vector3(-x, y, -z), new Vector3(-x, -y, -z),
				// +X
				new Vector3(x, -y, -z), new Vector3(x, y, -z), new Vector3(x, y, z), new Vector3(x, -y, z),
				// -Y
				new Vector3(-x, -y, z), new Vector3(-x, -y, -z), new Vector3(x, -y, -z), new Vector3(x, -y, z),
				// +Y
				new Vector3(-x, y, -z), new Vector3(-x, y, z), new Vector3(x, y, z), new Vector3(x, y, -z)
			};
			var normals = new Vector3[24];
			var faceNormals = new[] {
				Vector3.back, Vector3.forward, Vector3.left, Vector3.right, Vector3.down, Vector3.up
			};
			for ( var face = 0; face < 6; face++ ) {
				for ( var corner = 0; corner < 4; corner++ ) {
					normals[(face * 4) + corner] = faceNormals[face];
				}
			}
			var uv = new Vector2[24];
			for ( var face = 0; face < 6; face++ ) {
				uv[(face * 4) + 0] = new Vector2(0f, 0f);
				uv[(face * 4) + 1] = new Vector2(0f, 1f);
				uv[(face * 4) + 2] = new Vector2(1f, 1f);
				uv[(face * 4) + 3] = new Vector2(1f, 0f);
			}
			var triangles = new int[36];
			for ( var face = 0; face < 6; face++ ) {
				var v = face * 4;
				var t = face * 6;
				// Unity culls counter-clockwise faces, so the corners are wound the other way round.
				triangles[t + 0] = v + 0;
				triangles[t + 1] = v + 2;
				triangles[t + 2] = v + 1;
				triangles[t + 3] = v + 0;
				triangles[t + 4] = v + 3;
				triangles[t + 5] = v + 2;
			}
			var mesh = new Mesh {
				name = name,
				// Runtime-only asset: keep it out of the scene so play mode does not report it leaked.
				hideFlags = HideFlags.HideAndDontSave,
				vertices = vertices,
				normals = normals,
				uv = uv,
				triangles = triangles
			};
			mesh.RecalculateBounds();
			return mesh;
		}
	}
}
