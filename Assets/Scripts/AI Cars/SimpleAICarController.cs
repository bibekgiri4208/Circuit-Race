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
    public int currentWaypointIndex;
    public float waypointReachDistance = 8f;

    [Header("Race Pace")]
    public float maxSpeedKmh = 168f;
    public float motorTorque = 4000f;
    public float brakeTorque = 4500f;
    [Min(1f)] public float acceleration = 10f;
    [Min(1f)] public float brakingDeceleration = 15f;
    [Min(1f)] public float corneringAcceleration = 19f;
    [Range(0f, 0.15f)] public float paceVariation = 0.045f;

    [Header("Predictive Steering")]
    public float maxSteerAngle = 35f;
    public float steerSensitivity = 8f;
    [Min(2f)] public float minimumLookAhead = 7f;
    [Min(0.1f)] public float lookAheadTime = 0.45f;
    [Min(0f)] public float passingLaneWidth = 2.6f;

    [Header("Avoidance Sensors")]
    public LayerMask obstacleLayers;
    public float frontSensorLength = 10f;
    public float sideSensorLength = 4f;
    public float sensorHeight = 0.6f;
    public float sensorSideOffset = 0.75f;
    public float avoidanceSteerStrength = 0.5f;
    public float obstacleSlowSpeedKmh = 35f;

    [Header("Stability and Recovery")]
    public Transform centerOfMass;
    public Vector3 fallbackCOM = new Vector3(0f, -0.6f, 0.1f);
    public float downforce = 80f;
    [Min(1f)] public float stuckRecoveryTime = 4f;
    [Header("Debug")]
    public bool showSensorRays;

    Rigidbody rb;
    WheelCollider[] wheels;
    PlayerLapTracker lapTracker;
    RaceRoute route;
    RigidbodyConstraints originalConstraints;
    bool gridLocked;
    int routeSegment = -1;
    float routeDistance;
    float offRouteDistance;
    float wheelbase;
    float currentSteer;
    float laneOffset;
    float desiredLane;
    float laneHoldUntil;
    float personality;
    float preferredSide;
    float trafficSpeedLimit;
    float avoidanceSteer;
    float stuckTime;
    float reverseUntil;
    float nextRecoveryTime;
    float lastPlanTime;
    float plannedSpeed;
    readonly RaycastHit[] sensorHits = new RaycastHit[24];
    readonly Collider[] traffic = new Collider[48];

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        originalConstraints = rb.constraints;
        lapTracker = GetComponent<PlayerLapTracker>();
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.angularDamping = 1f;
        rb.maxAngularVelocity = 5f;
        rb.centerOfMass = centerOfMass != null ? transform.InverseTransformPoint(centerOfMass.position) : fallbackCOM;
        wheels = new[] { wheelFL, wheelFR, wheelRL, wheelRR };
        foreach (WheelCollider wheel in wheels)
        {
            if (wheel == null)
            {
                Debug.LogError("AI car needs four wheel colliders: " + name, this);
                enabled = false;
                return;
            }
            wheel.ConfigureVehicleSubsteps(10f, 5, 5);
            WheelFrictionCurve friction = wheel.sidewaysFriction;
            friction.stiffness = 1.7f;
            wheel.sidewaysFriction = friction;
        }
        wheelbase = Mathf.Max(1f, Vector3.Distance((wheelFL.transform.position + wheelFR.transform.position) * 0.5f,
            (wheelRL.transform.position + wheelRR.transform.position) * 0.5f));
        route = new RaceRoute(waypoints);
        if (!route.IsValid && RaceManager.Instance != null) route = RaceManager.Instance.Route;
        if (route == null || !route.IsValid)
        {
            Debug.LogWarning("AI car has no valid closed route: " + name, this);
            SetBrakes(brakeTorque);
            enabled = false;
            return;
        }
        int slot = lapTracker != null ? lapTracker.GridIndex : transform.GetSiblingIndex();
        personality = Mathf.Repeat(slot * 0.618034f + 0.23f, 1f);
        preferredSide = slot % 2 == 0 ? -1f : 1f;
        desiredLane = preferredSide * passingLaneWidth * 0.15f;
    }

    void FixedUpdate()
    {
        RaceManager race = RaceManager.Instance;
        bool countdown = race != null && !race.raceStarted;
        if (gridLocked != countdown)
        {
            gridLocked = countdown;
            rb.constraints = countdown ? originalConstraints | RigidbodyConstraints.FreezePositionX |
                RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation : originalConstraints;
        }
        if (countdown || (race != null && race.raceFinished) || (lapTracker != null && lapTracker.RaceCompleted))
        {
            SetMotorTorque(0f);
            SetBrakes(brakeTorque);
            if (countdown)
            {
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                rb.angularVelocity = Vector3.zero;
            }
            return;
        }

        routeDistance = route.Project(rb.position, ref routeSegment, out offRouteDistance);
        currentWaypointIndex = (routeSegment + 1) % route.Count;
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float speed = Mathf.Max(0f, forwardSpeed);
        int grounded = ArcadeVehicleUtility.GroundContact(wheels, out Vector3 normal);
        if (Time.time >= lastPlanTime)
        {
            plannedSpeed = PlanCornerSpeed(speed);
            PlanTraffic(speed);
            lastPlanTime = Time.time + 0.1f;
        }
        laneOffset = Mathf.MoveTowards(laneOffset, desiredLane, Time.fixedDeltaTime * 2.5f);
        float lookAhead = minimumLookAhead + speed * lookAheadTime;
        Vector3 target = route.Sample(routeDistance + lookAhead, out Vector3 tangent);
        Vector3 pathRight = Vector3.Cross(Vector3.up, tangent).normalized;
        // Return to the route centre before tight corners or after leaving the road.
        float laneScale = Mathf.InverseLerp(10f, 27f, plannedSpeed) * (offRouteDistance < 6f ? 1f : 0f);
        target += pathRight * laneOffset * laneScale;
        Vector3 localTarget = transform.InverseTransformPoint(target);
        float distanceSquared = localTarget.x * localTarget.x + localTarget.z * localTarget.z;
        float steer = Mathf.Atan2(2f * wheelbase * localTarget.x, Mathf.Max(1f, distanceSquared)) * Mathf.Rad2Deg;
        if (localTarget.z < 0f) steer = Mathf.Sign(localTarget.x) * maxSteerAngle;
        steer = Mathf.Clamp(steer + avoidanceSteer * maxSteerAngle, -maxSteerAngle, maxSteerAngle);

        bool reversing = Time.time < reverseUntil;
        if (reversing) steer = -steer;
        currentSteer = Mathf.Lerp(currentSteer, steer, 1f - Mathf.Exp(-steerSensitivity * Time.fixedDeltaTime));
        wheelFL.steerAngle = wheelFR.steerAngle = currentSteer;
        float targetSpeed = Mathf.Min(plannedSpeed, trafficSpeedLimit);
        float error = targetSpeed - speed;
        float throttle = reversing ? -0.55f : Mathf.Clamp01(error / 6f);
        float brake = reversing ? 0f : Mathf.Clamp01(-error / 5f);
        SetMotorTorque(throttle * motorTorque);
        SetBrakes(brake * brakeTorque);

        if (grounded >= 2)
        {
            float contact = grounded * 0.25f;
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, normal).normalized;
            Vector3 right = Vector3.Cross(normal, forward);
            ArcadeVehicleUtility.ApplyDrive(rb, wheels, forward, throttle, acceleration, reversing ? 6f : maxSpeedKmh / 3.6f, contact);
            float deceleration = Mathf.Min(brake * brakingDeceleration, Mathf.Abs(forwardSpeed) / Time.fixedDeltaTime);
            rb.AddForce(-forward * Mathf.Sign(forwardSpeed) * deceleration * contact, ForceMode.Acceleration);
            float lateralSpeed = Vector3.Dot(rb.linearVelocity, right);
            rb.AddForce(right * Mathf.Clamp(-lateralSpeed * 6f, -26f, 26f) * contact, ForceMode.Acceleration);
            float desiredYaw = Mathf.Tan(currentSteer * Mathf.Deg2Rad) * forwardSpeed / wheelbase;
            desiredYaw = Mathf.Clamp(desiredYaw, -1.2f, 1.2f);
            float yaw = Vector3.Dot(rb.angularVelocity, normal);
            rb.AddTorque(normal * Mathf.Clamp((desiredYaw - yaw) * 6f, -7f, 7f) * contact, ForceMode.Acceleration);
            rb.AddForce(-normal * downforce * speed * contact);
            Vector3 tiltVelocity = Vector3.ProjectOnPlane(rb.angularVelocity, normal);
            rb.AddTorque((Vector3.Cross(transform.up, normal) * 4f - tiltVelocity * 2f) * contact, ForceMode.Acceleration);
        }

        bool blocked = rb.linearVelocity.magnitude < 2f && plannedSpeed > 3f;
        bool overturned = Vector3.Dot(transform.up, Vector3.up) < 0.25f;
        stuckTime = blocked || overturned || offRouteDistance > 24f ? stuckTime + Time.fixedDeltaTime : 0f;
        if (stuckTime > 1.5f && !reversing && Time.time > nextRecoveryTime)
        {
            reverseUntil = Time.time + 1.1f;
            nextRecoveryTime = Time.time + stuckRecoveryTime;
        }
        if (stuckTime > stuckRecoveryTime || rb.position.y < target.y - 20f) Recover();
    }

    float PlanCornerSpeed(float speed)
    {
        float cruise = maxSpeedKmh / 3.6f * Mathf.Lerp(1f - paceVariation, 1f, personality);
        // Leading opponents ease their pace slightly; trailing cars still obey their normal limits.
        RaceManager race = RaceManager.Instance;
        if (race != null && race.PlayerTracker != null && lapTracker != null)
        {
            float lead = lapTracker.RaceDistance - race.PlayerTracker.RaceDistance;
            cruise *= Mathf.Lerp(1f, 0.93f, Mathf.InverseLerp(35f, 130f, lead));
        }
        float result = cruise;
        float horizon = Mathf.Max(35f, speed * speed / (2f * brakingDeceleration) + speed * 0.7f);
        float span = Mathf.Clamp(route.Length / route.Count, 8f, 20f);
        // Compute the speed allowed now to reach each upcoming corner's safe speed in time.
        for (float ahead = 0f; ahead <= horizon; ahead += 6f)
        {
            Vector3 a = route.Sample(routeDistance + ahead - span, out _);
            Vector3 b = route.Sample(routeDistance + ahead, out _);
            Vector3 c = route.Sample(routeDistance + ahead + span, out _);
            Vector3 ab = b - a;
            Vector3 bc = c - b;
            ab.y = bc.y = 0f;
            float curvature = Vector3.Angle(ab, bc) * Mathf.Deg2Rad / Mathf.Max(1f, (ab.magnitude + bc.magnitude) * 0.5f);
            float cornerSpeed = Mathf.Sqrt(corneringAcceleration / Mathf.Max(0.008f, curvature));
            cornerSpeed = Mathf.Min(cornerSpeed, route.SpeedLimitAt(routeDistance + ahead));
            result = Mathf.Min(result, Mathf.Sqrt(cornerSpeed * cornerSpeed + 2f * brakingDeceleration * Mathf.Max(0f, ahead - 4f)));
        }
        return Mathf.Max(6f, result);
    }

    void PlanTraffic(float speed)
    {
        trafficSpeedLimit = float.PositiveInfinity;
        avoidanceSteer = 0f;
        float nearestLead = float.PositiveInfinity;
        Rigidbody leadCar = null;
        bool leftBlocked = false;
        bool rightBlocked = false;
        float range = Mathf.Max(18f, speed * 1.1f);
        Vector3 ownCentre = route.Sample(routeDistance, out Vector3 ownTangent);
        float ownLane = Vector3.Dot(rb.position - ownCentre, Vector3.Cross(Vector3.up, ownTangent).normalized);
        int count = Physics.OverlapSphereNonAlloc(rb.position, range, traffic, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Rigidbody other = traffic[i].attachedRigidbody;
            if (other == null || other == rb || (other.GetComponent<CarController>() == null &&
                other.GetComponent<SimpleAICarController>() == null)) continue;
            Vector3 local = transform.InverseTransformPoint(other.position);
            if (local.z > -5f && local.z < 12f)
            {
                if (local.x < -0.8f && local.x > -passingLaneWidth * 2.5f) leftBlocked = true;
                if (local.x > 0.8f && local.x < passingLaneWidth * 2.5f) rightBlocked = true;
            }
            int otherSegment = routeSegment;
            float otherDistance = route.Project(other.position, ref otherSegment, out float otherError);
            Vector3 otherCentre = route.Sample(otherDistance, out Vector3 otherTangent);
            float otherLane = Vector3.Dot(other.position - otherCentre, Vector3.Cross(Vector3.up, otherTangent).normalized);
            float routeGap = Mathf.Repeat(otherDistance - routeDistance + route.Length * 0.5f, route.Length) - route.Length * 0.5f;
            // Compare lanes in track space: on a bend, a car in another lane can still
            // appear directly ahead in the follower's local coordinates.
            if (routeGap > 0f && routeGap < range && otherError < 10f && Mathf.Abs(otherLane - ownLane) < 2f && routeGap < nearestLead)
            {
                nearestLead = routeGap;
                leadCar = other;
            }
        }
        if (Time.time >= laneHoldUntil) desiredLane = preferredSide * passingLaneWidth * 0.15f;
        if (leadCar != null)
        {
            float leadSpeed = Mathf.Max(0f, Vector3.Dot(leadCar.linearVelocity, transform.forward));
            float gap = 6f + speed * 0.65f;
            if (nearestLead < gap + 8f)
            {
                if (!leftBlocked && (!rightBlocked ? preferredSide < 0f : true)) desiredLane = -passingLaneWidth;
                else if (!rightBlocked) desiredLane = passingLaneWidth;
                else desiredLane = laneOffset;
                laneHoldUntil = Time.time + 2.5f;
            }
            trafficSpeedLimit = Mathf.Max(0f, leadSpeed + (nearestLead - gap) * 0.8f);
        }

        float sensorLength = Mathf.Max(frontSensorLength, speed * 0.8f + 5f);
        Vector3 origin = rb.position + transform.up * sensorHeight + transform.forward * 2.2f;
        float left = SenseObstacle(origin - transform.right * sensorSideOffset, transform.forward, sensorLength);
        float right = SenseObstacle(origin + transform.right * sensorSideOffset, transform.forward, sensorLength);
        float centre = SenseObstacle(origin, transform.forward, sensorLength);
        if (left < sensorLength || right < sensorLength || centre < sensorLength)
        {
            float distance = Mathf.Min(centre, Mathf.Min(left, right));
            trafficSpeedLimit = Mathf.Min(trafficSpeedLimit, Mathf.Sqrt(2f * brakingDeceleration * Mathf.Max(0f, distance - 2f)));
            float direction = left < right ? 1f : right < left ? -1f : preferredSide;
            avoidanceSteer = direction * Mathf.Max(0.25f, avoidanceSteerStrength) * (1f - distance / sensorLength);
        }
        float sideLeft = SenseObstacle(origin, -transform.right, sideSensorLength);
        float sideRight = SenseObstacle(origin, transform.right, sideSensorLength);
        if (sideLeft < sideSensorLength) avoidanceSteer += (1f - sideLeft / sideSensorLength) * 0.5f;
        if (sideRight < sideSensorLength) avoidanceSteer -= (1f - sideRight / sideSensorLength) * 0.5f;
        avoidanceSteer = Mathf.Clamp(avoidanceSteer, -0.65f, 0.65f);
    }

    float SenseObstacle(Vector3 origin, Vector3 direction, float distance)
    {
        int mask = obstacleLayers.value == 0 ? Physics.DefaultRaycastLayers : obstacleLayers.value;
        int count = Physics.SphereCastNonAlloc(origin, 0.3f, direction, sensorHits, distance, mask, QueryTriggerInteraction.Ignore);
        float closest = distance;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = sensorHits[i];
            // Traffic uses relative velocity above. Road surfaces and our own colliders aren't obstacles.
            if (hit.rigidbody == rb || Vector3.Dot(hit.normal, Vector3.up) > 0.65f) continue;
            if (hit.rigidbody != null && (hit.rigidbody.GetComponent<CarController>() != null ||
                hit.rigidbody.GetComponent<SimpleAICarController>() != null)) continue;
            closest = Mathf.Min(closest, hit.distance);
        }
        if (showSensorRays) Debug.DrawRay(origin, direction * closest, closest < distance ? Color.red : Color.green);
        return closest;
    }

    void Recover()
    {
        if (lapTracker == null) return;
        lapTracker.GetRecoveryPose(out Vector3 position, out Quaternion rotation);
        if (!ArcadeVehicleUtility.IsRecoverySpaceClear(position, rotation, transform)) return;
        ArcadeVehicleUtility.Recover(rb, position, rotation);
        routeSegment = -1;
        stuckTime = reverseUntil = currentSteer = 0f;
        nextRecoveryTime = Time.time + stuckRecoveryTime;
    }

    void SetMotorTorque(float torque)
    {
        if (wheels == null) return;
        foreach (WheelCollider wheel in wheels) if (wheel != null) wheel.motorTorque = torque * 0.25f;
    }

    void SetBrakes(float torque)
    {
        if (wheels == null) return;
        foreach (WheelCollider wheel in wheels) if (wheel != null) wheel.brakeTorque = torque;
    }

    void Update()
    {
        UpdateWheel(wheelFL, meshFL);
        UpdateWheel(wheelFR, meshFR);
        UpdateWheel(wheelRL, meshRL);
        UpdateWheel(wheelRR, meshRR);
    }

    static void UpdateWheel(WheelCollider wheel, Transform mesh)
    {
        if (wheel == null || mesh == null) return;
        wheel.GetWorldPose(out Vector3 position, out Quaternion rotation);
        mesh.SetPositionAndRotation(position, rotation);
    }

    void OnDisable()
    {
        SetMotorTorque(0f);
        SetBrakes(brakeTorque);
        if (rb != null && gridLocked) rb.constraints = originalConstraints;
    }
}
