using UnityEngine;

public class CarSpawner : MonoBehaviour
{
    [SerializeField] private GameObject[] carsPrefab;
    [SerializeField] private ChaseCamera cameraScript;

    public GameObject SpawnedCar { get; private set; }
    public bool SpawnAttempted { get; private set; }

    void Start()
    {
        TrySpawnCar();
    }

    // Repeated requests reuse the same car instead of creating a second player.
    public bool TrySpawnCar()
    {
        if (SpawnedCar != null) return true;
        if (SpawnAttempted) return false;
        SpawnAttempted = true;
        if (carsPrefab == null || carsPrefab.Length == 0)
        {
            Debug.LogError("CarSpawner: No player car prefabs are assigned on " + name + ".", this);
            return false;
        }

        int savedIndex = PlayerPrefs.GetInt(CarSelection.SelectionKey, 0);
        int index = Mathf.Clamp(savedIndex, 0, carsPrefab.Length - 1);
        if (!IsUsablePlayerPrefab(carsPrefab[index]))
        {
            index = -1;
            for (int i = 0; i < carsPrefab.Length; i++)
            {
                if (!IsUsablePlayerPrefab(carsPrefab[i])) continue;
                index = i;
                break;
            }
            if (index < 0)
            {
                Debug.LogError("CarSpawner: No usable player prefab on " + name +
                    ". Assign a car with a CarController, Rigidbody and four wheel colliders.", this);
                return false;
            }
        }
        if (index != savedIndex)
        {
            PlayerPrefs.SetInt(CarSelection.SelectionKey, index);
            PlayerPrefs.Save();
        }

        SpawnedCar = Instantiate(
            carsPrefab[index],
            transform.position,
            transform.rotation
        );
        SpawnedCar.SetActive(true);

        Rigidbody rb = SpawnedCar.GetComponent<Rigidbody>();

        if (cameraScript == null && Camera.main != null) cameraScript = Camera.main.GetComponent<ChaseCamera>();
        if (cameraScript != null)
        {
            cameraScript.enabled = true;
            cameraScript.target = SpawnedCar.transform;
            cameraScript.targetRb = rb;
            cameraScript.SnapToTarget();
        }

        Skidmarks skidmarksController = FindAnyObjectByType<Skidmarks>();

        WheelSkid[] wheelSkids = SpawnedCar.GetComponentsInChildren<WheelSkid>(true);

        foreach (WheelSkid wheelSkid in wheelSkids)
        {
            wheelSkid.SetRigidbody(rb);
            wheelSkid.SetSkidmarksController(skidmarksController);
        }
        return true;
    }

    static bool IsUsablePlayerPrefab(GameObject prefab)
    {
        if (prefab == null || prefab.GetComponent<Rigidbody>() == null) return false;
        CarController car = prefab.GetComponent<CarController>();
        return car != null && car.frontLeftCollider != null && car.frontRightCollider != null &&
            car.rearLeftCollider != null && car.rearRightCollider != null;
    }
}
