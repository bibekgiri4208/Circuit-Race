using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Recover focus before the EventSystem processes navigation or submit input.
[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
public class MenuNavigation : MonoBehaviour
{
    private Button[] buttons;
    private Button preferredButton;
    private Button lastSelectedButton;
    private EventSystem eventSystem;
    private InputSystemUIInputModule inputModule;

    public void Initialize(GameObject root, Button firstSelected)
    {
        eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (inputModule == null)
        {
            foreach (BaseInputModule module in eventSystem.GetComponents<BaseInputModule>())
                module.enabled = false;
            inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }
        eventSystem.sendNavigationEvents = true;

        buttons = root.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button.GetComponent<UIButtonFeedback>() == null)
                button.gameObject.AddComponent<UIButtonFeedback>();
        }

        preferredButton = firstSelected;
    }

    public void Focus(Button firstSelected = null)
    {
        if (eventSystem == null || buttons == null) return;
        if (firstSelected != null) preferredButton = firstSelected;

        Button target = IsAvailable(preferredButton) ? preferredButton : null;
        if (target == null)
        {
            foreach (Button button in buttons)
            {
                if (!IsAvailable(button)) continue;
                target = button;
                break;
            }
        }

        lastSelectedButton = target;
        eventSystem.SetSelectedGameObject(target != null ? target.gameObject : null);
    }

    void Update()
    {
        if (eventSystem == null || buttons == null || inputModule == null) return;

        GameObject selected = eventSystem.currentSelectedGameObject;
        Button selectedButton = selected != null ? selected.GetComponent<Button>() : null;
        if (IsAvailable(selectedButton) && System.Array.IndexOf(buttons, selectedButton) >= 0)
        {
            lastSelectedButton = selectedButton;
            return;
        }

        bool navigating = inputModule.move != null && inputModule.move.action != null &&
            inputModule.move.action.ReadValue<Vector2>().sqrMagnitude > 0.01f;
        bool submitting = inputModule.submit != null && inputModule.submit.action != null &&
            inputModule.submit.action.WasPressedThisFrame();

        // Leave mouse hover free to shrink back; restore focus when navigation resumes.
        if (navigating || submitting)
        {
            if (IsAvailable(lastSelectedButton))
                eventSystem.SetSelectedGameObject(lastSelectedButton.gameObject);
            else
                Focus();
        }
    }

    private static bool IsAvailable(Button button)
    {
        return button != null && button.IsActive() && button.IsInteractable();
    }
}
