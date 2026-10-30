using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CircuitRace.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Bakes meadow bounce light without freezing the player, wind or sun shadows.</summary>
[InitializeOnLoad]
public static class TestingLightingBaker
{
    private const string ScenePath = "Assets/Scenes/Testing.unity";
    private const string SettingsPath = "Assets/Scenes/Testing/Meadow Lighting.lighting";
    private const string RequestPath = "Temp/TestingLighting.request";
    private const string StatusPath = "Temp/TestingLighting.status.txt";
    private static Scene scene;

    static TestingLightingBaker()
    {
        EditorApplication.delayCall += CheckRequest;
    }

    private static void CheckRequest()
    {
        if (!File.Exists(RequestPath)) return;
        string request = File.ReadAllText(RequestPath).Trim();
        File.Delete(RequestPath);
        try
        {
            if (request == "validate") Validate();
            else Bake();
        }
        catch (Exception exception)
        {
            File.WriteAllText(StatusPath, "FAILED\n" + exception);
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Ghibli Meadow/Bake Lighting in Testing")]
    public static void Bake()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Stop Play mode and any existing lighting bake first.");
        // A single-scene bake must never accidentally include another loaded scene.
        if (SceneManager.sceneCount != 1 || SceneManager.GetActiveScene().path != ScenePath)
            throw new InvalidOperationException("Open Testing by itself before baking its lighting.");
        scene = SceneManager.GetActiveScene();
        string backup = "Temp/Testing-before-lighting-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
        if (!EditorSceneManager.SaveScene(scene, backup, true))
            throw new IOException("Could not back up the current scene, including unsaved edits.");
        var meadow = scene.GetRootGameObjects().Single(g => g.name == "GHIBLI Reference Meadow");
        var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
        if (settings == null)
        {
            settings = new LightingSettings { name = "Meadow Lighting" };
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        settings.bakedGI = true;
        settings.realtimeGI = false;
        settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
        settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
        settings.lightmapResolution = 8;
        settings.lightmapMaxSize = 2048;
        settings.lightmapPadding = 4;
        settings.directionalityMode = LightmapsMode.CombinedDirectional;
        settings.directSampleCount = 32;
        settings.indirectSampleCount = 256;
        settings.environmentSampleCount = 128;
        settings.maxBounces = 3;
        settings.ao = false;
        Lightmapping.lightingSettings = settings;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssetIfDirty(settings);

        var sun = meadow.GetComponentsInChildren<Light>(true).Single(l => l.name.StartsWith("Meadow sun"));
        sun.lightmapBakeType = LightmapBakeType.Mixed;
        // The sky fill belongs in the bake; leaving it realtime would double its energy.
        var fill = meadow.GetComponentsInChildren<Light>(true).Single(l => l.name.StartsWith("Cool soft fill"));
        fill.lightmapBakeType = LightmapBakeType.Baked;
        Record(sun);
        Record(fill);

        int contributors = 0;
        foreach (var renderer in meadow.GetComponentsInChildren<MeshRenderer>())
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            // Distant decorative silhouettes should not consume the lightmap atlas.
            if (renderer.shadowCastingMode == ShadowCastingMode.Off) continue;
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            bool ground = renderer.sharedMaterials.Any(m => m != null &&
                m.shader.name == "CircuitRace/Ghibli Meadow/Painterly Ground");
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,
                GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) | StaticEditorFlags.ContributeGI);
            renderer.receiveGI = ground ? ReceiveGI.Lightmaps : ReceiveGI.LightProbes;
            renderer.scaleInLightmap = ground ? 1 : 0;
            if (ground) PrepareGroundUVs(filter);
            Record(renderer.gameObject);
            Record(renderer);
            contributors++;
        }

        var groundCollider = meadow.GetComponentsInChildren<MeshCollider>()
            .First(c => c.name.StartsWith("Rolling meadow"));
        var probeObject = meadow.transform.Find("Baked meadow light probes");
        if (probeObject == null)
        {
            probeObject = new GameObject("Baked meadow light probes").transform;
            probeObject.SetParent(meadow.transform, false);
        }
        var probes = probeObject.GetComponent<LightProbeGroup>();
        if (probes == null) probes = probeObject.gameObject.AddComponent<LightProbeGroup>();
        Physics.SyncTransforms();
        var positions = new List<Vector3>();
        for (float z = -36; z <= 152; z += 8)
        for (float x = -88; x <= 88; x += 8)
        {
            var origin = meadow.transform.TransformPoint(new Vector3(x, 100, z));
            if (!groundCollider.Raycast(new Ray(origin, -meadow.transform.up), out var hit, 200)) continue;
            foreach (float height in new[] { 0.5f, 2f, 6f })
                positions.Add(probeObject.InverseTransformPoint(hit.point + meadow.transform.up * height));
        }
        probes.probePositions = positions.ToArray();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Testing before baking.");
        File.WriteAllText(StatusPath, $"BAKING\nBackup: {backup}\nContributors: {contributors}\nProbes: {positions.Count}");
        Lightmapping.bakeCompleted -= Complete;
        Lightmapping.bakeCompleted += Complete;
        if (!Lightmapping.BakeAsync())
        {
            Lightmapping.bakeCompleted -= Complete;
            throw new InvalidOperationException("Unity could not start the lighting bake. See the Console.");
        }
    }

    private static void PrepareGroundUVs(MeshFilter filter)
    {
        if (AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith("Assets/Scenes/Testing/") &&
            filter.sharedMesh.uv2.Length == filter.sharedMesh.vertexCount) return;
        // Dedicated copies keep generated source geometry and imported pack meshes untouched.
        string path = "Assets/Scenes/Testing/" + filter.sharedMesh.name + " - Lightmapped.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh == null)
        {
            mesh = Object.Instantiate(filter.sharedMesh);
            mesh.name = filter.sharedMesh.name;
            var bounds = mesh.bounds;
            mesh.uv2 = mesh.vertices.Select(v => new Vector2(
                (v.x - bounds.min.x) / Mathf.Max(bounds.size.x, 0.001f),
                (v.z - bounds.min.z) / Mathf.Max(bounds.size.z, 0.001f))).ToArray();
            AssetDatabase.CreateAsset(mesh, path);
        }
        filter.sharedMesh = mesh;
        Record(filter);
    }

    private static void Record(Object target)
    {
        EditorUtility.SetDirty(target);
        if (PrefabUtility.IsPartOfPrefabInstance(target))
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    [MenuItem("Tools/Ghibli Meadow/Validate Baked Lighting")]
    public static void Validate()
    {
        var active = SceneManager.GetActiveScene();
        var meadow = active.GetRootGameObjects().Single(g => g.name == "GHIBLI Reference Meadow");
        var materials = meadow.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
            .Concat(meadow.GetComponentsInChildren<GhibliMeadowFoliage>()
                .SelectMany(f => f.data.prototypes.Select(p => p.material))).Where(m => m != null).Distinct();
        var report = new List<string>
        {
            "Scene: " + active.path,
            "Lightmaps: " + LightmapSettings.lightmaps.Length,
            "Probes: " + (LightmapSettings.lightProbes != null ? LightmapSettings.lightProbes.count : 0)
        };
        foreach (var material in materials)
        {
            if (material.shader == null) { report.Add("Missing shader: " + material.name); continue; }
            if (!material.shader.isSupported || ShaderUtil.ShaderHasError(material.shader))
            {
                report.Add("Shader check: " + material.name + " / " + material.shader.name);
                report.AddRange(ShaderUtil.GetShaderMessages(material.shader)
                    .Select(m => m.severity + ": " + m.message + " (" + m.file + ":" + m.line + ")"));
            }
        }
        File.WriteAllLines("Temp/TestingLighting.validation.txt", report);
    }

    private static void Complete()
    {
        Lightmapping.bakeCompleted -= Complete;
        try
        {
            if (LightmapSettings.lightmaps.Length == 0 || Lightmapping.lightingDataAsset == null)
                throw new InvalidOperationException("Bake ended without lightmaps or a lighting data asset.");
            LightProbes.Tetrahedralize();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var foliage in root.GetComponentsInChildren<GhibliMeadowFoliage>()) foliage.Rebuild();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the baked Testing scene.");
            File.WriteAllText(StatusPath, $"COMPLETE\nLightmaps: {LightmapSettings.lightmaps.Length}\n" +
                $"Probes: {LightmapSettings.lightProbes.count}\nLighting data: {AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset)}");
            GhibliMeadowBuilder.RefreshPreviews();
            Debug.Log("Testing lighting baked and saved. Ground and vegetation now use baked bounce lighting.");
        }
        catch (Exception exception)
        {
            File.WriteAllText(StatusPath, "FAILED\n" + exception);
            Debug.LogException(exception);
        }
    }
}
