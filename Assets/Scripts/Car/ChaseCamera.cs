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

    [HideInInspector] public bool holdPosition = false;

    private float yaw;
    private float pitch = 15f;

    private void Start()
    {
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
        Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 desiredPosition = target.position + cameraRotation * offset;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            followSmoothness * Time.deltaTime
        );

        Vector3 lookPoint = target.position + Vector3.up * lookHeight;
        transform.rotation = Quaternion.LookRotation(lookPoint - transform.position);
    }

}
