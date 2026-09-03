using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider wheelFL;
    public WheelCollider wheelFR;
    public WheelCollider wheelRL;
    public WheelCollider wheelRR;

    [Header("Wheel Meshes")]
    public Transform meshFL;
    public Transform meshFR;
    public Transform meshRL;
    public Transform meshRR;

    [Header("Center Of Mass")]
    public Transform centerOfMass;
    public Vector3 fallbackCOM = new Vector3(0f, -0.65f, 0.05f);

    [Header("Engine")]
    public float motorPower = 2200f;
    public float brakePower = 2500f;
    public float topSpeedKmh = 220f;

    [Header("Steering")]
    public float maxSteerAngle = 42f;
    public float steerSpeed = 8f;
    public float highSpeedSteerReduction = 0.4f;

    [Header("Drift")]
    public float driftMotorBoost = 1.3f;
    public float driftBrakeRear = 0f;
    public float driftSidewaysFriction = 0.6f;
    public float normalSidewaysFriction = 1f;
    public float driftForwardFriction = 0.8f;
    public float normalForwardFriction = 1f;

    [Header("Brake Lights")]
    public Light[] brakeLights;
    public float brakeLightIntensity = 2.5f;

    [Header("Body Visual")]
    public Transform carVisual;
    public float bodyRollAmount = 5f;
    public float bodyPitchAmount = 3f;
    public float bodyRollSpeed = 8f;

    Rigidbody rb;

    private float gasInput;
    private float brakeInput;
    private float steeringInput;
    private float currentSteerAngle;
    private float lateralG;
    private float slipAngle;
    private bool isDrifting;
    private float driftFactor;
    Quaternion visualStartRot;
    private Quaternion meshFLLocalRot, meshFRLocalRot, meshRLLocalRot, meshRRLocalRot;

    WheelFrictionCurve flForward, flSideways;
    WheelFrictionCurve frForward, frSideways;
    WheelFrictionCurve rlForward, rlSideways;
    WheelFrictionCurve rrForward, rrSideways;

    public float SpeedKmh { get; private set; }
    public float ThrottleInput => gasInput;
    public float EngineLoad => Mathf.Clamp01(Mathf.Abs(gasInput));
    public bool IsHandbraking => Input.GetKey(KeyCode.Space);
    public float LateralG => lateralG;
    public bool IsDrifting => isDrifting;
    public float DriftAngle { get; private set; }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = 1300f;
        rb.angularDamping = 0.05f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        rb.centerOfMass = centerOfMass != null
            ? transform.InverseTransformPoint(centerOfMass.position)
            : fallbackCOM;

        visualStartRot = carVisual != null ? carVisual.localRotation : Quaternion.identity;

        meshFLLocalRot = meshFL != null ? meshFL.localRotation : Quaternion.identity;
        meshFRLocalRot = meshFR != null ? meshFR.localRotation : Quaternion.identity;
        meshRLLocalRot = meshRL != null ? meshRL.localRotation : Quaternion.identity;
        meshRRLocalRot = meshRR != null ? meshRR.localRotation : Quaternion.identity;

        CacheFrictionCurves();
    }

    void CacheFrictionCurves()
    {
        flForward = wheelFL.forwardFriction;
        flSideways = wheelFL.sidewaysFriction;
        frForward = wheelFR.forwardFriction;
        frSideways = wheelFR.sidewaysFriction;
        rlForward = wheelRL.forwardFriction;
        rlSideways = wheelRL.sidewaysFriction;
        rrForward = wheelRR.forwardFriction;
        rrSideways = wheelRR.sidewaysFriction;
    }

    void Update()
    {
        UpdateBodyVisual();
        HandleBrakeLights();
    }

    void LateUpdate()
    {
        UpdateWheelMeshes();
    }

    void FixedUpdate()
    {
        ReadInput();

        if (RaceManager.Instance != null && (!RaceManager.Instance.raceStarted || RaceManager.Instance.raceFinished))
        {
            ApplyBrakeTorque(300f);
            ApplyMotor(0f);
            return;
        }

        SpeedKmh = rb.linearVelocity.magnitude * 3.6f;

        DetectDrift();
        ApplyMotor(GetMotorTorque());
        ApplySteering();
        ApplyBraking();
        ApplyDriftFriction();
        CalculateLateralG();
    }

    void ReadInput()
    {
        gasInput = Input.GetAxis("Vertical");
        steeringInput = Input.GetAxis("Horizontal");

        Vector3 vel = rb.linearVelocity;
        vel.y = 0f;
        slipAngle = vel.sqrMagnitude > 1f
            ? Vector3.Angle(transform.forward, vel)
            : 0f;

        float movingDir = Vector3.Dot(transform.forward, rb.linearVelocity);

        if (movingDir < -0.5f && gasInput > 0)
        {
            brakeInput = gasInput;
            gasInput = 0f;
        }
        else if (movingDir > 0.5f && gasInput < -0.1f)
        {
            brakeInput = -gasInput;
            gasInput = 0f;
        }
        else
        {
            brakeInput = 0f;
        }
    }

    float GetMotorTorque()
    {
        float speedRatio = SpeedKmh / topSpeedKmh;

        if (speedRatio >= 1f && gasInput > 0f)
            return 0f;

        float power = motorPower;

        if (isDrifting)
            power *= driftMotorBoost;

        return power * gasInput;
    }

    void ApplyMotor(float torque)
    {
        wheelRL.motorTorque = torque;
        wheelRR.motorTorque = torque;
    }

    void ApplySteering()
    {
        float speedFactor = Mathf.Clamp01(SpeedKmh / topSpeedKmh);
        float steerLimit = Mathf.Lerp(1f, highSpeedSteerReduction, speedFactor);
        float targetAngle = steeringInput * maxSteerAngle * steerLimit;

        if (isDrifting && Mathf.Abs(steeringInput) > 0.1f)
        {
            float driftAssist = Mathf.Sign(steeringInput) * driftFactor * 10f;
            targetAngle += driftAssist;
        }

        currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetAngle, Time.fixedDeltaTime * steerSpeed);

        wheelFL.steerAngle = currentSteerAngle;
        wheelFR.steerAngle = currentSteerAngle;
    }

    void ApplyBraking()
    {
        if (brakeInput > 0.1f)
        {
            float frontBrake = brakePower * brakeInput;
            float rearBrake = isDrifting ? brakePower * brakeInput * driftBrakeRear : brakePower * brakeInput * 0.3f;

            wheelFL.brakeTorque = frontBrake;
            wheelFR.brakeTorque = frontBrake;
            wheelRL.brakeTorque = rearBrake;
            wheelRR.brakeTorque = rearBrake;
        }
        else if (Input.GetKey(KeyCode.Space))
        {
            wheelFL.brakeTorque = 0f;
            wheelFR.brakeTorque = 0f;
            wheelRL.brakeTorque = brakePower * 0.2f;
            wheelRR.brakeTorque = brakePower * 0.2f;
        }
        else
        {
            ApplyBrakeTorque(0f);
        }
    }

    void ApplyBrakeTorque(float torque)
    {
        wheelFL.brakeTorque = torque;
        wheelFR.brakeTorque = torque;
        wheelRL.brakeTorque = torque;
        wheelRR.brakeTorque = torque;
    }

    void DetectDrift()
    {
        bool handbrake = Input.GetKey(KeyCode.Space);
        bool speedCheck = SpeedKmh > 15f;
        bool angleCheck = slipAngle > 15f;
        bool powerOversteer = gasInput > 0.1f && SpeedKmh > 30f && Mathf.Abs(lateralG) > 0.4f;

        isDrifting = speedCheck && (handbrake || angleCheck || powerOversteer);

        float targetDrift = isDrifting ? Mathf.InverseLerp(15f, 40f, slipAngle) : 0f;
        driftFactor = Mathf.Lerp(driftFactor, targetDrift, Time.fixedDeltaTime * 6f);

        DriftAngle = Mathf.Lerp(DriftAngle, isDrifting ? slipAngle : 0f, Time.fixedDeltaTime * 5f);
    }

    void ApplyDriftFriction()
    {
        float sidewaysTarget = isDrifting ? driftSidewaysFriction : normalSidewaysFriction;
        float forwardTarget = isDrifting ? driftForwardFriction : normalForwardFriction;

        rlSideways.stiffness = Mathf.Lerp(rlSideways.stiffness, sidewaysTarget, Time.fixedDeltaTime * 8f);
        rrSideways.stiffness = Mathf.Lerp(rrSideways.stiffness, sidewaysTarget, Time.fixedDeltaTime * 8f);
        rlForward.stiffness = Mathf.Lerp(rlForward.stiffness, forwardTarget, Time.fixedDeltaTime * 8f);
        rrForward.stiffness = Mathf.Lerp(rrForward.stiffness, forwardTarget, Time.fixedDeltaTime * 8f);

        flSideways.stiffness = Mathf.Lerp(flSideways.stiffness, isDrifting ? 0.9f : normalSidewaysFriction, Time.fixedDeltaTime * 8f);
        frSideways.stiffness = Mathf.Lerp(frSideways.stiffness, isDrifting ? 0.9f : normalSidewaysFriction, Time.fixedDeltaTime * 8f);

        wheelRL.sidewaysFriction = rlSideways;
        wheelRR.sidewaysFriction = rrSideways;
        wheelRL.forwardFriction = rlForward;
        wheelRR.forwardFriction = rrForward;
        wheelFL.sidewaysFriction = flSideways;
        wheelFR.sidewaysFriction = frSideways;
    }

    void CalculateLateralG()
    {
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        float targetLateralG = localVel.x / 9.81f;
        lateralG = Mathf.Lerp(lateralG, targetLateralG, Time.fixedDeltaTime * 10f);
    }

    void UpdateWheelMeshes()
    {
        UpdateSingleWheel(wheelFL, meshFL, meshFLLocalRot);
        UpdateSingleWheel(wheelFR, meshFR, meshFRLocalRot);
        UpdateSingleWheel(wheelRL, meshRL, meshRLLocalRot);
        UpdateSingleWheel(wheelRR, meshRR, meshRRLocalRot);
    }

    void UpdateSingleWheel(WheelCollider col, Transform mesh, Quaternion baseRot)
    {
        if (col == null || mesh == null) return;
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        mesh.position = pos;
        mesh.rotation = rot * baseRot;
    }

    void UpdateBodyVisual()
    {
        if (carVisual == null) return;

        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        float roll = -lateralG * bodyRollAmount;
        float pitch = -localVel.z * bodyPitchAmount / 35f;

        Quaternion target = visualStartRot * Quaternion.Euler(pitch, 0f, roll);
        carVisual.localRotation = Quaternion.Slerp(
            carVisual.localRotation,
            target,
            Time.deltaTime * bodyRollSpeed
        );
    }

    void HandleBrakeLights()
    {
        bool braking = brakeInput > 0.1f || Input.GetKey(KeyCode.Space);
        float targetIntensity = braking ? brakeLightIntensity : 0f;

        foreach (Light light in brakeLights)
        {
            if (light == null) continue;
            light.intensity = Mathf.Lerp(light.intensity, targetIntensity, Time.deltaTime * 12f);
        }
    }
}
