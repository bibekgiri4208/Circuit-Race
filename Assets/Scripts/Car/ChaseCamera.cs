using UnityEngine;
using UnityEngine.InputSystem;

public class ChaseCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Rigidbody targetRb;

    [Header("Camera Position")]
    public Vector3 offset = new Vector3(0f, 3f, -7f);
    public float followSmoothness = 12f;
    public float lookHeight = 1.5f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 0.15f;
    public float minPitch = -10f;
    public float maxPitch = 45f;

    [Header("Boost Camera Effect")]
    public float boostPullBackDistance = 2.5f;
    public float boostHeightIncrease = 0.3f;
    public float boostCameraSmoothness = 8f;

    [HideInInspector] public bool holdPosition = false;

    private float yaw;
    private float pitch = 15f;

    private Vector3 currentOffset;

    private void Start()
    {
        currentOffset = offset;

        if (target != null)
        {
            yaw = target.eulerAngles.y;
        }
    }

    private void LateUpdate()
    {
        if (target == null || holdPosition)
            return;

        HandleMouseLook();
        FollowTarget();
    }

    private void HandleMouseLook()
    {
        if (Mouse.current == null) return;

        Vector2 lookInput = Mouse.current.delta.ReadValue() * mouseSensitivity;

        yaw += lookInput.x;
        pitch -= lookInput.y;

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void FollowTarget()
    {
        bool isBoosting = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

        Vector3 targetOffset = offset;

        if (isBoosting)
        {
            targetOffset = new Vector3(
                offset.x,
                offset.y + boostHeightIncrease,
                offset.z - boostPullBackDistance
            );
        }

        currentOffset = Vector3.Lerp(
            currentOffset,
            targetOffset,
            boostCameraSmoothness * Time.deltaTime
        );

        Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 desiredPosition = target.position + cameraRotation * currentOffset;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            followSmoothness * Time.deltaTime
        );

        Vector3 lookPoint = target.position + Vector3.up * lookHeight;
        transform.rotation = Quaternion.LookRotation(lookPoint - transform.position);
    }

}
