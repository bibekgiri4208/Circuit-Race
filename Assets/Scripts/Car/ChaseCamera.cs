using UnityEngine;
using UnityEngine.InputSystem;

public class ChaseCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Rigidbody targetRb;
    [Header("Camera Position")]
    public Vector3 offset = new Vector3(0f, 2.5f, -6.4f);
    public float followSmoothness = 14f;
    public float lookHeight = 1.2f;
    [Header("Arcade Follow")]
    public float yawFollowSpeed = 7f;
    public float maxDriftTilt = 2.5f;
    public float tiltSmoothing = 6f;
    public float lookAhead = 3f;
    public float speedPullback = 1f;
    public float baseFieldOfView = 65f;
    public float fastFieldOfView = 82f;
    public LayerMask obstructionLayers = ~0;
    [Header("Mouse Look")]
    public float mouseSensitivity = 0.15f;
    [Header("Gamepad Look")]
    public float gamepadSensitivity = 120f;
    public float gamepadDeadzone = 0.15f;
    [Header("Look Limits")]
    public float minPitch = -10f;
    public float maxPitch = 45f;
    [HideInInspector] public bool holdPosition;

    float yaw;
    float pitch = 8f;
    float currentTilt;
    Transform boundTarget;
    Camera view;
    readonly RaycastHit[] obstructionHits = new RaycastHit[24];

    void Awake() => view = GetComponent<Camera>();

    public void SnapToTarget()
    {
        if (target == null) return;
        boundTarget = target;
        if (targetRb == null) targetRb = target.GetComponent<Rigidbody>();
        yaw = target.eulerAngles.y;
        pitch = 8f;
        currentTilt = 0f;
        transform.position = target.position + Quaternion.Euler(pitch, yaw, 0f) * offset;
    }

    void LateUpdate()
    {
        if (target == null || holdPosition || Time.timeScale <= 0f) return;
        if (boundTarget != target || Vector3.Distance(transform.position, target.position) > 30f) SnapToTarget();
        float dt = Time.deltaTime;
        float speed = targetRb != null ? targetRb.linearVelocity.magnitude : 0f;
        float speedRatio = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, 50f, speed));
        bool mouseLook = Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed;
        Vector2 stick = Gamepad.current != null ? Gamepad.current.rightStick.ReadValue() : Vector2.zero;
        bool freeLook = mouseLook || stick.magnitude > gamepadDeadzone;
        bool rearView = (Keyboard.current != null && Keyboard.current.cKey.isPressed) ||
            (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed);

        if (freeLook)
        {
            Vector2 input = stick.magnitude > gamepadDeadzone ? stick * gamepadSensitivity * dt :
                Mouse.current != null ? Mouse.current.delta.ReadValue() * mouseSensitivity : Vector2.zero;
            yaw += input.x;
            pitch = Mathf.Clamp(pitch - input.y, minPitch, maxPitch);
        }
        else
        {
            Vector3 heading = target.forward;
            if (targetRb != null && speed > 5f && Vector3.Dot(targetRb.linearVelocity, target.forward) > 0f)
                heading = Vector3.Lerp(heading, targetRb.linearVelocity.normalized, 0.22f);
            float targetYaw = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg + (rearView ? 180f : 0f);
            yaw = Mathf.LerpAngle(yaw, targetYaw, 1f - Mathf.Exp(-(rearView ? 18f : yawFollowSpeed) * dt));
            pitch = Mathf.Lerp(pitch, 8f, 1f - Mathf.Exp(-5f * dt));
        }

        Vector3 followOffset = offset;
        followOffset.z -= speedRatio * speedPullback;
        Vector3 desired = target.position + Quaternion.Euler(pitch, yaw, 0f) * followOffset;
        Vector3 pivot = target.position + Vector3.up * lookHeight;
        Vector3 boom = desired - pivot;
        float allowed = boom.magnitude;
        int count = Physics.SphereCastNonAlloc(pivot, 0.25f, boom.normalized, obstructionHits, allowed,
            obstructionLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = obstructionHits[i];
            if (hit.collider.transform.IsChildOf(target) || hit.rigidbody == targetRb) continue;
            allowed = Mathf.Min(allowed, Mathf.Max(0.7f, hit.distance - 0.2f));
        }
        desired = pivot + boom.normalized * allowed;
        bool obstructed = allowed < boom.magnitude - 0.01f;
        transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-(obstructed ? 35f : followSmoothness) * dt));
        Vector3 lookPoint = pivot + (rearView ? -target.forward : target.forward) * lookAhead * (freeLook ? 0f : speedRatio);
        Vector3 lookDirection = lookPoint - transform.position;
        if (lookDirection.sqrMagnitude > 0.01f)
        {
            float lateral = targetRb != null ? Vector3.Dot(targetRb.linearVelocity, target.right) : 0f;
            float tilt = freeLook || rearView ? 0f : Mathf.Clamp(-lateral * 0.15f, -maxDriftTilt, maxDriftTilt);
            currentTilt = Mathf.Lerp(currentTilt, tilt, 1f - Mathf.Exp(-tiltSmoothing * dt));
            transform.rotation = Quaternion.LookRotation(lookDirection) * Quaternion.Euler(0f, 0f, currentTilt);
        }
        if (view != null) view.fieldOfView = Mathf.Lerp(view.fieldOfView,
            Mathf.Lerp(baseFieldOfView, fastFieldOfView, speedRatio), 1f - Mathf.Exp(-4f * dt));
    }
}
