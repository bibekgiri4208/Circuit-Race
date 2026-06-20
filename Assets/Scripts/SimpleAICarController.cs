using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SimpleAICarController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider wheelFL, wheelFR, wheelRL, wheelRR;

    [Header("Wheel Meshes")]
    public Transform meshFL, meshFR, meshRL, meshRR;

    [Header("AI Path")]
    public Transform[] waypoints;
    public int currentWaypointIndex = 0;
    public float waypointReachDistance = 8f;

    [Header("Speed")]
    public float maxSpeedKmh = 100f;
    public float cornerSpeedKmh = 55f;
    public float motorTorque = 1800f;
    public float brakeTorque = 3500f;

    [Header("Steering")]
    public float maxSteerAngle = 30f;
    public float steerSensitivity = 4f;
    public float turnSlowdownAngle = 25f;

    [Header("Stability")]
    public Transform centerOfMass;
    public Vector3 fallbackCOM = new Vector3(0f, -0.6f, 0.1f);
    public float downforce = 80f;

    Rigidbody rb;
    float currentSteer;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        rb.mass = 1200f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.angularDamping = 1.5f;

        rb.centerOfMass = centerOfMass != null
            ? transform.InverseTransformPoint(centerOfMass.position)
            : fallbackCOM;
    }

    void FixedUpdate()
    {
        if (waypoints == null || waypoints.Length == 0)
            return;

        HandleWaypoint();
        HandleSteering();
        HandleSpeed();
        ApplyDownforce();
    }

    void Update()
    {
        UpdateWheelMeshes();
    }

    void HandleWaypoint()
    {
        Transform targetWaypoint = waypoints[currentWaypointIndex];

        float distance = Vector3.Distance(
            transform.position,
            targetWaypoint.position
        );

        if (distance < waypointReachDistance)
        {
            currentWaypointIndex++;

            if (currentWaypointIndex >= waypoints.Length)
                currentWaypointIndex = 0;
        }
    }

    void HandleSteering()
    {
        Transform targetWaypoint = waypoints[currentWaypointIndex];

        Vector3 localTarget =
            transform.InverseTransformPoint(targetWaypoint.position);

        float steerInput =
            Mathf.Clamp(localTarget.x / localTarget.magnitude, -1f, 1f);

        float targetSteer =
            steerInput * maxSteerAngle;

        currentSteer = Mathf.Lerp(
            currentSteer,
            targetSteer,
            Time.fixedDeltaTime * steerSensitivity
        );

        wheelFL.steerAngle = currentSteer;
        wheelFR.steerAngle = currentSteer;
    }

    void HandleSpeed()
    {
        float speedKmh = rb.linearVelocity.magnitude * 3.6f;

        Transform targetWaypoint = waypoints[currentWaypointIndex];

        Vector3 localTarget =
            transform.InverseTransformPoint(targetWaypoint.position);

        float cornerAngle =
            Mathf.Abs(Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg);

        float targetSpeed =
            cornerAngle > turnSlowdownAngle
                ? cornerSpeedKmh
                : maxSpeedKmh;

        if (speedKmh < targetSpeed)
        {
            SetMotorTorque(motorTorque);
            SetBrakeTorque(0f);
        }
        else
        {
            SetMotorTorque(0f);
            SetBrakeTorque(brakeTorque * 0.35f);
        }
    }

    void ApplyDownforce()
    {
        rb.AddForce(
            -transform.up * downforce * rb.linearVelocity.magnitude,
            ForceMode.Force
        );
    }

    void SetMotorTorque(float torque)
    {
        wheelRL.motorTorque = torque;
        wheelRR.motorTorque = torque;

        wheelFL.motorTorque = 0f;
        wheelFR.motorTorque = 0f;
    }

    void SetBrakeTorque(float torque)
    {
        wheelFL.brakeTorque = torque;
        wheelFR.brakeTorque = torque;
        wheelRL.brakeTorque = torque;
        wheelRR.brakeTorque = torque;
    }

    void UpdateWheelMeshes()
    {
        UpdateWheel(wheelFL, meshFL);
        UpdateWheel(wheelFR, meshFR);
        UpdateWheel(wheelRL, meshRL);
        UpdateWheel(wheelRR, meshRR);
    }

    void UpdateWheel(WheelCollider col, Transform mesh)
    {
        if (col == null || mesh == null) return;

        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        mesh.position = pos;
        mesh.rotation = rot;
    }
}