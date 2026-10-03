using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class UIButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField, Range(1f, 1.2f)] private float highlightedScale = 1.1f;
    [SerializeField, Min(0.01f)] private float smoothTime = 0.1f;

    private Button button;
    private Vector3 originalScale;
    private Vector3 scaleVelocity;
    private bool isHovered;

    void Awake()
    {
        button = GetComponent<Button>();
        originalScale = transform.localScale;
    }

    void Update()
    {
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        bool highlighted = button.IsActive() && button.IsInteractable() &&
            (selected == gameObject || (selected == null && isHovered));
        Vector3 targetScale = originalScale * (highlighted ? highlightedScale : 1f);

        // Keep feedback responsive even when the pause menu stops game time.
        transform.localScale = Vector3.SmoothDamp(transform.localScale, targetScale,
            ref scaleVelocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!button.IsInteractable()) return;
        isHovered = true;

        // Mouse and gamepad share one focus, so two buttons do not look selected.
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(gameObject);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
            EventSystem.current.SetSelectedGameObject(null);
    }

    void OnDisable()
    {
        isHovered = false;
        scaleVelocity = Vector3.zero;
        transform.localScale = originalScale;
    }
}
