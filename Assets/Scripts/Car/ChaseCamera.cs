using UnityEngine;

public class ChaseCamera : MonoBehaviour
{
    public Transform target;
    public Rigidbody targetRb;

    [Header("Follow")]
    public float baseDistance = 4.5f;
    public float maxDistance = 6.5f;
    public float height = 1.8f;

    [Header("Speed Camera")]
    public float speedForMaxDistance = 80f;
    public float distanceSmoothSpeed = 4f;

    [Header("Speed Shake")]
    public float shakeStartSpeed = 70f;
    public float maxShakeSpeed = 180f;
    public float maxShakeAmount = 0.04f;
    public float shakeFrequency = 22f;

    [Header("Position Smooth")]
    public float positionSmoothSpeed = 8f;

    [Header("Rotation")]
    public float rotationSmoothSpeed = 6f;
    public float velocityFollowStrength = 0.75f;

    [Header("Look")]
    public float lookHeight = 1.2f;
    public float lookForwardOffset = 2f;

    [Header("Dynamic Effects")]
    public float cameraTiltAmount = 6f;
    public float tiltSmoothSpeed = 5f;

    private float currentYaw;
    private float currentDistance;
    private float currentTilt;

    void Start()
    {
        if (target != null)
        {
            currentYaw = target.eulerAngles.y;
        }

        currentDistance = baseDistance;
        currentTilt = 0f;

        //  FORCE CLEAN INITIAL CAMERA STATE (fixes startup tilt)
        if (target != null && targetRb != null)
        {
            Quaternion rotation = Quaternion.Euler(0f, currentYaw, 0f);

            Vector3 startPos =
                target.position +
                Vector3.up * height -
                rotation * Vector3.forward * baseDistance;

            transform.position = startPos;

            Vector3 lookTarget =
                target.position +
                target.forward * lookForwardOffset +
                Vector3.up * lookHeight;

            transform.rotation = Quaternion.LookRotation(
                lookTarget - transform.position,
                Vector3.up
            );
        }
    }

    void LateUpdate()
    {
        if (target == null || targetRb == null)
            return;

        Vector3 velocity = targetRb.linearVelocity;
        velocity.y = 0f;

        float speed = velocity.magnitude;
        float speedKmh = speed * 3.6f;

        // ================= CAMERA DIRECTION =================
        Vector3 forward = target.forward;

        if (speed > 2f)
        {
            Vector3 velocityDir = velocity.normalized;

            forward = Vector3.Slerp(
                target.forward,
                velocityDir,
                velocityFollowStrength
            );
        }

        float targetYaw =
            Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

        currentYaw = Mathf.LerpAngle(
            currentYaw,
            targetYaw,
            rotationSmoothSpeed * Time.deltaTime
        );


        Vector3 GetSpeedShake(float speedKmh)
        {
            if (speedKmh < shakeStartSpeed)
                return Vector3.zero;

            float shake01 = Mathf.InverseLerp(
                shakeStartSpeed,
                maxShakeSpeed,
                speedKmh
            );

            float amount = maxShakeAmount * shake01;

            float x =
                (Mathf.PerlinNoise(Time.time * shakeFrequency, 0f) - 0.5f)
                * amount;

            float y =
                (Mathf.PerlinNoise(0f, Time.time * shakeFrequency) - 0.5f)
                * amount;

            return transform.right * x + transform.up * y;
        }

        // ================= SPEED BASED DISTANCE =================
        float speed01 = Mathf.Clamp01(speed / speedForMaxDistance);
        speed01 = Mathf.SmoothStep(0f, 1f, speed01);

        float targetDistance = Mathf.Lerp(
            baseDistance,
            maxDistance,
            speed01
        );

        currentDistance = Mathf.Lerp(
            currentDistance,
            targetDistance,
            distanceSmoothSpeed * Time.deltaTime
        );

        // ================= CAMERA POSITION =================
        Quaternion rotation =
            Quaternion.Euler(0f, currentYaw, 0f);

        Vector3 desiredPosition =
    target.position
    + Vector3.up * height
    - rotation * Vector3.forward * currentDistance;

        desiredPosition += GetSpeedShake(speedKmh);

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            positionSmoothSpeed * Time.deltaTime
        );

        // ================= LOOK TARGET =================
        Vector3 lookTarget =
            target.position
            + target.forward * lookForwardOffset
            + Vector3.up * lookHeight;

        Quaternion lookRotation =
            Quaternion.LookRotation(
                lookTarget - transform.position,
                Vector3.up
            );

        // ================= SAFE TILT =================
        float steerInfluence = 0f;

        if (speed > 1f)
        {
            steerInfluence = Vector3.Dot(target.right, velocity.normalized);
        }

        float targetTilt =
            (speed > 1f) ? -steerInfluence * cameraTiltAmount : 0f;

        currentTilt = Mathf.Lerp(
            currentTilt,
            targetTilt,
            tiltSmoothSpeed * Time.deltaTime
        );

        transform.rotation =
            lookRotation *
            Quaternion.Euler(0f, 0f, currentTilt);
    }
}