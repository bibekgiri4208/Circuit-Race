using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;
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

    [Header("UI Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject trackSelectionPanel;

    [Header("UI Navigation")]
    [SerializeField] private Button mainMenuDefaultButton;
    [SerializeField] private Button trackSelectionDefaultButton;

    private MenuNavigation menuNavigation;

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

        Canvas canvas = mainMenuPanel != null ? mainMenuPanel.GetComponentInParent<Canvas>() : null;
        if (canvas != null)
        {
            menuNavigation = canvas.GetComponent<MenuNavigation>();
            if (menuNavigation == null)
                menuNavigation = canvas.gameObject.AddComponent<MenuNavigation>();
            menuNavigation.Initialize(canvas.gameObject, mainMenuDefaultButton);
            menuNavigation.Focus();
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        Gamepad gamepad = Gamepad.current;
        if (gamepad != null && mainMenuPanel != null && mainMenuPanel.activeInHierarchy)
        {
            if (gamepad.leftShoulder.wasPressedThisFrame)
                PreviousCar();
            else if (gamepad.rightShoulder.wasPressedThisFrame)
                NextCar();
        }

        bool backPressed = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) ||
            (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
        if (backPressed && trackSelectionPanel != null && trackSelectionPanel.activeInHierarchy)
            BackToMainMenu();
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

        // Save the car choice right away when entering the track menu
        currentCarIndex = Mathf.Clamp(currentCarIndex, 0, cars.Length - 1);
        PlayerPrefs.SetInt("CarIndexValue", currentCarIndex);
        PlayerPrefs.Save();

        // Switch panels: Hide main menu, show track choices
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (trackSelectionPanel != null) trackSelectionPanel.SetActive(true);
        if (menuNavigation != null) menuNavigation.Focus(trackSelectionDefaultButton);
    }

    // Call this from the "Back" button in the track panel
    public void BackToMainMenu()
    {
        // Switch panels back
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (trackSelectionPanel != null) trackSelectionPanel.SetActive(false);
        if (menuNavigation != null) menuNavigation.Focus(mainMenuDefaultButton);
    }

    // Track Selection Button Functions
    public void LoadTrack1()
    {
        LoadingScreen.LoadScene("Level1");
    }

    public void LoadTrack2()
    {
        LoadingScreen.LoadScene("Level2");
    }

    public void LoadTrack3()
    {
        LoadingScreen.LoadScene("Level3");
    }

    public void PracticeButton()
    {
        if (cars == null || cars.Length == 0) return;

        currentCarIndex = Mathf.Clamp(currentCarIndex, 0, cars.Length - 1);

        PlayerPrefs.SetInt("CarIndexValue", currentCarIndex);
        PlayerPrefs.Save();

        LoadingScreen.LoadScene("Practice");
    }

    public void Exit()
    {
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }
}
