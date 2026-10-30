using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using CircuitRace.Environment;
using Object = UnityEngine.Object;
using Random = System.Random;

/// <summary>Deterministic, editor-only dressing of Testing. Never edits source pack assets.</summary>
public static class GhibliMeadowBuilder
{
    private const string ScenePath = "Assets/Scenes/Testing.unity";
    private const string Output = "Assets/GhibliMeadow";
    private const string Pack = "Assets/GHIBLI style/Art/";
    private const string RootName = "GHIBLI Reference Meadow";
    private static Random random;
    private static Scene scene;
    private static Transform root;
    private static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    private static readonly List<Vector3> path = new List<Vector3>();

    [MenuItem("Tools/Ghibli Meadow/Build in Testing Scene")]
    public static void BuildFromMenu()
    {
        if (!EditorUtility.DisplayDialog("Build reference meadow in Testing?",
                "Adds the meadow to Testing only. Keeps the gameplay setup, with optional reference cameras. " +
                "Rebuilding replaces only the generated GHIBLI Reference Meadow root and its generated resources.", "Build", "Cancel")) return;
        Build();
        EditorUtility.DisplayDialog("Meadow ready", "Saved in Assets/Scenes/Testing.unity. Open Testing to view it.", "OK");
    }

    private static void Build()
    {
        var previous = SceneManager.GetActiveScene();
        scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var randomState = UnityEngine.Random.state;
        try
        {
            Directory.CreateDirectory(Output + "/Generated");
            Directory.CreateDirectory(Output + "/Materials");
            AssetDatabase.Refresh();
            random = new Random(20261030);
            UnityEngine.Random.InitState(20261030);
            materials.Clear();
            path.Clear();
            var oldRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            var sunSettings = LightSettings.Capture(oldRoot, "Meadow sun");
            var fillSettings = LightSettings.Capture(oldRoot, "Cool soft fill");
            if (oldRoot != null) Object.DestroyImmediate(oldRoot);

            // Keep gameplay objects/cameras intact. Only the legacy terrain conflicts with this ground.
            foreach (var gameObject in scene.GetRootGameObjects())
            {
                foreach (var terrain in gameObject.GetComponentsInChildren<Terrain>(true))
                {
                    terrain.drawHeightmap = false;
                    terrain.drawTreesAndFoliage = false;
                    terrain.enabled = false;
                    var collider = terrain.GetComponent<TerrainCollider>();
                    if (collider != null) collider.enabled = false;
                    terrain.gameObject.SetActive(false);
                }
            }

            root = new GameObject(RootName).transform;
            BuildPath();
            MakeGround();
            MakeTrees();
            MakeRocksAndShrubs();
            MakeFencesAndShrine();
            MakeMeadow();
            MakeMountains();
            MakeLighting(sunSettings, fillSettings);
            var cameras = MakeCameras();
            GhibliMeadowPlaySetup.Ensure(scene, root);
            MakePetals();
            // Do not save unrelated dirty assets the user may have open in another scene.
            foreach (string guid in AssetDatabase.FindAssets("", new[] { Output }))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && EditorUtility.IsDirty(asset)) AssetDatabase.SaveAssetIfDirty(asset);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Capture(cameras[0], Output + "/Preview-Path.png");
            Capture(cameras[1], Output + "/Preview-Meadow.png");
            File.WriteAllText("Temp/GhibliMeadow.validation.txt", Validate());
            // Let Unity update light probes/render state before producing the final previews.
            EditorApplication.delayCall += RefreshPreviews;
            Debug.Log("GHIBLI meadow saved in Testing. Reference cameras and previews are in Assets/GhibliMeadow.");
        }
        finally
        {
            UnityEngine.Random.state = randomState;
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (openedHere && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.Refresh();
        }
    }

    private static Transform Group(string name)
    {
        var group = new GameObject(name).transform;
        group.SetParent(root, false);
        return group;
    }

    private static float Next(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());
    private static Vector3 OnGround(float x, float z) => new Vector3(x, Height(x, z), z);
    private static float Height(float x, float z)
    {
        return 0.26f + Mathf.Sin(x * 0.043f + z * 0.021f) * 0.3f +
               Mathf.Sin(z * 0.067f - x * 0.018f) * 0.28f +
               Mathf.PerlinNoise(x * 0.026f + 20, z * 0.026f + 30) * 0.7f +
               Mathf.Pow(Mathf.Clamp01((Mathf.Abs(x) - 42) / 65), 2) * 4.5f;
    }

    private static void BuildPath()
    {
        Vector3[] points =
        {
            new Vector3(10,0,-35), new Vector3(9,0,-15), new Vector3(5,0,0),
            new Vector3(-5,0,16), new Vector3(-7,0,29), new Vector3(0,0,42),
            new Vector3(11,0,54), new Vector3(12,0,65), new Vector3(1,0,71)
        };
        for (int segment = 0; segment < points.Length - 1; segment++)
        {
            var a = points[Mathf.Max(segment - 1, 0)];
            var b = points[segment];
            var c = points[segment + 1];
            var d = points[Mathf.Min(segment + 2, points.Length - 1)];
            for (int j = 0; j < 24; j++)
            {
                float t = j / 24f;
                var p = 0.5f * ((2 * b) + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (-a + 3 * b - 3 * c + d) * t * t * t);
                path.Add(OnGround(p.x, p.z));
            }
        }
        path.Add(OnGround(points[points.Length - 1].x, points[points.Length - 1].z));
    }

    private static float PathDistance(float x, float z)
    {
        float best = float.MaxValue;
        var p = new Vector2(x, z);
        for (int i = 0; i < path.Count - 1; i += 3)
        {
            var a = new Vector2(path[i].x, path[i].z);
            var b = new Vector2(path[Mathf.Min(i + 3, path.Count - 1)].x, path[Mathf.Min(i + 3, path.Count - 1)].z);
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.001f));
            best = Mathf.Min(best, (p - (a + t * ab)).sqrMagnitude);
        }
        return Mathf.Sqrt(best);
    }

    private static Material GroundMaterial(string name, string texture, float textureStrength)
    {
        var material = new Material(Shader.Find("CircuitRace/Ghibli Meadow/Painterly Ground")) { name = name };
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Pack + "Textures/" + texture));
        material.SetFloat("_TextureStrength", textureStrength);
        return SaveAsset(material, Output + "/Materials/" + name + ".mat");
    }

    private static void MakeGround()
    {
        var group = Group("01 • Rolling meadow and golden footpath");
        const int nx = 180, nz = 190;
        var vertices = new Vector3[(nx + 1) * (nz + 1)];
        var colors = new Color[vertices.Length];
        var uv = new Vector2[vertices.Length];
        for (int z = 0; z <= nz; z++)
        for (int x = 0; x <= nx; x++)
        {
            int i = z * (nx + 1) + x;
            float wx = x - 90, wz = z - 38;
            vertices[i] = OnGround(wx, wz);
            uv[i] = new Vector2(wx, wz) * 0.25f;
            float patch = Mathf.PerlinNoise(wx * 0.07f + 51, wz * 0.07f + 7);
            colors[i] = Color.Lerp(new Color(0.29f, 0.50f, 0.22f), new Color(0.48f, 0.68f, 0.29f), patch).linear;
        }
        var triangles = new int[nx * nz * 6];
        int at = 0;
        for (int z = 0; z < nz; z++)
        for (int x = 0; x < nx; x++)
        {
            int i = z * (nx + 1) + x;
            triangles[at++] = i; triangles[at++] = i + nx + 1; triangles[at++] = i + 1;
            triangles[at++] = i + 1; triangles[at++] = i + nx + 1; triangles[at++] = i + nx + 2;
        }
        var mesh = new Mesh { name = "Rolling meadow", vertices = vertices, triangles = triangles, colors = colors, uv = uv };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        MeshObject("Rolling meadow • walkable", mesh, GroundMaterial("Meadow Ground", "Grass_ground_Base_Color.png", 0.2f), group, true);

        const int across = 8;
        vertices = new Vector3[path.Count * (across + 1)];
        colors = new Color[vertices.Length];
        uv = new Vector2[vertices.Length];
        triangles = new int[(path.Count - 1) * across * 6];
        for (int i = 0; i < path.Count; i++)
        {
            var tangent = path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)];
            var side = new Vector3(tangent.z, 0, -tangent.x).normalized;
            float halfWidth = Mathf.Lerp(2.5f, 1.65f, i / (float)path.Count);
            for (int j = 0; j <= across; j++)
            {
                float offset = (j / (float)across * 2 - 1) * halfWidth;
                offset += Mathf.Sin(i * 0.63f) * 0.09f * Mathf.Abs(offset / halfWidth);
                var p = path[i] + side * offset;
                p.y = Height(p.x, p.z) + 0.035f;
                int index = i * (across + 1) + j;
                vertices[index] = p;
                uv[index] = new Vector2(p.x, p.z) * 0.24f;
                colors[index] = Color.Lerp(new Color(0.77f, 0.59f, 0.27f), new Color(0.95f, 0.76f, 0.37f), Mathf.Sin(i * 0.12f) * 0.5f + 0.5f).linear;
            }
        }
        at = 0;
        for (int i = 0; i < path.Count - 1; i++)
        for (int j = 0; j < across; j++)
        {
            int index = i * (across + 1) + j;
            triangles[at++] = index; triangles[at++] = index + across + 1; triangles[at++] = index + 1;
            triangles[at++] = index + 1; triangles[at++] = index + across + 1; triangles[at++] = index + across + 2;
        }
        mesh = new Mesh { name = "Winding golden path", vertices = vertices, colors = colors, uv = uv, triangles = triangles };
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        MeshObject("S-curve • golden earth", mesh, GroundMaterial("Golden Path", "Road_Base_Color.png", 0.12f), group);
    }

    private static GameObject MeshObject(string name, Mesh mesh, Material material, Transform parent, bool collider = false)
    {
        string safeName = name.Replace('•', '-').Replace('/', '-');
        mesh = SaveAsset(mesh, Output + "/Generated/" + safeName + ".asset");
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        return go;
    }

    private static T SaveAsset<T>(T asset, string assetPath) where T : Object
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (existing != null)
        {
            if (asset is Mesh sourceMesh && existing is Mesh destinationMesh)
            {
                // CopySerialized alone does not reliably invalidate a loaded Mesh's GPU buffers.
                destinationMesh.Clear();
                destinationMesh.indexFormat = sourceMesh.indexFormat;
                destinationMesh.vertices = sourceMesh.vertices;
                destinationMesh.normals = sourceMesh.normals;
                destinationMesh.uv = sourceMesh.uv;
                destinationMesh.colors = sourceMesh.colors;
                destinationMesh.subMeshCount = sourceMesh.subMeshCount;
                for (int i = 0; i < sourceMesh.subMeshCount; i++)
                    destinationMesh.SetTriangles(sourceMesh.GetTriangles(i), i, false);
                destinationMesh.bounds = sourceMesh.bounds;
            }
            else if (asset is Material sourceMaterial && existing is Material destinationMaterial)
            {
                destinationMaterial.shader = sourceMaterial.shader;
                destinationMaterial.CopyPropertiesFromMaterial(sourceMaterial);
                destinationMaterial.enableInstancing = sourceMaterial.enableInstancing;
                destinationMaterial.renderQueue = sourceMaterial.renderQueue;
            }
            else EditorUtility.CopySerialized(asset, existing);
            Object.DestroyImmediate(asset);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(asset, assetPath);
        return asset;
    }

    private static Material CopyMaterial(Material source)
    {
        if (materials.TryGetValue(source.name, out var result)) return result;
        var copy = new Material(source) { name = source.name + " • Meadow", enableInstancing = true };
        // The pack already contains painted leaf normals and vertex colour wind masks.
        // Preserve its shader graph rather than replacing vegetation with generic primitives.
        if (source.name == "Big_Tree_Leaves")
        {
            copy.SetColor("_Color", new Color(0.78f, 0.67f, 0.73f, 1));
            copy.SetColor("_EmissionColor", new Color(0.58f, 0.37f, 0.50f, 1));
            copy.SetFloat("_EmissionPower", 7.17f); // Fresnel exponent, NOT emission brightness.
            copy.SetFloat("_BillboardSize", 3.29f);
        }
        if (source.name.StartsWith("Tree_leaves_") || source.name.StartsWith("Shrub_"))
        {
            SetColor(copy, "_Color", new Color(0.17f, 0.55f, 0.25f, 1));
            SetColor(copy, "_EmissionColor", new Color(0.22f, 0.56f, 0.17f, 1));
            if (copy.HasProperty("_EmissionPower")) copy.SetFloat("_EmissionPower", 5.3f);
        }
        if (source.name.StartsWith("Grass_"))
        {
            copy.shader = Shader.Find("CircuitRace/Ghibli Meadow/Sunlit Vegetation");
            // Colour properties are converted to linear by Unity; don't convert twice.
            copy.SetColor("_RootColor", new Color(0.25f, 0.50f, 0.30f));
            copy.SetColor("_TipColor", new Color(0.53f, 0.78f, 0.36f));
            copy.SetTexture("_Variation", AssetDatabase.LoadAssetAtPath<Texture2D>(Pack + "Textures/Grass Variation.png"));
            copy.SetFloat("_UseAtlas", 0);
            copy.SetFloat("_WindStrength", 0.045f);
            copy.renderQueue = 2450;
        }
        if (source.name == "Flowers")
        {
            copy.shader = Shader.Find("CircuitRace/Ghibli Meadow/Sunlit Vegetation");
            copy.SetTexture("_FlowerColor", source.GetTexture("_Flower_Color"));
            copy.SetTexture("_FlowerMask", source.GetTexture("_Flower_Mask"));
            copy.SetFloat("_UseAtlas", 1);
            copy.SetFloat("_WindStrength", 0.025f);
            copy.renderQueue = 2450;
        }
        result = SaveAsset(copy, Output + "/Materials/" + source.name + ".mat");
        materials.Add(source.name, result);
        return result;
    }

    private static void SetColor(Material material, string property, Color color)
    {
        if (material.HasProperty(property)) material.SetColor(property, color);
    }

    private static GameObject Place(string prefabName, Transform parent, float x, float z, float scale, float yaw = -999)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "Prefabs/" + prefabName + ".prefab");
        if (prefab == null) throw new FileNotFoundException("Missing GHIBLI prefab: " + prefabName);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        go.transform.SetParent(parent, false);
        go.transform.position = OnGround(x, z);
        go.transform.rotation = Quaternion.Euler(0, yaw < -900 ? Next(0, 360) : yaw, 0);
        go.transform.localScale = Vector3.one * scale;
        foreach (var renderer in go.GetComponentsInChildren<Renderer>())
            renderer.sharedMaterials = renderer.sharedMaterials.Select(CopyMaterial).ToArray();
        return go;
    }

    private static void MakeTrees()
    {
        var group = Group("02 • Ancient lavender tree and enclosing forest");
        var tree = Place("Big Tree", group, -9, 65, 1.58f, 18);
        tree.name = "Hero • ancient lavender tree";
        for (int row = 0; row < 3; row++)
        for (int i = 0; i < 52; i++)
        {
            float x = -103 + i * 4.05f + Next(-1.8f, 1.8f);
            float z = 91 + row * 7 + Next(-3, 3);
            int type = random.Next(1, 6);
            Place("Tree_0" + type, group, x, z, ForestScale(type));
        }
        for (int side = -1; side <= 1; side += 2)
        for (int row = 0; row < 2; row++)
        for (int i = 0; i < 17; i++)
        {
            float z = -16 + i * 7 + Next(-2, 2);
            float x = side * (57 + row * 11 + Next(-5, 4));
            int type = random.Next(1, 6);
            Place("Tree_0" + type, group, x, z, ForestScale(type));
        }
        for (int i = 0; i < 35; i++)
            Place("Tree_03", group, -102 + i * 6 + Next(-1, 1), 88 + Next(-3, 3), Next(1.4f, 1.9f));
        // A low understory hides bare trunks and closes the forest edge like the reference.
        for (int i = 0; i < 69; i++)
            Place("Shrubs_02", group, -103 + i * 3.0f + Next(-0.6f, 0.6f), 86 + Next(-1.5f, 1.5f), Next(2.7f, 3.5f));
        // Close silhouettes frame the view just like the first photograph.
        var framing = Place("Tree_01", group, -9, -3, 3.3f, 63);
        framing.name = "Foreground • left framing tree";
        framing.transform.rotation = Quaternion.AngleAxis(-9, Vector3.forward) * framing.transform.rotation;
        Place("Tree_02", group, 27, 10, 2.15f, 200).name = "Foreground • right framing tree";
        Place("Tree_02", group, 34, 14, 2.0f, 25);
        Place("Tree_05", group, 41, 17, 2.4f, 120);
    }

    private static float ForestScale(int type)
    {
        float[] nativeHeights = { 8.08f, 8.12f, 4.34f, 10.83f, 5.89f };
        return Next(8, 12) / nativeHeights[type - 1];
    }

    private static void MakeRocksAndShrubs()
    {
        var group = Group("03 • Mossy stones and rounded bushes");
        for (int i = 0; i < 65; i++)
        {
            float x = Next(-61, 61), z = Next(-20, 98);
            if (x > 0 && x < 27 && z > 11 && z < 32) continue;
            if (PathDistance(x, z) < 4 || Vector2.Distance(new Vector2(x, z), new Vector2(-9, 65)) < 7) continue;
            Place("Shrubs_0" + random.Next(1, 4), group, x, z, Next(0.9f, 2.2f));
        }
        for (int i = 0; i < 44; i++)
        {
            float x = Next(-55, 55), z = Next(-10, 90);
            if (x > 0 && x < 27 && z > 11 && z < 32) continue;
            if (PathDistance(x, z) < 3.8f) continue;
            Place("Rock_0" + random.Next(4, 8), group, x, z, Next(0.65f, 1.7f));
        }
        Place("Shrubs_02", group, -1, -10, 4.3f, 80);
        Place("Rock_10", group, 20, -3, 1.4f, 35);
        Place("Shrubs_02", group, 12.5f, 18.8f, 1.1f);
        Place("Rock_07", group, 8, 20.5f, 0.5f);
        Place("Rock_09", group, -24, 15, 0.8f);
    }

    private static void MakeFencesAndShrine()
    {
        var group = Group("04 • Broken timber fences and tree-side shrine");
        // Broken runs: neither side becomes a continuous corridor.
        for (int i = 43; i < path.Count - 17; i += 8)
        {
            if ((i / 8) % 5 == 1) continue;
            var tangent = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
            var side = new Vector3(tangent.z, 0, -tangent.x);
            float direction = (i / 8) % 3 == 0 ? 1 : -1;
            var p = path[i] + side * (3 + Next(0, 0.5f)) * direction;
            float yaw = Mathf.Atan2(tangent.x, tangent.z) * Mathf.Rad2Deg + 90 + Next(-5, 5);
            Place(random.NextDouble() < 0.65 ? "Fence_01" : "Fence_02", group, p.x, p.z, 1.15f, yaw);
        }
        for (int i = 0; i < 13; i++)
        {
            float angle = Mathf.Lerp(135, 390, i / 12f) * Mathf.Deg2Rad;
            float x = -9 + Mathf.Cos(angle) * 7.2f;
            float z = 65 + Mathf.Sin(angle) * 7.2f;
            Place("Fence_01", group, x, z, 1.1f, -angle * Mathf.Rad2Deg + 90);
        }
        var shrine = Place("Altar", group, -13, 58.5f, 0.95f, 180);
        shrine.name = "Small wooden shrine at the roots";
    }

    private static void MakeMeadow()
    {
        var group = Group("05 • Wind-swept grass and wildflower drifts (GPU instanced)");
        var prototypes = new List<GhibliMeadowFoliageData.Prototype>();
        var placements = new List<GhibliMeadowFoliageData.Placement>();
        var indices = new Dictionary<string, int[]>();
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            string[] grasses = { "Gras_01", "Gras_02", "Gras_03", "Gras_03" };
            string[] flowers = { "Flower_01_3", "Flower_02_3", "Flower_03_3", "Flower_04_3", "Flower_05_3", "Flower_06_2", "Flower_07_2", "Flower_08_2", "Flower_09_2", "Flower_10", "Flower_11", "Flower_12", "Flower_13" };
            int[] smallFlowers = { 0, 0, 0, 0, 3, 3, 4, 4, 1 };
            int[] foregroundFlowers = { 0, 0, 0, 3, 4 };
            for (int i = 0; i < 90000; i++)
            {
                float x = Next(-66, 66), z = Next(-27, 99);
                if (PathDistance(x, z) < GrassPathMargin(z) + Next(0.04f, 0.2f)) continue;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(-9, 65)) < 4.8f) continue;
                float scale = Next(1.1f, 1.8f);
                AddPlacement(grasses[random.Next(grasses.Length)], OnGround(x, z), scale, prototypes, placements, indices, preview);
            }
            for (int i = 0; i < 2500; i++)
            {
                float x = Next(-2, 30), z = Next(12, 35);
                if (PathDistance(x, z) < GrassPathMargin(z) + 0.15f) continue;
                AddPlacement("Gras_03", OnGround(x, z), Next(1.0f, 1.45f), prototypes, placements, indices, preview);
            }
            for (int i = 0; i < 16000; i++)
            {
                float x = Next(-60, 60), z = Next(-23, 95);
                if (PathDistance(x, z) < 2.85f) continue;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(-9, 65)) < 5.5f) continue;
                // Large, loose drifts rather than a uniformly spaced flower grid.
                float drift = Mathf.PerlinNoise(x * 0.09f + 10, z * 0.09f + 19);
                if (Next(0, 1) > drift + 0.2f) continue;
                int index = random.NextDouble() < 0.65 ? smallFlowers[random.Next(smallFlowers.Length)] : random.Next(5, flowers.Length);
                if (index == 8 && z < 28 && x > -4 && x < 27) index = 7;
                float scale = index < 5 || index >= 9 ? Next(1.4f, 2.2f) : Next(0.65f, 1.0f);
                AddPlacement(flowers[index], OnGround(x, z), scale, prototypes, placements, indices, preview);
            }
            // A close drift of daisies gives the low reference viewpoint its flower-filled foreground.
            for (int i = 0; i < 240; i++)
            {
                float x = Next(3, 24), z = Next(16, 30);
                if (PathDistance(x, z) < 2.9f) continue;
                AddPlacement(flowers[foregroundFlowers[random.Next(foregroundFlowers.Length)]], OnGround(x, z), Next(1.6f, 2.3f),
                    prototypes, placements, indices, preview);
            }
            var data = ScriptableObject.CreateInstance<GhibliMeadowFoliageData>();
            data.prototypes = prototypes.ToArray();
            data.placements = placements.ToArray();
            data = SaveAsset(data, Output + "/Meadow Foliage.asset");
            var foliage = group.gameObject.AddComponent<GhibliMeadowFoliage>();
            foliage.data = data;
            foliage.Rebuild();
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }

    private static void AddPlacement(string prefabName, Vector3 position, float scale,
        List<GhibliMeadowFoliageData.Prototype> prototypes, List<GhibliMeadowFoliageData.Placement> placements,
        Dictionary<string, int[]> indices, Scene preview)
    {
        if (!indices.TryGetValue(prefabName, out var prototypeIndices))
        {
            var template = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(Pack + "Prefabs/" + prefabName + ".prefab"), preview);
            var newIndices = new List<int>();
            foreach (var filter in template.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || filter.sharedMesh == null) continue;
                for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
                {
                    newIndices.Add(prototypes.Count);
                    prototypes.Add(new GhibliMeadowFoliageData.Prototype
                    {
                        mesh = filter.sharedMesh, subMesh = sub,
                        material = CopyMaterial(renderer.sharedMaterials[Mathf.Min(sub, renderer.sharedMaterials.Length - 1)]),
                        localTransform = template.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix
                    });
                }
            }
            prototypeIndices = newIndices.ToArray();
            indices.Add(prefabName, prototypeIndices);
            Object.DestroyImmediate(template);
        }
        float yaw = Next(0, 360);
        var size = new Vector3(scale, scale * Next(0.85f, 1.2f), scale);
        foreach (int index in prototypeIndices)
            placements.Add(new GhibliMeadowFoliageData.Placement { prototype = index, position = position, yaw = yaw, scale = size });
    }

    private static float GrassPathMargin(float z) => Mathf.Lerp(2.5f, 1.65f, Mathf.InverseLerp(-20, 71, z));

    private static void MakeMountains()
    {
        var group = Group("06 • Layered distant mountain silhouettes");
        var material = GroundMaterial("Hazy Mountains", "Rock_001_Base_Color.png", 0.12f);
        for (int layer = 0; layer < 2; layer++)
        for (int i = 0; i < 13; i++)
        {
            float x = -210 + i * 35 + Next(-9, 9);
            float z = 180 + layer * 65 + Next(-10, 10);
            float width = Next(30, 54), height = Next(23, 43);
            if (i == 9) height = 58;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            const int columns = 14, rows = 8;
            float depth = width * Next(0.45f, 0.7f);
            float lean = Next(-0.18f, 0.18f);
            var baseColor = Color.Lerp(new Color(0.55f, 0.63f, 0.65f), new Color(0.72f, 0.75f, 0.76f), layer).linear;
            for (int row = 0; row <= rows; row++)
            for (int column = 0; column <= columns; column++)
            {
                float u = column / (float)columns, v = row / (float)rows * 2 - 1;
                float ridge = Mathf.Max(0, 1 - Mathf.Abs((u - 0.55f - v * lean) / 0.43f));
                ridge = Mathf.Max(ridge, Mathf.Max(0, 0.56f - Mathf.Abs((u - 0.23f) / 0.36f)));
                ridge *= Mathf.Pow(1 - Mathf.Abs(v), 0.65f);
                ridge *= 0.9f + Mathf.PerlinNoise(column * 0.78f + i * 13, row * 0.6f + layer * 9) * 0.2f;
                vertices.Add(new Vector3(x + (u * 2 - 1) * width, -5 + ridge * height, z + v * depth));
                colors.Add(Color.Lerp(baseColor, new Color(0.84f, 0.85f, 0.82f).linear,
                    Mathf.SmoothStep(0.65f, 1, ridge) * 0.4f));
            }
            for (int row = 0; row < rows; row++)
            for (int column = 0; column < columns; column++)
            {
                int a = row * (columns + 1) + column;
                triangles.Add(a); triangles.Add(a + columns + 1); triangles.Add(a + 1);
                triangles.Add(a + 1); triangles.Add(a + columns + 1); triangles.Add(a + columns + 2);
            }
            // Unshared vertices retain broad, painterly rock planes instead of smooth cones.
            var flatVertices = triangles.Select(t => vertices[t]).ToArray();
            var flatColors = triangles.Select(t => colors[t]).ToArray();
            var mesh = new Mesh { name = "Mountain " + layer + "-" + i };
            mesh.vertices = flatVertices; mesh.colors = flatColors;
            mesh.triangles = Enumerable.Range(0, flatVertices.Length).ToArray();
            mesh.uv = flatVertices.Select(p => new Vector2(p.x, p.y) * 0.02f).ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            MeshObject(mesh.name, mesh, material, group).GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    private sealed class LightSettings
    {
        private Quaternion rotation;
        private Color color;
        private float intensity, colorTemperature, shadowStrength, shadowBias, shadowNormalBias;
        private LightShadows shadows;
        private bool enabled, active, useColorTemperature;

        public static LightSettings Capture(GameObject existingRoot, string prefix)
        {
            if (existingRoot == null) return null;
            var light = existingRoot.GetComponentsInChildren<Light>(true)
                .FirstOrDefault(l => l.type == LightType.Directional &&
                    l.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (light == null) return null;
            return new LightSettings
            {
                rotation = light.transform.rotation,
                color = light.color,
                intensity = light.intensity,
                colorTemperature = light.colorTemperature,
                useColorTemperature = light.useColorTemperature,
                shadows = light.shadows,
                shadowStrength = light.shadowStrength,
                shadowBias = light.shadowBias,
                shadowNormalBias = light.shadowNormalBias,
                enabled = light.enabled,
                active = light.gameObject.activeSelf
            };
        }

        public void Apply(Light light)
        {
            light.transform.rotation = rotation;
            light.color = color;
            light.intensity = intensity;
            light.colorTemperature = colorTemperature;
            light.useColorTemperature = useColorTemperature;
            light.shadows = shadows;
            light.shadowStrength = shadowStrength;
            light.shadowBias = shadowBias;
            light.shadowNormalBias = shadowNormalBias;
            light.enabled = enabled;
            light.gameObject.SetActive(active);
        }
    }

    private static void MakeLighting(LightSettings sunSettings, LightSettings fillSettings)
    {
        var group = Group("07 • Warm sunlight, blue shadows and soft atmospheric haze");
        var sun = new GameObject("Meadow sun • upper right", typeof(Light));
        sun.transform.SetParent(group, false);
        sun.transform.rotation = Quaternion.Euler(38, -140, 0);
        var light = sun.GetComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1, 0.94f, 0.83f);
        light.intensity = 1.8f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.8f;
        light.shadowBias = 0.035f;
        light.shadowNormalBias = 0.3f;
        sunSettings?.Apply(light);
        var fill = new GameObject("Cool soft fill • sky bounce", typeof(Light));
        fill.transform.SetParent(group, false);
        fill.transform.rotation = Quaternion.Euler(30, -30, 0);
        var fillLight = fill.GetComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.color = new Color(0.8f, 0.86f, 1);
        fillLight.intensity = 0.65f;
        fillLight.shadows = LightShadows.None;
        fillSettings?.Apply(fillLight);
        RenderSettings.sun = light;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.5f, 0.63f, 0.78f);
        RenderSettings.ambientEquatorColor = new Color(0.28f, 0.38f, 0.44f);
        RenderSettings.ambientGroundColor = new Color(0.16f, 0.22f, 0.17f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.73f, 0.81f, 0.79f);
        RenderSettings.fogStartDistance = 85;
        RenderSettings.fogEndDistance = 300;
        var sky = new Material(Shader.Find("CircuitRace/Ghibli Meadow/Soft Cloud Sky")) { name = "Meadow Cloud Sky" };
        sky.SetColor("_Zenith", new Color(0.08f, 0.27f, 0.43f));
        sky.SetColor("_Horizon", new Color(0.39f, 0.61f, 0.70f));
        sky.SetColor("_Cloud", new Color(0.95f, 0.95f, 0.88f));
        RenderSettings.skybox = SaveAsset(sky, Output + "/Materials/Meadow Cloud Sky.mat");
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "Meadow Atmosphere";
        var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
        var bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(1.0f); bloom.intensity.Override(0.22f); bloom.scatter.Override(0.65f);
        var grading = profile.Add<ColorAdjustments>(true);
        grading.postExposure.Override(0.25f); grading.contrast.Override(5); grading.saturation.Override(6);
        var whiteBalance = profile.Add<WhiteBalance>(true); whiteBalance.temperature.Override(4);
        var vignette = profile.Add<Vignette>(true); vignette.intensity.Override(0.08f); vignette.smoothness.Override(0.75f);
        profile = SaveProfile(profile);
        var volume = new GameObject("Meadow colour grade", typeof(Volume));
        volume.transform.SetParent(group, false);
        volume.layer = 2; // Existing Ignore Raycast layer: isolate this grade from other loaded scenes.
        var component = volume.GetComponent<Volume>();
        component.isGlobal = true; component.priority = 100; component.sharedProfile = profile;
    }

    private static VolumeProfile SaveProfile(VolumeProfile profile)
    {
        string assetPath = Output + "/Meadow Atmosphere.asset";
        var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(assetPath);
        if (existing != null)
        {
            foreach (var old in existing.components.ToArray()) Object.DestroyImmediate(old, true);
            existing.components.Clear();
            foreach (var component in profile.components)
            {
                existing.components.Add(component); AssetDatabase.AddObjectToAsset(component, existing);
            }
            profile.components.Clear();
            Object.DestroyImmediate(profile);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        AssetDatabase.CreateAsset(profile, assetPath);
        foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile);
        return profile;
    }

    private static Camera[] MakeCameras()
    {
        var group = Group("08 • Optional reference viewpoints (disabled for gameplay)");
        var a = MakeCamera("Reference Camera • winding path (reference 1)", group,
            new Vector3(7, Height(7, -20) + 4.7f, -20), new Vector3(4, 7.5f, 65), 57);
        a.enabled = false;
        var b = MakeCamera("Reference Camera • flower meadow (reference 2)", group,
            new Vector3(13, Height(13, 13) + 1.65f, 13), new Vector3(-1, 4.5f, 65), 64);
        b.enabled = false;
        return new[] { a, b };
    }

    private static Camera MakeCamera(string name, Transform parent, Vector3 position, Vector3 target, float fov)
    {
        var go = new GameObject(name, typeof(Camera));
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.LookAt(target);
        var camera = go.GetComponent<Camera>();
        camera.fieldOfView = fov; camera.nearClipPlane = 0.15f; camera.farClipPlane = 650;
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.allowHDR = true; camera.allowMSAA = true; camera.depth = 10;
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.volumeLayerMask = 1 << 2;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.High;
        return camera;
    }

    private static void MakePetals()
    {
        var group = Group("09 • Drifting petals");
        var go = new GameObject("Slow airborne petals", typeof(ParticleSystem));
        go.transform.SetParent(group, false);
        go.transform.position = new Vector3(-3, 9, 40);
        var system = go.GetComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.duration = 18; main.loop = true; main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(12, 18);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.055f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.58f, 0.26f, 0.15f, 0.75f), new Color(0.98f, 0.72f, 0.53f, 0.65f));
        main.maxParticles = 160;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = system.emission; emission.rateOverTime = 7;
        var shape = system.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(75, 14, 90);
        var velocity = system.velocityOverLifetime; velocity.enabled = true;
        velocity.x = 0.45f; velocity.y = -0.12f; velocity.z = -0.2f;
        var noise = system.noise; noise.enabled = true; noise.strength = 0.25f; noise.frequency = 0.2f;
        var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Soft Petals" };
        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0); material.renderQueue = 3000;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        var particleRenderer = go.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = SaveAsset(material, Output + "/Materials/Soft Petals.mat");
        var petal = new Mesh
        {
            name = "Tiny wind petal",
            vertices = new[] { new Vector3(-0.5f,-0.2f,0), new Vector3(0.5f,-0.1f,0), new Vector3(0,0.7f,0) },
            triangles = new[] { 0, 1, 2, 0, 2, 1 },
            uv = new[] { Vector2.zero, Vector2.right, Vector2.up }
        };
        petal.RecalculateNormals(); petal.RecalculateBounds();
        particleRenderer.renderMode = ParticleSystemRenderMode.Mesh;
        particleRenderer.mesh = SaveAsset(petal, Output + "/Generated/Tiny wind petal.asset");
        system.Play();
    }

    private static void Capture(Camera camera, string file)
    {
        var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        ulong previousMask = camera.overrideSceneCullingMask;
        ulong previousSceneMask = EditorSceneManager.GetSceneCullingMask(scene);
        ulong isolatedMask = EditorSceneManager.CalculateAvailableSceneCullingMask();
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            // Loaded, normal scenes share the default mask. Give Testing a temporary,
            // unique mask so an unrelated open scene cannot appear in the preview.
            EditorSceneManager.SetSceneCullingMask(scene, isolatedMask);
            camera.overrideSceneCullingMask = isolatedMask;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes(file, image.EncodeToPNG());
        }
        finally
        {
            camera.overrideSceneCullingMask = previousMask;
            EditorSceneManager.SetSceneCullingMask(scene, previousSceneMask);
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(image);
            RenderTexture.ReleaseTemporary(target);
        }
    }

    private static string Validate()
    {
        var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        int missing = root.GetComponentsInChildren<Transform>(true).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
        int bad = renderers.SelectMany(r => r.sharedMaterials).Count(m => m == null || m.shader == null || !m.shader.isSupported || ShaderUtil.ShaderHasError(m.shader));
        long vertices = root.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh != null).Sum(f => (long)f.sharedMesh.vertexCount);
        var foliage = root.GetComponentInChildren<GhibliMeadowFoliage>();
        int instances = foliage != null && foliage.data != null ? foliage.data.placements.Length : 0;
        if (foliage != null && foliage.data != null)
            bad += foliage.data.prototypes.Count(p => p.mesh == null || p.material == null || ShaderUtil.ShaderHasError(p.material.shader));
        return "Scene: " + ScenePath + "\nRoot: " + RootName + "\nMesh renderers: " + renderers.Length +
               "\nEnabled mesh renderers: " + renderers.Count(r => r.enabled && r.gameObject.activeInHierarchy) +
               "\nStatic vertices: " + vertices + "\nInstanced foliage placements: " + instances + "\nMissing scripts: " + missing + "\nInvalid materials: " + bad +
               "\nActive scene cameras: " + scene.GetRootGameObjects()
                   .SelectMany(g => g.GetComponentsInChildren<Camera>(true))
                   .Count(c => c.enabled && c.gameObject.activeInHierarchy) + "\n";
    }

    [MenuItem("Tools/Ghibli Meadow/Refresh Preview Images")]
    public static void RefreshPreviews()
    {
        var previous = SceneManager.GetActiveScene();
        scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            root = scene.GetRootGameObjects().First(g => g.name == RootName).transform;
            var cameras = root.GetComponentsInChildren<Camera>();
            Capture(cameras[0], Output + "/Preview-Path.png");
            Capture(cameras[1], Output + "/Preview-Meadow.png");
            File.WriteAllText("Temp/GhibliMeadow.validation.txt", Validate());
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            AssetDatabase.Refresh();
        }
    }
}
