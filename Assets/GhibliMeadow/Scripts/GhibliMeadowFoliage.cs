using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CircuitRace.Environment
{
    /// <summary>Draws the pack's original foliage meshes without duplicating their vertex data.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class GhibliMeadowFoliage : MonoBehaviour
    {
        public GhibliMeadowFoliageData data;

        private sealed class DrawBatch
        {
            public int prototype;
            public Matrix4x4[] matrices;
        }

        private readonly List<DrawBatch> batches = new List<DrawBatch>();
        private GhibliMeadowFoliageData cachedData;
        private Matrix4x4 cachedTransform;

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            Camera.onPreCull += BeforeBuiltInCamera;
            Rebuild();
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            Camera.onPreCull -= BeforeBuiltInCamera;
            batches.Clear();
            cachedData = null;
        }

        private void OnValidate() => cachedData = null;

        public void Rebuild()
        {
            batches.Clear();
            cachedData = data;
            cachedTransform = transform.localToWorldMatrix;
            if (data == null) return;
            var groups = new Dictionary<Vector3Int, List<Matrix4x4>>();
            foreach (var placement in data.placements)
            {
                if (placement.prototype < 0 || placement.prototype >= data.prototypes.Length) continue;
                var key = new Vector3Int(Mathf.FloorToInt(placement.position.x / 18),
                    Mathf.FloorToInt(placement.position.z / 18), placement.prototype);
                if (!groups.TryGetValue(key, out var matrices))
                {
                    matrices = new List<Matrix4x4>();
                    groups.Add(key, matrices);
                }
                matrices.Add(cachedTransform * Matrix4x4.TRS(placement.position,
                    Quaternion.Euler(0, placement.yaw, 0), placement.scale) *
                    data.prototypes[placement.prototype].localTransform);
            }
            foreach (var group in groups)
            {
                // Stay below the 511-instance limit on backends that upload inverse matrices.
                for (int start = 0; start < group.Value.Count; start += 500)
                {
                    int count = Mathf.Min(500, group.Value.Count - start);
                    var matrices = new Matrix4x4[count];
                    group.Value.CopyTo(start, matrices, 0, count);
                    batches.Add(new DrawBatch { prototype = group.Key.z, matrices = matrices });
                }
            }
        }

        private void BeforeBuiltInCamera(Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline == null) Draw(camera);
        }

        private void BeforeCamera(ScriptableRenderContext context, Camera camera) => Draw(camera);

        private void Draw(Camera camera)
        {
            if (!isActiveAndEnabled || data == null || camera == null || camera.cameraType == CameraType.Preview) return;
            if ((camera.cullingMask & (1 << gameObject.layer)) == 0) return;
            if (cachedData != data || cachedTransform != transform.localToWorldMatrix) Rebuild();
            foreach (var batch in batches)
            {
                var prototype = data.prototypes[batch.prototype];
                if (prototype.mesh == null || prototype.material == null) continue;
                var parameters = new RenderParams(prototype.material)
                {
                    camera = camera,
                    layer = gameObject.layer,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = true,
                    lightProbeUsage = LightProbeUsage.Off
                };
                // Explicit draws have no owning Scene renderer; include the camera's
                // temporary editor isolation mask when producing scene-only previews.
                #if UNITY_EDITOR
                parameters.overrideSceneCullingMask = true;
                parameters.sceneCullingMask = UnityEditor.SceneManagement.EditorSceneManager.GetSceneCullingMask(gameObject.scene);
                #endif
                Graphics.RenderMeshInstanced(parameters, prototype.mesh, prototype.subMesh, batch.matrices);
            }
        }
    }
}
