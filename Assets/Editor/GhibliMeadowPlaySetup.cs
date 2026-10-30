using System;
using System.IO;
using System.Linq;
using CircuitRace.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class GhibliMeadowPlaySetup
{
    private const string ScenePath = "Assets/Scenes/Testing.unity";

    [MenuItem("Tools/Ghibli Meadow/Enable Driving and Free Camera in Testing")]
    public static void SetupTestingScene()
    {
        var previous = SceneManager.GetActiveScene();
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            if (scene.isDirty)
            {
                string backup = "Temp/Testing-before-player-setup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity";
                if (!EditorSceneManager.SaveScene(scene, backup, true))
                    throw new IOException("Could not back up the unsaved Testing scene.");
                File.WriteAllText("Temp/MeadowPlayerSetup.backup.txt", backup);
            }
            var meadow = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "GHIBLI Reference Meadow");
            if (meadow == null) throw new InvalidOperationException("Build the meadow in Testing first.");
            Ensure(scene, meadow.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            File.WriteAllText("Temp/MeadowPlayerSetup.validation.txt", Validate(scene));
        }
        finally
        {
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if (openedHere && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    public static void Ensure(Scene scene, Transform meadow)
    {
        foreach (var reference in meadow.GetComponentsInChildren<Camera>(true))
        {
            reference.enabled = false;
            reference.tag = "Untagged";
            if (reference.name.StartsWith("Main Camera")) reference.name = reference.name.Replace("Main Camera", "Reference Camera");
            foreach (var listener in reference.GetComponents<AudioListener>()) listener.enabled = false;
        }
        var roots = scene.GetRootGameObjects();
        if (roots.SelectMany(g => g.GetComponentsInChildren<MeadowMapControls>(true)).Any()) return;

        var camera = roots.Where(g => !g.transform.IsChildOf(meadow))
            .SelectMany(g => g.GetComponentsInChildren<ChaseCamera>(true))
            .FirstOrDefault(c => !c.transform.IsChildOf(meadow));
        if (camera == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Main Camera.prefab");
            if (prefab == null) throw new FileNotFoundException("Main Camera prefab is missing.");
            camera = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<ChaseCamera>();
        }
        var view = camera.GetComponent<Camera>();
        camera.gameObject.SetActive(true);
        view.enabled = true;
        view.tag = "MainCamera";
        view.depth = 0;
        view.farClipPlane = Mathf.Max(view.farClipPlane, 650);
        camera.enabled = true;
        camera.holdPosition = false;
        var audio = camera.GetComponent<AudioListener>();
        if (audio == null) audio = camera.gameObject.AddComponent<AudioListener>();
        audio.enabled = true;
        var data = camera.GetComponent<UniversalAdditionalCameraData>();
        if (data == null) data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        data.enabled = true;
        data.volumeLayerMask = 1 << 2;
        data.renderPostProcessing = true;
        var fly = camera.GetComponent<MeadowFreeFlyCamera>();
        if (fly == null) fly = camera.gameObject.AddComponent<MeadowFreeFlyCamera>();
        fly.enabled = false;

        var setupObject = new GameObject("Meadow Gameplay • driving + free camera");
        SceneManager.MoveGameObjectToScene(setupObject, scene);
        var controls = setupObject.AddComponent<MeadowMapControls>();
        controls.playerCamera = view;
        controls.chaseCamera = camera;
        controls.freeFlyCamera = fly;
        controls.playerCar = roots.SelectMany(g => g.GetComponentsInChildren<CarController>(true))
            .FirstOrDefault(c => c.gameObject.activeInHierarchy && c.enabled);

        var spawner = roots.SelectMany(g => g.GetComponentsInChildren<CarSpawner>(true))
            .FirstOrDefault(s => s.enabled && s.gameObject.activeInHierarchy);
        bool createdSpawner = false;
        if (spawner == null && controls.playerCar == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Garage Scene/CarSpawner.prefab");
            if (prefab == null) throw new FileNotFoundException("Existing player car spawner prefab is missing.");
            var spawnObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            spawnObject.name = "Player Car Spawn • move this to change the start";
            spawnObject.transform.SetParent(setupObject.transform, true);
            spawner = spawnObject.GetComponent<CarSpawner>();
            createdSpawner = true;
            var ground = meadow.GetComponentsInChildren<MeshCollider>(true)
                .FirstOrDefault(c => c.name.StartsWith("Rolling meadow"));
            Physics.SyncTransforms();
            if (ground == null) throw new InvalidOperationException("The meadow ground collider is missing.");
            var start = meadow.TransformPoint(new Vector3(9, 0, -12));
            var origin = new Vector3(start.x, ground.bounds.max.y + 25, start.z);
            if (!ground.Raycast(new Ray(origin, Vector3.down), out var hit, ground.bounds.size.y + 50))
                throw new InvalidOperationException("No walkable meadow ground beneath the player spawn.");
            spawnObject.transform.position = hit.point + Vector3.up * 0.6f;
            spawnObject.transform.rotation = meadow.rotation * Quaternion.Euler(0, -15, 0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(spawnObject.transform);
        }
        if (spawner != null)
        {
            // An already placed, active player must not be duplicated by the spawner's Start.
            if (controls.playerCar != null) spawner.enabled = false;
            var serialized = new SerializedObject(spawner);
            serialized.FindProperty("cameraScript").objectReferenceValue = camera;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawner);
            if (PrefabUtility.IsPartOfPrefabInstance(spawner)) PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);
            controls.carSpawner = spawner;
            if (createdSpawner)
            {
                camera.transform.position = spawner.transform.position +
                    Quaternion.Euler(8, spawner.transform.eulerAngles.y, 0) * camera.offset;
                camera.transform.LookAt(spawner.transform.position + Vector3.up * camera.lookHeight);
            }
        }
        if (controls.playerCar != null)
        {
            camera.target = controls.playerCar.transform;
            camera.targetRb = controls.playerCar.GetComponent<Rigidbody>();
            var meshes = controls.playerCar.GetComponentsInChildren<Renderer>(true)
                .Where(r => r.gameObject.activeInHierarchy && (r is MeshRenderer || r is SkinnedMeshRenderer)).ToArray();
            // Repair the earlier blanket-disable only if the entire vehicle is invisible.
            // Otherwise preserve individual renderer choices made by the user.
            if (!meshes.Any(r => r.enabled))
                foreach (var renderer in meshes)
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
                    if (source != null && !source.enabled) continue;
                    renderer.enabled = true;
                    EditorUtility.SetDirty(renderer);
                    if (PrefabUtility.IsPartOfPrefabInstance(renderer)) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
        }
        foreach (var component in new Component[] { camera.transform, camera, view, audio, data, fly })
        {
            EditorUtility.SetDirty(component);
            if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
        EditorUtility.SetDirty(controls);
    }

    public static string Validate(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var controls = roots.SelectMany(g => g.GetComponentsInChildren<MeadowMapControls>(true)).Single();
        var cameras = roots.SelectMany(g => g.GetComponentsInChildren<Camera>(true)).ToArray();
        if (controls.playerCamera == null || !controls.playerCamera.enabled || controls.chaseCamera == null ||
            !controls.chaseCamera.enabled || controls.freeFlyCamera == null || controls.freeFlyCamera.enabled)
            throw new InvalidOperationException("Gameplay camera is not configured for driving with optional free flight.");
        if (cameras.Count(c => c.enabled && c.gameObject.activeInHierarchy && c.CompareTag("MainCamera")) != 1)
            throw new InvalidOperationException("Testing must have exactly one active MainCamera.");
        var meadow = roots.First(g => g.name == "GHIBLI Reference Meadow");
        if (meadow.GetComponentsInChildren<Camera>(true).Any(c => c.enabled))
            throw new InvalidOperationException("A reference camera is still overriding gameplay.");
        if (!meadow.GetComponentsInChildren<MeshCollider>().Any(c => c.enabled && c.name.StartsWith("Rolling meadow")))
            throw new InvalidOperationException("Meadow ground collider is not available.");
        int usable = 0;
        if (controls.playerCar != null)
        {
            var car = controls.playerCar;
            var body = car.GetComponent<Rigidbody>();
            if (body == null || body.isKinematic || car.frontLeftCollider == null || car.frontRightCollider == null ||
                car.rearLeftCollider == null || car.rearRightCollider == null)
                throw new InvalidOperationException("The existing player needs a dynamic Rigidbody and all four wheel colliders.");
        }
        if (controls.carSpawner != null)
        {
            var serialized = new SerializedObject(controls.carSpawner);
            var prefabs = serialized.FindProperty("carsPrefab");
            for (int i = 0; i < prefabs.arraySize; i++)
            {
                var prefab = prefabs.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                var car = prefab != null ? prefab.GetComponent<CarController>() : null;
                if (car != null && car.GetComponent<Rigidbody>() != null && car.frontLeftCollider != null &&
                    car.frontRightCollider != null && car.rearLeftCollider != null && car.rearRightCollider != null) usable++;
            }
            if (serialized.FindProperty("cameraScript").objectReferenceValue != controls.chaseCamera)
                throw new InvalidOperationException("Car spawner is not bound to the gameplay camera.");
        }
        if (usable == 0 && controls.playerCar == null) throw new InvalidOperationException("No usable player car assigned.");
        int listeners = roots.SelectMany(g => g.GetComponentsInChildren<AudioListener>(true))
            .Count(l => l.enabled && l.gameObject.activeInHierarchy);
        if (listeners != 1) throw new InvalidOperationException("Testing must have one active audio listener.");
        string player = controls.playerCar != null ? "existing player " + controls.playerCar.name : usable + " usable spawn prefabs";
        return "PASS: one active gameplay camera/listener; screenshot cameras disabled.\n" +
               "PASS: chase camera and free-flight switch are bound to " + player + ".\n" +
               "PASS: dynamic car physics, four wheel colliders and collidable meadow ground.\n";
    }
}
