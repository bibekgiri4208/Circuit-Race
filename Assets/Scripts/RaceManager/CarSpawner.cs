using UnityEngine;

public class CarSpawner : MonoBehaviour
{
    [SerializeField] private GameObject[] carsPrefab;
    [SerializeField] private ChaseCamera cameraScript;

    public GameObject SpawnedCar { get; private set; }

    void Start()
    {
        SpawnCar();
    }

    void SpawnCar()
    {
        if (carsPrefab == null || carsPrefab.Length == 0)
        {
            Debug.LogError("No car prefabs assigned in CarSpawner!");
            return;
        }

        int index = PlayerPrefs.GetInt("CarIndexValue", 0);
        index = Mathf.Clamp(index, 0, carsPrefab.Length - 1);

        if (carsPrefab[index] == null)
        {
            Debug.LogError("Car prefab missing at Element " + index);
            return;
        }

        SpawnedCar = Instantiate(
            carsPrefab[index],
            transform.position,
            transform.rotation
        );

        Rigidbody rb = SpawnedCar.GetComponent<Rigidbody>();

        if (cameraScript != null)
        {
            cameraScript.target = SpawnedCar.transform;
            cameraScript.targetRb = rb;
        }

        Skidmarks skidmarksController = FindAnyObjectByType<Skidmarks>();

        WheelSkid[] wheelSkids = SpawnedCar.GetComponentsInChildren<WheelSkid>(true);

        foreach (WheelSkid wheelSkid in wheelSkids)
        {
            wheelSkid.SetRigidbody(rb);
            wheelSkid.SetSkidmarksController(skidmarksController);
        }
    }
}