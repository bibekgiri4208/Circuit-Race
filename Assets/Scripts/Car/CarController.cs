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
    public float maxMotorTorque = 3200f;
    public float topSpeedKmh = 240f;
    public AnimationCurve powerCurve = new AnimationCurve(
        new Keyframe(0f, 0.6f),
        new Keyframe(0.3f, 1f),
        new Keyframe(0.7f, 0.95f),
        new Keyframe(1f, 0.7f)
    );
    public float engineBraking = 400f;

    [Header("Steering")]
    public float maxSteerAngle = 35f;
    public AnimationCurve steerCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.4f, 0.7f),
        new Keyframe(0.8f, 0.4f),
        new Keyframe(1f, 0.25f)
    );
    public float steerSpeed = 8f;

    [Header("Brakes")]
    public float brakeTorque = 7000f;
    public float brakeBias = 0.6f;
    public float idleBrakeTorque = 250f;
    public float handbrakeTorque = 9000f;

    [Header("Downforce")]
    public float downforce = 120f;
    public AnimationCurve downforceCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.3f, 0.4f),
        new Keyframe(1f, 0.8f)
    );

    [Header("Drift")]
    public float driftRearGrip = 0.55f;
    public float driftFrontGrip = 0.9f;
    public float driftCounterSteerSpeed = 14f;
    public float driftCounterSteerStrength = 0.45f;
    public float driftThrottleBoost = 1.1f;
    public float driftEntrySpeedMin = 30f;
    public float driftSideSlipThreshold = 0.18f;
    public float driftExitSlipThreshold = 0.06f;
    public float driftGripBlendSpeed = 5f;

    [Header("Normal Grip")]
    public float frontGripMultiplier = 1f;
    public float rearGripMultiplier = 1f;
    public float speedGripReduction = 0.1f;

    [Header("Brake Lights")]
    public Light[] brakeLights;
    public float brakeLightIntensity = 2.5f;

    [Header("Body Visual")]
    public Transform carVisual;
    public float bodyRollAmount = 5f;
    public float bodyPitchAmount = 3f;
    public float bodyRollSpeed = 8f;

    Rigidbody rb;

    private float throttle;
    private float steerInput;
    private float brakeInput;
    private bool handbrake;
    private float currentSteerAngle;
    private float lateralG;
    Quaternion visualStartRot;

    private bool isDrifting;
    private float driftAngle;
    private float currentDriftBlend;

    public float SpeedKmh { get; private set; }
    public float ThrottleInput => throttle;
    public bool IsHandbraking => handbrake;
    public float EngineLoad { get; private set; }
    public float LateralG => lateralG;
    public bool IsDrifting => isDrifting;
    public float DriftAngle => driftAngle;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = 1300f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        rb.centerOfMass = centerOfMass != null
            ? transform.InverseTransformPoint(centerOfMass.position)
            : fallbackCOM;

        visualStartRot = carVisual != null ? carVisual.localRotation : Quaternion.identity;

        ConfigureWheelColliders();
    }

    void ConfigureWheelColliders()
    {
        WheelCollider[] wheels = { wheelFL, wheelFR, wheelRL, wheelRR };
        float[] grip = { frontGripMultiplier, frontGripMultiplier, rearGripMultiplier, rearGripMultiplier };

        for (int i = 0; i < 4; i++)
        {
            if (wheels[i] == null) continue;

            WheelFrictionCurve fwd = wheels[i].forwardFriction;
            fwd.extremumSlip = 0.4f;
            fwd.extremumValue = 1f;
            fwd.asymptoteSlip = 0.8f;
            fwd.asymptoteValue = 0.5f;
            fwd.stiffness = grip[i];
            wheels[i].forwardFriction = fwd;

            WheelFrictionCurve side = wheels[i].sidewaysFriction;
            side.extremumSlip = 0.15f;
            side.extremumValue = 1f;
            side.asymptoteSlip = 0.45f;
            side.asymptoteValue = 0.7f;
            side.stiffness = grip[i];
            wheels[i].sidewaysFriction = side;
        }
    }

    void Update()
    {
        UpdateWheelMeshes();
        UpdateBodyVisual();
        HandleBrakeLights();
    }

    void FixedUpdate()
    {
        throttle = Mathf.Clamp01(Input.GetAxis("Vertical"));
        steerInput = Mathf.Clamp(Input.GetAxis("Horizontal"), -1f, 1f);
        brakeInput = Mathf.Clamp01(-Input.GetAxis("Vertical"));
        handbrake = Input.GetKey(KeyCode.Space);

        if (RaceManager.Instance != null && (!RaceManager.Instance.raceStarted || RaceManager.Instance.raceFinished))
        {
            SetMotorTorque(0f);
            SetBrakeTorque(idleBrakeTorque);
            return;
        }

        SpeedKmh = rb.linearVelocity.magnitude * 3.6f;
        float speedFactor = Mathf.Clamp01(SpeedKmh / topSpeedKmh);

        CalculateLateralG();
        DetectDrift();
        HandleSteering(speedFactor);
        HandleMotorAndBrakes(speedFactor);
        ApplyDownforce(speedFactor);
        AdjustGripForSpeed(speedFactor);
        ApplyAngularDrag(speedFactor);
    }

    void CalculateLateralG()
    {
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        float targetLateralG = localVel.x / 9.81f;
        lateralG = Mathf.Lerp(lateralG, targetLateralG, Time.fixedDeltaTime * 10f);
    }

    void DetectDrift()
    {
        float sideSlip = Mathf.Abs(GetRearSlipAngle());
        bool handbrakeDrift = handbrake && SpeedKmh > driftEntrySpeedMin;
        bool powerOversteer = throttle > 0.3f && sideSlip > driftSideSlipThreshold && SpeedKmh > driftEntrySpeedMin;
        bool brakingDrift = brakeInput > 0.3f && SpeedKmh > driftEntrySpeedMin && sideSlip > driftSideSlipThreshold * 0.7f;

        bool wantsDrift = handbrakeDrift || powerOversteer || brakingDrift;

        if (wantsDrift && !isDrifting)
        {
            isDrifting = true;
        }
        else if (isDrifting && sideSlip < driftExitSlipThreshold && !handbrake)
        {
            isDrifting = false;
        }

        float targetBlend = isDrifting ? 1f : 0f;
        currentDriftBlend = Mathf.Lerp(currentDriftBlend, targetBlend, Time.fixedDeltaTime * driftGripBlendSpeed);

        if (isDrifting)
        {
            driftAngle = Mathf.Lerp(driftAngle, sideSlip * Mathf.Rad2Deg * 3f, Time.fixedDeltaTime * 8f);
        }
        else
        {
            driftAngle = Mathf.Lerp(driftAngle, 0f, Time.fixedDeltaTime * 6f);
        }
    }

    void HandleMotorAndBrakes(float speedFactor)
    {
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        SetBrakeTorque(0f);
        SetMotorTorque(0f);

        float powerMultiplier = powerCurve.Evaluate(speedFactor);
        float torque = maxMotorTorque * powerMultiplier;

        if (isDrifting)
        {
            torque *= driftThrottleBoost;
        }

        bool canDrive = throttle > 0.05f && SpeedKmh < topSpeedKmh;

        if (canDrive)
        {
            SetMotorTorque(throttle * torque);
            EngineLoad = throttle;
        }
        else
        {
            EngineLoad = 0.1f;
        }

        if (brakeInput > 0.05f)
        {
            if (forwardSpeed > 1f)
            {
                float frontBrake = brakeInput * brakeTorque * brakeBias;
                float rearBrake = brakeInput * brakeTorque * (1f - brakeBias);
                wheelFL.brakeTorque = frontBrake;
                wheelFR.brakeTorque = frontBrake;
                wheelRL.brakeTorque = rearBrake;
                wheelRR.brakeTorque = rearBrake;
            }
            else
            {
                SetMotorTorque(-brakeInput * maxMotorTorque * 0.4f);
            }
            EngineLoad = Mathf.Max(EngineLoad, brakeInput);
        }

        if (throttle < 0.05f && brakeInput < 0.05f)
        {
            float engineDrag = engineBraking * Mathf.Clamp01(SpeedKmh / 60f);
            SetBrakeTorque(idleBrakeTorque + engineDrag);
        }

        if (handbrake)
        {
            wheelRL.brakeTorque = handbrakeTorque;
            wheelRR.brakeTorque = handbrakeTorque;
        }
    }

    void HandleSteering(float speedFactor)
    {
        float steerLimit = steerCurve.Evaluate(speedFactor);
        float targetSteer = steerInput * maxSteerAngle * steerLimit;

        if (currentDriftBlend > 0.05f)
        {
            float slipAngle = GetRearSlipAngle();
            float counterSteer = -slipAngle * maxSteerAngle * driftCounterSteerStrength;
            counterSteer = Mathf.Clamp(counterSteer, -maxSteerAngle * 0.7f, maxSteerAngle * 0.7f);
            targetSteer = Mathf.Lerp(targetSteer, targetSteer + counterSteer, currentDriftBlend);
        }
        else
        {
            float slipAngle = GetRearSlipAngle();
            if (Mathf.Abs(slipAngle) > 5f)
            {
                float counterSteer = -Mathf.Sign(slipAngle) * Mathf.Clamp01((Mathf.Abs(slipAngle) - 5f) / 20f) * maxSteerAngle * 0.3f;
                targetSteer += counterSteer;
            }
        }

        float steerSpd = currentDriftBlend > 0.05f ? driftCounterSteerSpeed : steerSpeed;
        currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, targetSteer, steerSpd * maxSteerAngle * Time.fixedDeltaTime);

        wheelFL.steerAngle = currentSteerAngle;
        wheelFR.steerAngle = currentSteerAngle;
    }

    float GetRearSlipAngle()
    {
        if (wheelRL == null) return 0f;
        WheelHit hit;
        if (wheelRL.GetGroundHit(out hit))
        {
            return hit.sidewaysSlip;
        }
        return 0f;
    }

    void ApplyDownforce(float speedFactor)
    {
        float dfMultiplier = downforceCurve.Evaluate(speedFactor);
        rb.AddForce(-transform.up * downforce * dfMultiplier * rb.linearVelocity.sqrMagnitude * 0.01f, ForceMode.Force);
    }

    void AdjustGripForSpeed(float speedFactor)
    {
        float reduction = 1f - speedFactor * speedGripReduction;

        float fg = Mathf.Lerp(frontGripMultiplier, driftFrontGrip, currentDriftBlend) * reduction;
        float rg = Mathf.Lerp(rearGripMultiplier, driftRearGrip, currentDriftBlend) * reduction;

        WheelFrictionCurve fwd = wheelFL.forwardFriction;
        fwd.stiffness = fg;
        wheelFL.forwardFriction = fwd;
        wheelFR.forwardFriction = fwd;

        WheelFrictionCurve side = wheelFL.sidewaysFriction;
        side.stiffness = fg;
        wheelFL.sidewaysFriction = side;
        wheelFR.sidewaysFriction = side;

        fwd = wheelRL.forwardFriction;
        fwd.stiffness = rg;
        wheelRL.forwardFriction = fwd;
        wheelRR.forwardFriction = fwd;

        side = wheelRL.sidewaysFriction;
        side.stiffness = rg;
        wheelRL.sidewaysFriction = side;
        wheelRR.sidewaysFriction = side;
    }

    void ApplyAngularDrag(float speedFactor)
    {
        float baseDrag = Mathf.Lerp(1.8f, 3.0f, speedFactor);
        float drag = Mathf.Lerp(baseDrag, baseDrag * 0.75f, currentDriftBlend);
        rb.angularDamping = drag;
    }

    void SetMotorTorque(float torque)
    {
        wheelRL.motorTorque = torque;
        wheelRR.motorTorque = torque;
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
        UpdateSingleWheel(wheelFL, meshFL);
        UpdateSingleWheel(wheelFR, meshFR);
        UpdateSingleWheel(wheelRL, meshRL);
        UpdateSingleWheel(wheelRR, meshRR);
    }

    void UpdateSingleWheel(WheelCollider col, Transform mesh)
    {
        if (col == null || mesh == null) return;
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);
        mesh.position = pos;
        mesh.rotation = rot;
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
        bool braking = brakeInput > 0.1f || handbrake;
        float targetIntensity = braking ? brakeLightIntensity : 0f;

        foreach (Light light in brakeLights)
        {
            if (light == null) continue;
            light.intensity = Mathf.Lerp(light.intensity, targetIntensity, Time.deltaTime * 12f);
        }
    }
}
