using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CarSelection : MonoBehaviour
{
    public const string SelectionKey = "CarIndexValue";

    [System.Serializable]
    public class CarInfo
    {
        public string carName;
    }

    [Header("Cars (same order as the Practice spawner)")]
    [SerializeField] private GameObject[] cars;
    [SerializeField] private CarInfo[] carInfos;
    [SerializeField] private TMP_Text carNameText;

    [Header("UI Navigation")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private Button mainMenuDefaultButton;

    private int currentCarIndex;
    private bool isLoading;

    void Start()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (cars == null || cars.Length == 0)
        {
            Debug.LogError("CarSelection: No display cars are assigned.", this);
            enabled = false;
            return;
        }

        ShowCar(PlayerPrefs.GetInt(SelectionKey, 0));

        Canvas canvas = mainMenuPanel != null ? mainMenuPanel.GetComponentInParent<Canvas>() : null;
        if (canvas != null)
        {
            MenuNavigation navigation = canvas.GetComponent<MenuNavigation>();
            if (navigation == null) navigation = canvas.gameObject.AddComponent<MenuNavigation>();
            navigation.Initialize(mainMenuPanel, mainMenuDefaultButton);
            navigation.Focus();
        }
    }

    void Update()
    {
        if (isLoading) return;
        Gamepad gamepad = Gamepad.current;
        if (gamepad == null) return;
        if (gamepad.leftShoulder.wasPressedThisFrame) PreviousCar();
        else if (gamepad.rightShoulder.wasPressedThisFrame) NextCar();
    }

    public void NextCar()
    {
        if (!isLoading && cars != null && cars.Length > 0)
            ShowCar((currentCarIndex + 1) % cars.Length);
    }

    public void PreviousCar()
    {
        if (!isLoading && cars != null && cars.Length > 0)
            ShowCar((currentCarIndex + cars.Length - 1) % cars.Length);
    }

    void ShowCar(int index)
    {
        currentCarIndex = Mathf.Clamp(index, 0, cars.Length - 1);
        for (int i = 0; i < cars.Length; i++)
        {
            if (cars[i] != null) cars[i].SetActive(i == currentCarIndex);
        }

        if (carNameText != null)
        {
            string carName = carInfos != null && currentCarIndex < carInfos.Length && carInfos[currentCarIndex] != null
                ? carInfos[currentCarIndex].carName : cars[currentCarIndex] != null ? cars[currentCarIndex].name : string.Empty;
            carNameText.text = (carName ?? string.Empty).ToUpperInvariant();
        }
    }

    public void PracticeButton()
    {
        if (isLoading || cars == null || cars.Length == 0 || cars[currentCarIndex] == null) return;
        if (!Application.CanStreamedLevelBeLoaded("Practice"))
        {
            Debug.LogError("CarSelection: Add the Practice scene to Build Settings.", this);
            return;
        }

        PlayerPrefs.SetInt(SelectionKey, currentCarIndex);
        PlayerPrefs.Save();
        isLoading = true;
        SceneManager.LoadSceneAsync("Practice");
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
