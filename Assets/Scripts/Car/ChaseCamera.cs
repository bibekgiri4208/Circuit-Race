using UnityEngine;
using UnityEngine.InputSystem;

public class ChaseCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Rigidbody targetRb;

    [Header("Camera Position")]
    public Vector3 offset = new Vector3(0f, 1.2f, -3.5f);
    public float followSmoothness = 12f;
    public float lookHeight = 1f;

    [Header("Cinematic Follow")]
    public float yawFollowSpeed = 3f;
    public float maxDriftTilt = 8f;
    public float tiltSmoothing = 3f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 0.15f;

    [Header("Gamepad Look")]
    public float gamepadSensitivity = 2f;
    public float gamepadDeadzone = 0.15f;

    [Header("Look Limits")]
    public float minPitch = -10f;
    public float maxPitch = 45f;

    [HideInInspector] public bool holdPosition = false;

    private float yaw;
    private float pitch = 15f;
    private float currentTilt;
    private CarController carController;
    private bool isFreeCam;

    private void Start()
    {
        if (target != null)
        {
            yaw = target.eulerAngles.y;
            carController = target.GetComponent<CarController>();
        }
    }

    private void LateUpdate()
    {
        if (target == null || holdPosition)
            return;

        bool holdingCtrl = Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed;

        if (holdingCtrl)
        {
            isFreeCam = true;
            HandleMouseLook();
            FollowTargetFree();
        }
        else
        {
            if (isFreeCam)
            {
                yaw = target.eulerAngles.y;
                isFreeCam = false;
            }

            FollowTargetCinematic();
        }
    }

    private void HandleMouseLook()
    {
        Vector2 lookInput = Vector2.zero;

        if (Gamepad.current != null)
        {
            Vector2 rightStick = Gamepad.current.rightStick.ReadValue();

            if (rightStick.magnitude > gamepadDeadzone)
            {
                lookInput = rightStick * gamepadSensitivity;
            }
        }

        if (lookInput == Vector2.zero && Mouse.current != null)
        {
            lookInput = Mouse.current.delta.ReadValue() * mouseSensitivity;
        }

        yaw += lookInput.x;
        pitch -= lookInput.y;

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void FollowTargetCinematic()
    {
        float targetYaw = target.eulerAngles.y;
        yaw = Mathf.LerpAngle(yaw, targetYaw, yawFollowSpeed * Time.deltaTime);

        Quaternion cameraRotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 desiredPosition = target.position + cameraRotation * offset;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            followSmoothness * Time.deltaTime
        );

        Vector3 lookPoint = target.position + Vector3.up * lookHeight;

        float tiltAngle = 0f;
        if (carController != null && carController.IsDrifting)
        {
            tiltAngle = Mathf.Clamp(-carController.DriftAngle, -maxDriftTilt, maxDriftTilt);
        }
        currentTilt = Mathf.Lerp(currentTilt, tiltAngle, tiltSmoothing * Time.deltaTime);

        Quaternion lookRot = Quaternion.LookRotation(lookPoint - transform.position);
        transform.rotation = Quaternion.Euler(lookRot.eulerAngles.x, lookRot.eulerAngles.y, currentTilt);
    }

    private void FollowTargetFree()
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
