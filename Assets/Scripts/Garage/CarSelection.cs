using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class CarSelection : MonoBehaviour
{
    [System.Serializable]
    public class CarInfo
    {
        public string carName;
    }

    [Header("Cars")]
    [SerializeField] private GameObject[] cars;

    [Header("Car Info")]
    [SerializeField] private CarInfo[] carInfos;

    [Header("3D Car Name Text")]
    [SerializeField] private TMP_Text carNameText;

    int currentCarIndex;

    void Awake()
    {
        if (cars == null || cars.Length == 0)
        {
            Debug.LogError("No cars assigned in CarSelection!");
            return;
        }

        currentCarIndex = PlayerPrefs.GetInt("CarIndexValue", 0);
        currentCarIndex = Mathf.Clamp(currentCarIndex, 0, cars.Length - 1);
    }

    void Start()
    {
        ShowCar(currentCarIndex);
    }

    public void NextCar()
    {
        if (cars == null || cars.Length == 0) return;

        currentCarIndex = (currentCarIndex + 1) % cars.Length;
        ShowCar(currentCarIndex);
    }

    public void PreviousCar()
    {
        if (cars == null || cars.Length == 0) return;

        currentCarIndex--;

        if (currentCarIndex < 0)
            currentCarIndex = cars.Length - 1;

        ShowCar(currentCarIndex);
    }

    void ShowCar(int index)
    {
        if (cars == null || cars.Length == 0) return;

        index = Mathf.Clamp(index, 0, cars.Length - 1);
        currentCarIndex = index;

        for (int i = 0; i < cars.Length; i++)
        {
            if (cars[i] != null)
                cars[i].SetActive(i == index);
        }

        UpdateCarName(index);
    }

    void UpdateCarName(int index)
    {
        if (carInfos == null || carInfos.Length == 0)
        {
            Debug.LogWarning("No car info assigned!");
            return;
        }

        if (index >= carInfos.Length)
        {
            Debug.LogWarning("Car name missing for car index: " + index);
            return;
        }

        if (carNameText != null)
        {
            carNameText.text = carInfos[index].carName.ToUpper();
        }
    }

    public void PlayButton()
    {
        if (cars == null || cars.Length == 0) return;

        currentCarIndex = Mathf.Clamp(currentCarIndex, 0, cars.Length - 1);

        PlayerPrefs.SetInt("CarIndexValue", currentCarIndex);
        PlayerPrefs.Save();

        SceneManager.LoadScene("Level1");
    }
}