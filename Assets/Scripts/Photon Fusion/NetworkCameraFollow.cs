using UnityEngine;
using Fusion;

public class NetworkCameraFollow : MonoBehaviour
{
    private Transform _targetCar;
    private Rigidbody _targetRb;

    [Header("Settings (Same as Singleplayer)")]
    public float baseDistance = 4.5f;
    public float maxDistance = 6.5f;
    public float height = 1.8f;
    public float speedForMaxDistance = 80f;
    public float distanceSmoothSpeed = 4f;
    public float shakeStartSpeed = 70f;
    public float maxShakeSpeed = 180f;
    public float maxShakeAmount = 0.04f;
    public float shakeFrequency = 22f;
    public float positionSmoothSpeed = 8f;
    public float rotationSmoothSpeed = 6f;
    public float velocityFollowStrength = 0.75f;
    public float lookHeight = 1.2f;
    public float lookForwardOffset = 2f;
    public float cameraTiltAmount = 6f;
    public float tiltSmoothSpeed = 5f;

    private float currentYaw;
    private float currentDistance;
    private float currentTilt;

    void LateUpdate()
    {
        // 1. Find the local player car
        if (_targetCar == null)
        {
            foreach (var netObj in Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Exclude))
            {
                if (netObj.HasInputAuthority && netObj.CompareTag("Player"))
                {
                    _targetCar = netObj.transform;
                    _targetRb = netObj.GetComponent<Rigidbody>();
                    currentYaw = _targetCar.eulerAngles.y;
                    currentDistance = baseDistance;
                    break;
                }
            }
            return;
        }

        // 2. Calculate speed
        Vector3 velocity = _targetRb != null ? _targetRb.linearVelocity : Vector3.zero;
        velocity.y = 0f;
        float speed = velocity.magnitude;
        float speedKmh = speed * 3.6f;

        // 3. Direction and Yaw
        Vector3 forward = _targetCar.forward;
        if (speed > 2f)
            forward = Vector3.Slerp(_targetCar.forward, velocity.normalized, velocityFollowStrength);

        currentYaw = Mathf.LerpAngle(currentYaw, Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, rotationSmoothSpeed * Time.deltaTime);

        // 4. Distance
        float speed01 = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(speed / speedForMaxDistance));
        currentDistance = Mathf.Lerp(currentDistance, Mathf.Lerp(baseDistance, maxDistance, speed01), distanceSmoothSpeed * Time.deltaTime);

        // 5. Position & Shake
        Quaternion rotation = Quaternion.Euler(0f, currentYaw, 0f);
        Vector3 desiredPosition = _targetCar.position + Vector3.up * height - rotation * Vector3.forward * currentDistance;

        // Shake logic
        if (speedKmh >= shakeStartSpeed)
        {
            float shake01 = Mathf.InverseLerp(shakeStartSpeed, maxShakeSpeed, speedKmh);
            float x = (Mathf.PerlinNoise(Time.time * shakeFrequency, 0f) - 0.5f) * maxShakeAmount * shake01;
            float y = (Mathf.PerlinNoise(0f, Time.time * shakeFrequency) - 0.5f) * maxShakeAmount * shake01;
            desiredPosition += transform.right * x + transform.up * y;
        }

        transform.position = Vector3.Lerp(transform.position, desiredPosition, positionSmoothSpeed * Time.deltaTime);

        // 6. Look and Tilt
        Vector3 lookTarget = _targetCar.position + _targetCar.forward * lookForwardOffset + Vector3.up * lookHeight;
        Quaternion lookRotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);

        float steerInfluence = (speed > 1f) ? Vector3.Dot(_targetCar.right, velocity.normalized) : 0f;
        currentTilt = Mathf.Lerp(currentTilt, (speed > 1f) ? -steerInfluence * cameraTiltAmount : 0f, tiltSmoothSpeed * Time.deltaTime);

        transform.rotation = lookRotation * Quaternion.Euler(0f, 0f, currentTilt);
    }
}