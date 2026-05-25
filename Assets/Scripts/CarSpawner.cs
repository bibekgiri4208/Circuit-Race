using UnityEngine;

public class CarSpawner : MonoBehaviour
{
    [SerializeField] GameObject[] carsPrefab;
    [SerializeField] ChaseCamera cameraScript;

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

        //  HARD SAFETY CLAMP
        index = Mathf.Clamp(index, 0, carsPrefab.Length - 1);

        GameObject car = Instantiate(
            carsPrefab[index],
            transform.position,
            transform.rotation
        );

        Rigidbody rb = car.GetComponent<Rigidbody>();

        if (cameraScript != null)
        {
            cameraScript.target = car.transform;
            cameraScript.targetRb = rb;
        }
    }
}