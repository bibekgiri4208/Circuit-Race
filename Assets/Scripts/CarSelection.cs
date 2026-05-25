using UnityEngine;
using UnityEngine.SceneManagement;

public class CarSelection : MonoBehaviour
{
    [SerializeField] GameObject[] cars;

    int currentCarIndex;

    void Awake()
    {
        //  Validate array
        if (cars == null || cars.Length == 0)
        {
            Debug.LogError("No cars assigned in CarSelection!");
            return;
        }

        //  Load + clamp saved index
        currentCarIndex = PlayerPrefs.GetInt("CarIndexValue", 0);
        currentCarIndex = Mathf.Clamp(currentCarIndex, 0, cars.Length - 1);
    }

    void Start()
    {
        ShowCar(currentCarIndex);
    }

    public void NextCar()
    {
        if (cars.Length == 0) return;

        currentCarIndex = (currentCarIndex + 1) % cars.Length;
        ShowCar(currentCarIndex);
    }

    public void PreviousCar()
    {
        if (cars.Length == 0) return;

        currentCarIndex--;
        if (currentCarIndex < 0)
            currentCarIndex = cars.Length - 1;

        ShowCar(currentCarIndex);
    }

    void ShowCar(int index)
    {
        if (cars == null || cars.Length == 0) return;

        index = Mathf.Clamp(index, 0, cars.Length - 1);

        for (int i = 0; i < cars.Length; i++)
        {
            if (cars[i] != null)
                cars[i].SetActive(i == index);
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