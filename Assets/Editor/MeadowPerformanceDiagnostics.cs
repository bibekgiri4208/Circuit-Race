using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CircuitRace.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Stopwatch = System.Diagnostics.Stopwatch;

[InitializeOnLoad]
public static class MeadowPerformanceDiagnostics
{
    static MeadowPerformanceDiagnostics()
    {
        EditorApplication.delayCall += () =>
        {
            const string requestPath = "Temp/MeadowPerformance.request";
            if (!File.Exists(requestPath)) return;
            string request = File.ReadAllText(requestPath).Trim();
            File.Delete(requestPath);
            try
            {
                if (request == "apply") ApplyBalanced();
                else if (request == "benchmark") Benchmark();
                else Validate();
            }
            catch (Exception exception)
            {
                File.WriteAllText("Temp/MeadowPerformance.validation.txt", "FAILED: " + exception);
                Debug.LogException(exception);
            }
        };
    }

    [MenuItem("Tools/Ghibli Meadow/Apply Balanced Foliage in Testing")]
    public static void ApplyBalanced()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/Testing.unity")
            throw new InvalidOperationException("Open Testing outside Play mode to save its foliage settings.");
        string backup = "Temp/Testing-before-performance-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
        if (!EditorSceneManager.SaveScene(scene, backup, true)) throw new IOException("Could not back up Testing.");
        foreach (var foliage in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GhibliMeadowFoliage>()))
        {
            Undo.RecordObject(foliage, "Balanced meadow foliage");
            foliage.drawDistance = 60;
            foliage.fullDensityDistance = 18;
            foliage.distantDensity = 0.2f;
            EditorUtility.SetDirty(foliage);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Testing's foliage settings.");
        Validate();
        GhibliMeadowBuilder.RefreshPreviews();
    }

    [MenuItem("Tools/Ghibli Meadow/Benchmark Foliage Draw Distance")]
    public static void Benchmark()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Run the fixed-camera benchmark outside Play mode.");
        var scene = SceneManager.GetActiveScene();
        var foliage = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GhibliMeadowFoliage>()).Single();
        var camera = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>())
            .First(c => c.enabled && c.CompareTag("MainCamera"));
        float oldDistance = foliage.drawDistance, oldNear = foliage.fullDensityDistance, oldDensity = foliage.distantDensity;
        bool oldEnabled = foliage.enabled;
        var oldTarget = camera.targetTexture;
        var oldActive = RenderTexture.active;
        var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.DefaultHDR);
        var pixel = new Texture2D(1, 1, TextureFormat.RGB24, false);
        var report = new List<string> { "1280x720 fixed-camera synchronous render benchmark; includes GPU readback, not gameplay FPS." };
        try
        {
            camera.targetTexture = target;
            foreach (var profile in new[] { new Vector3(10000, 9999, 1), new Vector3(85, 25, 0.25f),
                new Vector3(60, 18, 0.2f), new Vector3(45, 15, 0.15f), Vector3.zero, new Vector3(60, 18, 0.2f) })
            {
                foliage.enabled = profile.x > 0;
                foliage.drawDistance = profile.x;
                foliage.fullDensityDistance = profile.y;
                foliage.distantDensity = profile.z;
                var timer = new Stopwatch();
                for (int i = 0; i < 12; i++)
                {
                    if (i == 2) timer.Start();
                    camera.Render();
                    RenderTexture.active = target;
                    pixel.ReadPixels(new Rect(640, 360, 1, 1), 0, 0, false);
                }
                timer.Stop();
                foliage.GetVisibilityCounts(camera, out int total, out int visible, out int instances);
                report.Add($"Distance {profile.x}m / full {profile.y}m / distant density {profile.z}: " +
                    $"{timer.Elapsed.TotalMilliseconds / 10:F2} ms/render; batches {visible}/{total}; instances {instances:N0}.");
            }
        }
        catch (Exception exception)
        {
            report.Add("FAILED: " + exception);
            Debug.LogException(exception);
        }
        finally
        {
            foliage.drawDistance = oldDistance;
            foliage.fullDensityDistance = oldNear;
            foliage.distantDensity = oldDensity;
            foliage.enabled = oldEnabled;
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            Object.DestroyImmediate(pixel);
            RenderTexture.ReleaseTemporary(target);
            File.WriteAllLines("Temp/MeadowPerformance.benchmark.txt", report);
        }
    }

    [MenuItem("Tools/Ghibli Meadow/Validate Foliage Performance")]
    public static void Validate()
    {
        var report = new List<string>();
        try
        {
            TestVisibility();
            report.Add("PASS: front/behind camera, distance, transformed bounds, camera layer mask, null camera.");
            var scene = SceneManager.GetActiveScene();
            report.Add("Scene: " + scene.path);
            report.Add("Play mode: " + EditorApplication.isPlaying);
            report.Add("GPU: " + SystemInfo.graphicsDeviceName);
            report.Add("Lightmaps retained: " + LightmapSettings.lightmaps.Length);
            report.Add("Baked probes retained: " + (LightmapSettings.lightProbes != null ? LightmapSettings.lightProbes.count : 0));
            var cameras = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Camera>(true)).ToArray();
            foreach (var foliage in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GhibliMeadowFoliage>()))
            {
                if (foliage.data == null) continue;
                long triangles = 0;
                foreach (var placement in foliage.data.placements)
                {
                    var prototype = foliage.data.prototypes[placement.prototype];
                    if (prototype.mesh != null) triangles += prototype.mesh.GetIndexCount(prototype.subMesh) / 3;
                }
                report.Add($"Foliage placements: {foliage.data.placements.Length:N0}; unculled triangles per pass: {triangles:N0}");
                foreach (var camera in cameras.Where(c => c.isActiveAndEnabled || c.name.StartsWith("Reference Camera")))
                {
                    foliage.GetVisibilityCounts(camera, out int total, out int visible, out int instances);
                    report.Add($"{camera.name}: batches {visible}/{total}; instances {instances:N0}/{foliage.data.placements.Length:N0}");
                }
                var shader = Shader.Find("CircuitRace/Ghibli Meadow/Sunlit Vegetation");
                if (shader == null || ShaderUtil.ShaderHasError(shader))
                    throw new InvalidOperationException("The foliage shader is missing or has compilation errors.");
            }
            report.Add("PASS: foliage shader has no reported errors. Counts are draw submissions, not measured FPS.");
        }
        catch (Exception exception)
        {
            report.Add("FAILED: " + exception);
            Debug.LogException(exception);
        }
        File.WriteAllLines("Temp/MeadowPerformance.validation.txt", report);
    }

    private static void TestVisibility()
    {
        var preview = EditorSceneManager.NewPreviewScene();
        var mesh = new Mesh { name = "Foliage culling test mesh" };
        var material = new Material(Shader.Find("CircuitRace/Ghibli Meadow/Sunlit Vegetation"));
        var data = ScriptableObject.CreateInstance<GhibliMeadowFoliageData>();
        try
        {
            mesh.vertices = new[] { new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 0, 0), new Vector3(0, 1, 0) };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.RecalculateBounds();
            data.prototypes = new[] { new GhibliMeadowFoliageData.Prototype
                { mesh = mesh, material = material, subMesh = 0, localTransform = Matrix4x4.identity } };
            data.placements = new[] { 36f, -36f, 500f }.Select(z => new GhibliMeadowFoliageData.Placement
                { position = new Vector3(0, 0, z), scale = Vector3.one }).ToArray();
            var root = new GameObject("Temporary foliage test");
            SceneManager.MoveGameObjectToScene(root, preview);
            var foliage = root.AddComponent<GhibliMeadowFoliage>();
            foliage.data = data;
            var cameraObject = new GameObject("Temporary test camera");
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.aspect = 1;
            camera.fieldOfView = 60;
            camera.farClipPlane = 1000;
            AssertCount(foliage, camera, 1, "front/distance culling");
            camera.transform.rotation = Quaternion.Euler(0, 180, 0);
            AssertCount(foliage, camera, 1, "rear-facing camera");
            root.transform.position = new Vector3(1000, 0, 0);
            root.transform.localScale = new Vector3(-2, 1, 0.5f);
            camera.transform.SetPositionAndRotation(root.transform.position, Quaternion.identity);
            AssertCount(foliage, camera, 1, "translated/negative/nonuniform-scale bounds");
            camera.cullingMask = 0;
            AssertCount(foliage, camera, 0, "camera layer mask");
            AssertCount(foliage, null, 0, "null camera");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            Object.DestroyImmediate(data);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(mesh);
        }
    }

    private static void AssertCount(GhibliMeadowFoliage foliage, Camera camera, int expected, string test)
    {
        foliage.GetVisibilityCounts(camera, out _, out _, out int instances);
        if (instances != expected)
            throw new InvalidOperationException($"{test}: expected {expected} visible instances, got {instances}.");
    }
}
