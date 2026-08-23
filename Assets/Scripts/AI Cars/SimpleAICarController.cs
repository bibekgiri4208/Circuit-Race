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
    public float motorTorque = 1800f;
    public float brakeTorque = 3500f;

    [Header("Steering")]
    public float maxSteerAngle = 30f;
    public float steerSensitivity = 4f;

    [Header("Avoidance Sensors")]
    public LayerMask obstacleLayers;
    public float frontSensorLength = 10f;
    public float sideSensorLength = 4f;
    public float sensorHeight = 0.6f;
    public float sensorSideOffset = 0.75f;
    public float avoidanceSteerStrength = 0.5f;
    public float obstacleSlowSpeedKmh = 35f;

    [Header("Stability")]
    public Transform centerOfMass;
    public Vector3 fallbackCOM = new Vector3(0f, -0.6f, 0.1f);
    public float downforce = 80f;

    [Header("Debug")]
    public bool showSensorRays = true;

    Rigidbody rb;
    float currentSteer;

    bool obstacleAhead;
    float avoidanceSteer;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        rb.mass = 1200f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.angularDamping = 4f;

        rb.centerOfMass = centerOfMass != null
            ? transform.InverseTransformPoint(centerOfMass.position)
            : fallbackCOM;
    }

    void FixedUpdate()
    {
        if (waypoints == null || waypoints.Length == 0)
            return;

        if (RaceManager.Instance != null && !RaceManager.Instance.raceStarted)
        {
            SetMotorTorque(0f);
            SetBrakeTorque(300f);
            return;
        }

        HandleWaypoint();
        HandleSensors();
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

    void HandleSensors()
    {
        obstacleAhead = false;
        avoidanceSteer = 0f;

        Vector3 origin =
            transform.position +
            Vector3.up * sensorHeight;

        Vector3 frontLeft =
            origin - transform.right * sensorSideOffset;

        Vector3 frontRight =
            origin + transform.right * sensorSideOffset;

        bool centerHit = Physics.Raycast(
            origin,
            transform.forward,
            out RaycastHit hitCenter,
            frontSensorLength,
            obstacleLayers
        );

        bool leftHit = Physics.Raycast(
            frontLeft,
            transform.forward,
            out RaycastHit hitLeft,
            frontSensorLength,
            obstacleLayers
        );

        bool rightHit = Physics.Raycast(
            frontRight,
            transform.forward,
            out RaycastHit hitRight,
            frontSensorLength,
            obstacleLayers
        );

        bool sideLeftHit = Physics.Raycast(
            origin,
            -transform.right,
            out RaycastHit hitSideLeft,
            sideSensorLength,
            obstacleLayers
        );

        bool sideRightHit = Physics.Raycast(
            origin,
            transform.right,
            out RaycastHit hitSideRight,
            sideSensorLength,
            obstacleLayers
        );

        if (centerHit)
            obstacleAhead = true;

        if (leftHit)
        {
            obstacleAhead = true;
            avoidanceSteer += avoidanceSteerStrength;
        }

        if (rightHit)
        {
            obstacleAhead = true;
            avoidanceSteer -= avoidanceSteerStrength;
        }

        if (sideLeftHit)
            avoidanceSteer += avoidanceSteerStrength;

        if (sideRightHit)
            avoidanceSteer -= avoidanceSteerStrength;

        avoidanceSteer = Mathf.Clamp(avoidanceSteer, -1f, 1f);

        if (showSensorRays)
        {
            Debug.DrawRay(
                origin,
                transform.forward * frontSensorLength,
                centerHit ? Color.red : Color.green
            );

            Debug.DrawRay(
                frontLeft,
                transform.forward * frontSensorLength,
                leftHit ? Color.red : Color.yellow
            );

            Debug.DrawRay(
                frontRight,
                transform.forward * frontSensorLength,
                rightHit ? Color.red : Color.yellow
            );

            Debug.DrawRay(
                origin,
                -transform.right * sideSensorLength,
                sideLeftHit ? Color.red : Color.cyan
            );

            Debug.DrawRay(
                origin,
                transform.right * sideSensorLength,
                sideRightHit ? Color.red : Color.cyan
            );
        }
    }

    void HandleSteering()
    {
        Transform targetWaypoint = waypoints[currentWaypointIndex];

        Vector3 localTarget =
            transform.InverseTransformPoint(targetWaypoint.position);

        float pathSteer =
            Mathf.Clamp(localTarget.x / localTarget.magnitude, -1f, 1f);

        float steerInput =
            Mathf.Clamp(pathSteer + avoidanceSteer, -1f, 1f);

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

        float targetSpeed = maxSpeedKmh;

        AIWaypoint waypointData =
            targetWaypoint.GetComponent<AIWaypoint>();

        if (waypointData != null)
            targetSpeed = waypointData.targetSpeedKmh;

        if (obstacleAhead)
            targetSpeed = Mathf.Min(targetSpeed, obstacleSlowSpeedKmh);

        if (speedKmh < targetSpeed)
        {
            SetMotorTorque(motorTorque);
            SetBrakeTorque(0f);
        }
        else
        {
            SetMotorTorque(0f);
            SetBrakeTorque(brakeTorque * 0.45f);
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