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

        [Min(10f), Tooltip("Grass/flower draw distance in metres. Does not affect trees or the baked ground.")]
        public float drawDistance = 60f;
        [Min(0f), Tooltip("Keep all foliage instances within this distance of a cell.")]
        public float fullDensityDistance = 18f;
        [Range(0.1f, 1f)] public float distantDensity = 0.2f;

        private sealed class DrawBatch
        {
            public int prototype;
            public Matrix4x4[] matrices;
            public MaterialPropertyBlock probes;
            public Bounds bounds;
        }

        private readonly List<DrawBatch> batches = new List<DrawBatch>();
        private GhibliMeadowFoliageData cachedData;
        private Matrix4x4 cachedTransform;
        private readonly Plane[] frustumPlanes = new Plane[6];
        private bool rebuildPending;
        private static readonly int FadeDistances = Shader.PropertyToID("_FoliageFadeDistances");

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            Camera.onPreCull += BeforeBuiltInCamera;
            LightProbes.lightProbesUpdated += InvalidateProbes;
            Rebuild();
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            Camera.onPreCull -= BeforeBuiltInCamera;
            LightProbes.lightProbesUpdated -= InvalidateProbes;
            batches.Clear();
            cachedData = null;
        }

        private void OnValidate() => cachedData = null;

        // Several probe notifications can arrive during scene loading/baking. Rebuild once,
        // when a camera actually needs these batches, rather than on every notification.
        private void InvalidateProbes() => rebuildPending = true;

        public void Rebuild()
        {
            batches.Clear();
            cachedData = data;
            cachedTransform = transform.localToWorldMatrix;
            rebuildPending = false;
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
                // Prefixes used by distance LOD must be spatially mixed, not strips of plants.
                var random = new System.Random(unchecked(group.Key.x * 73856093 ^ group.Key.y * 19349663 ^ group.Key.z * 83492791));
                for (int i = group.Value.Count - 1; i > 0; i--)
                {
                    int j = random.Next(i + 1);
                    var matrix = group.Value[i];
                    group.Value[i] = group.Value[j];
                    group.Value[j] = matrix;
                }
                // Stay below the 511-instance limit on backends that upload inverse matrices.
                for (int start = 0; start < group.Value.Count; start += 500)
                {
                    int count = Mathf.Min(500, group.Value.Count - start);
                    var matrices = new Matrix4x4[count];
                    group.Value.CopyTo(start, matrices, 0, count);
                    var positions = new Vector3[count];
                    for (int i = 0; i < count; i++) positions[i] = matrices[i].GetColumn(3);
                    var sh = new SphericalHarmonicsL2[count];
                    var occlusion = new Vector4[count];
                    LightProbes.CalculateInterpolatedLightAndOcclusionProbes(positions, sh, occlusion);
                    var properties = new MaterialPropertyBlock();
                    properties.CopySHCoefficientArraysFrom(sh);
                    properties.CopyProbeOcclusionArrayFrom(occlusion);
                    var prototype = data.prototypes[group.Key.z];
                    if (prototype.mesh == null || prototype.material == null) continue;
                    Bounds bounds = TransformBounds(prototype.mesh.bounds, matrices[0]);
                    for (int i = 1; i < count; i++) bounds.Encapsulate(TransformBounds(prototype.mesh.bounds, matrices[i]));
                    // The shader bends in world space; include its maximum displacement.
                    float wind = prototype.material.HasProperty("_WindStrength") ?
                        Mathf.Abs(prototype.material.GetFloat("_WindStrength")) : 0f;
                    bounds.Expand(new Vector3(wind * 2f + 0.1f, 0.1f, wind * 0.68f + 0.1f));
                    batches.Add(new DrawBatch { prototype = group.Key.z, matrices = matrices, probes = properties, bounds = bounds });
                }
            }
        }

        private void BeforeBuiltInCamera(Camera camera)
        {
            if (GraphicsSettings.currentRenderPipeline == null) Draw(camera);
        }

        private void BeforeCamera(ScriptableRenderContext context, Camera camera) => Draw(camera);

        private static Bounds TransformBounds(Bounds local, Matrix4x4 matrix)
        {
            Vector3 x = matrix.MultiplyVector(new Vector3(local.extents.x, 0, 0));
            Vector3 y = matrix.MultiplyVector(new Vector3(0, local.extents.y, 0));
            Vector3 z = matrix.MultiplyVector(new Vector3(0, 0, local.extents.z));
            var extents = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            return new Bounds(matrix.MultiplyPoint3x4(local.center), extents * 2f);
        }

        private void PrepareCamera(Camera camera)
        {
            if (cachedData != data || cachedTransform != transform.localToWorldMatrix || rebuildPending) Rebuild();
            GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
        }

        private int VisibleCount(DrawBatch batch, Vector3 cameraPosition)
        {
            float maximumDistance = Mathf.Max(10f, drawDistance);
            float distanceSquared = batch.bounds.SqrDistance(cameraPosition);
            if (distanceSquared >= maximumDistance * maximumDistance ||
                !GeometryUtility.TestPlanesAABB(frustumPlanes, batch.bounds)) return 0;
            float nearDistance = Mathf.Clamp(fullDensityDistance, 0f, maximumDistance - 1f);
            float lod = Mathf.InverseLerp(nearDistance, maximumDistance, Mathf.Sqrt(distanceSquared));
            float density = Mathf.Lerp(1f, Mathf.Clamp(distantDensity, 0.1f, 1f), lod * lod * (3f - 2f * lod));
            return Mathf.Max(1, Mathf.CeilToInt(batch.matrices.Length * density));
        }

        public void GetVisibilityCounts(Camera camera, out int totalBatches, out int visibleBatches, out int visibleInstances)
        {
            totalBatches = visibleBatches = visibleInstances = 0;
            if (!isActiveAndEnabled || data == null || camera == null) return;
            PrepareCamera(camera);
            totalBatches = batches.Count;
            if ((camera.cullingMask & (1 << gameObject.layer)) == 0) return;
            Vector3 position = camera.transform.position;
            foreach (var batch in batches)
            {
                int count = VisibleCount(batch, position);
                if (count == 0) continue;
                visibleBatches++;
                visibleInstances += count;
            }
        }

        private void Draw(Camera camera)
        {
            if (!isActiveAndEnabled || data == null || camera == null || camera.cameraType == CameraType.Preview) return;
            if ((camera.cullingMask & (1 << gameObject.layer)) == 0) return;
            PrepareCamera(camera);
            Vector3 cameraPosition = camera.transform.position;
            float maximumDistance = Mathf.Max(10f, drawDistance);
            var fadeDistances = new Vector4(maximumDistance * 0.8f, maximumDistance, 0f, 0f);
            foreach (var batch in batches)
            {
                int count = VisibleCount(batch, cameraPosition);
                if (count == 0) continue;
                var prototype = data.prototypes[batch.prototype];
                if (prototype.mesh == null || prototype.material == null) continue;
                batch.probes.SetVector(FadeDistances, fadeDistances);
                var parameters = new RenderParams(prototype.material)
                {
                    camera = camera,
                    layer = gameObject.layer,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = true,
                    lightProbeUsage = LightProbeUsage.CustomProvided,
                    matProps = batch.probes,
                    worldBounds = batch.bounds
                };
                // Explicit draws have no owning Scene renderer; include the camera's
                // temporary editor isolation mask when producing scene-only previews.
                #if UNITY_EDITOR
                parameters.overrideSceneCullingMask = true;
                parameters.sceneCullingMask = UnityEditor.SceneManagement.EditorSceneManager.GetSceneCullingMask(gameObject.scene);
                #endif
                Graphics.RenderMeshInstanced(parameters, prototype.mesh, prototype.subMesh, batch.matrices, count);
            }
        }
    }
}
