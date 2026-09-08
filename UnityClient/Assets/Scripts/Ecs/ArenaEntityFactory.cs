using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine.Rendering;

namespace MultiplayerDemo.Client.Ecs {
	/// <summary>
	/// Glue for the Entities Graphics component set. Nothing is baked or prefabbed in this project, so
	/// every drawable entity is assembled here at runtime.
	/// </summary>
	public static class ArenaEntityFactory {
		public static void AddRendering(
			EntityManager entityManager,
			Entity entity,
			RenderMeshArray meshArray,
			int materialIndex,
			int meshIndex,
			AABB bounds,
			float3 position) {
			var description = new RenderMeshDescription(ShadowCastingMode.On, receiveShadows: true);
			RenderMeshUtility.AddComponents(
				entity,
				entityManager,
				description,
				meshArray,
				MaterialMeshInfo.FromRenderMeshArrayIndices(materialIndex, meshIndex));
			SetOrAdd(entityManager, entity, LocalTransform.FromPosition(position));
			SetOrAdd(entityManager, entity, new LocalToWorld { Value = float4x4.Translate(position) });
			SetOrAdd(entityManager, entity, new RenderBounds { Value = bounds });
		}

		public static void SetOrAdd<T>(EntityManager entityManager, Entity entity, T value)
			where T : unmanaged, IComponentData {
			if ( entityManager.HasComponent<T>(entity) ) {
				entityManager.SetComponentData(entity, value);
			} else {
				entityManager.AddComponentData(entity, value);
			}
		}
	}
}
