using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class ChaseCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public Rigidbody targetRb;
    [Header("Camera Position")]
    public Vector3 offset = new Vector3(0f, 2.8f, -8.5f);
    public float followSmoothness = 14f;
    public float lookHeight = 1.2f;
    [Header("Arcade Follow")]
    public float yawFollowSpeed = 7f;
    public float maxDriftTilt = 2.5f;
    public float tiltSmoothing = 6f;
    public float lookAhead = 3f;
    [Tooltip("Comfortable chase distance from the look pivot. Only solid obstructions may shorten it.")]
    [Min(0.1f)] public float minimumFollowDistance = 8.5f;
    public float speedPullback = 1.5f;
    [Min(0.01f)] public float speedZoomResponse = 4f;
    [Tooltip("Lower values retain the wide speed view longer while braking.")]
    [Min(0.01f)] public float brakingZoomReturn = 1.1f;
    public float baseFieldOfView = 72f;
    public float fastFieldOfView = 82f;
    [Header("Obstruction Avoidance")]
    public LayerMask obstructionLayers = Physics.DefaultRaycastLayers;
    [Min(0.01f)] public float collisionRadius = 0.3f;
    [Min(0f)] public float collisionPadding = 0.2f;
    [Min(0.01f)] public float obstructionReturnSpeed = 4f;
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
    Vector3 previousTargetPosition;
    float smoothedSpeedRatio;
    float followDistance;
    float collisionDistance;
    Camera view;
    readonly RaycastHit[] obstructionHits = new RaycastHit[24];

    void Awake() => view = GetComponent<Camera>();

    public void SnapToTarget()
    {
        if (target == null) return;
        boundTarget = target;
        var body = target.GetComponentInParent<Rigidbody>();
        if (body != null || targetRb == null) targetRb = body;
        if (view == null) view = GetComponent<Camera>();
        yaw = target.eulerAngles.y;
        pitch = 8f;
        currentTilt = 0f;
        previousTargetPosition = target.position;
        smoothedSpeedRatio = SpeedRatio(PlanarSpeed());
        Vector3 pivot = target.position + Vector3.up * lookHeight;
        Vector3 boom = DesiredBoom(pivot);
        followDistance = Mathf.Max(minimumFollowDistance, boom.magnitude);
        collisionDistance = followDistance;
        transform.position = pivot + boom.normalized * followDistance;
        if (view != null) view.fieldOfView = Mathf.Lerp(baseFieldOfView, fastFieldOfView, smoothedSpeedRatio);
    }

    void LateUpdate()
    {
        if (target == null || holdPosition || Time.timeScale <= 0f) return;
        float dt = Time.deltaTime;
        bool mouseLook = Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed;
        Vector2 stick = Gamepad.current != null ? Gamepad.current.rightStick.ReadValue() : Vector2.zero;
        bool freeLook = mouseLook || stick.magnitude > gamepadDeadzone;
        bool rearView = (Keyboard.current != null && Keyboard.current.cKey.isPressed) ||
            (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed);

        Vector2 input = freeLook ? (stick.magnitude > gamepadDeadzone ? stick * gamepadSensitivity * dt :
            Mouse.current != null ? Mouse.current.delta.ReadValue() * mouseSensitivity : Vector2.zero) : Vector2.zero;
        UpdateFollow(dt, input, freeLook, rearView);
    }

    // The same step supports ordinary input, scripted cameras and deterministic checks.
    public void UpdateFollow(float dt, Vector2 lookInput, bool freeLook, bool rearView)
    {
        if (target == null || holdPosition || dt <= 0f) return;
        if (boundTarget != target || (target.position - previousTargetPosition).sqrMagnitude > 900f) SnapToTarget();

        // Follow target translation immediately. Smoothing world positions lets a braking
        // or reversing car catch up to the camera and collapse the chase distance.
        transform.position += target.position - previousTargetPosition;
        previousTargetPosition = target.position;
        float speed = PlanarSpeed();
        float desiredRatio = SpeedRatio(speed);
        float zoomRate = desiredRatio < smoothedSpeedRatio ? brakingZoomReturn : speedZoomResponse;
        smoothedSpeedRatio = Mathf.Lerp(smoothedSpeedRatio, desiredRatio, 1f - Mathf.Exp(-Mathf.Max(0.01f, zoomRate) * dt));

        if (freeLook)
        {
            yaw += lookInput.x;
            pitch = Mathf.Clamp(pitch - lookInput.y, minPitch, maxPitch);
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

        Vector3 pivot = target.position + Vector3.up * lookHeight;
        Vector3 desiredBoom = DesiredBoom(pivot);
        Vector3 currentBoom = transform.position - pivot;
        Vector3 direction = currentBoom.sqrMagnitude > 0.001f ? currentBoom.normalized : desiredBoom.normalized;
        float followBlend = 1f - Mathf.Exp(-Mathf.Max(0.01f, followSmoothness) * dt);
        direction = Vector3.Slerp(direction, desiredBoom.normalized, followBlend).normalized;
        float minimum = Mathf.Max(0.1f, minimumFollowDistance);
        followDistance = Mathf.Max(minimum, Mathf.Lerp(followDistance, Mathf.Max(minimum, desiredBoom.magnitude), followBlend));
        float allowed = followDistance;
        int count = Physics.SphereCastNonAlloc(pivot, Mathf.Max(0.01f, collisionRadius), direction, obstructionHits, followDistance,
            obstructionLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = obstructionHits[i];
            if (hit.collider.transform.IsChildOf(target) || (targetRb != null && hit.rigidbody == targetRb)) continue;
            allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - Mathf.Max(0f, collisionPadding)));
        }
        // Never clip through an obstruction, but extend smoothly after it clears.
        collisionDistance = allowed < collisionDistance ? allowed :
            Mathf.Lerp(collisionDistance, allowed, 1f - Mathf.Exp(-Mathf.Max(0.01f, obstructionReturnSpeed) * dt));
        transform.position = pivot + direction * Mathf.Min(allowed, collisionDistance);
        Vector3 lookPoint = pivot + (rearView ? -target.forward : target.forward) * lookAhead * (freeLook ? 0f : smoothedSpeedRatio);
        Vector3 lookDirection = lookPoint - transform.position;
        if (lookDirection.sqrMagnitude > 0.01f)
        {
            float lateral = targetRb != null ? Vector3.Dot(targetRb.linearVelocity, target.right) : 0f;
            float tilt = freeLook || rearView ? 0f : Mathf.Clamp(-lateral * 0.15f, -maxDriftTilt, maxDriftTilt);
            currentTilt = Mathf.Lerp(currentTilt, tilt, 1f - Mathf.Exp(-tiltSmoothing * dt));
            transform.rotation = Quaternion.LookRotation(lookDirection) * Quaternion.Euler(0f, 0f, currentTilt);
        }
        if (view != null) view.fieldOfView = Mathf.Lerp(view.fieldOfView,
            Mathf.Lerp(baseFieldOfView, fastFieldOfView, smoothedSpeedRatio), 1f - Mathf.Exp(-4f * dt));
    }

    float PlanarSpeed() => targetRb != null ? Vector3.ProjectOnPlane(targetRb.linearVelocity, Vector3.up).magnitude : 0f;
    static float SpeedRatio(float speed) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, 50f, speed));

    Vector3 DesiredBoom(Vector3 pivot)
    {
        Vector3 followOffset = offset;
        followOffset.z -= smoothedSpeedRatio * speedPullback;
        Vector3 boom = target.position + Quaternion.Euler(pitch, yaw, 0f) * followOffset - pivot;
        return boom.sqrMagnitude > 0.001f ? boom : Quaternion.Euler(pitch, yaw, 0f) * Vector3.back;
    }
}
