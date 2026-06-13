using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class SimcadeCarController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider wheelFL, wheelFR, wheelRL, wheelRR;

    [Header("Wheel Meshes")]
    public Transform meshFL, meshFR, meshRL, meshRR;

    [Header("Center Of Mass")]
    public Transform centerOfMass;
    public Vector3 fallbackCOM = new Vector3(0f, -0.62f, 0.12f);

    [Header("Engine")]
    public float motorTorque = 2600f;
    public float reverseTorque = 1200f;
    public float topSpeedKmh = 185f;

    [Header("Drift Power")]
    public float driftTorqueMultiplier = 1.5f;

    [Header("Steering")]
    public float maxSteerAngle = 42f;
    public float steerResponse = 11f;
    public float highSpeedSteerLimit = 0.82f;

    [Header("Brakes")]
    public float brakeTorque = 5000f;
    public float idleBrakeTorque = 250f;
    public float handbrakeTorque = 6500f;

    [Header("Brake Lights")]
    public Light[] brakeLights;
    public float brakeLightIntensity = 2.5f;

    [Header("Drift Grip")]
    public float normalRearSidewaysStiffness = 1.08f;
    public float driftRearSidewaysStiffness = 0.62f;
    public float handbrakeRearSidewaysStiffness = 0.42f;
    public float frontSidewaysStiffness = 1.55f;
    public float minDriftSpeedKmh = 30f;

    [Header("Slip Angle Drift")]
    public float driftStartAngle = 8f;
    public float fullDriftAngle = 28f;
    public float maxSlipAssist = 3.5f;
    public float counterSteerStability = 2.5f;
    public float handbrakeInitiationBoost = 1.8f;

    [Header("Grip Recovery")]
    public float gripChangeSpeed = 5f;

    [Header("Stability")]
    public float downforce = 70f;
    public float angularDragNormal = 1.3f;
    public float angularDragDrift = 0.95f;

    [Header("Visual Body Roll")]
    public Transform carVisual;
    public float bodyRollAmount = 7f;
    public float bodyPitchAmount = 4f;
    public float bodyRollSpeed = 7f;

    Rigidbody rb;

    float throttle;
    float steerInput;
    float brakeInput;
    bool handbrake;

    float currentSteerAngle;
    Quaternion visualStartRot;

    public float SpeedKmh { get; private set; }
    public float ThrottleInput => throttle;
    public bool IsHandbraking => handbrake;
    public bool IsDrifting { get; private set; }
    public float EngineLoad { get; private set; }
    public float DriftAngle { get; private set; }

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        rb.mass = 1200f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.angularDamping = angularDragNormal;

        rb.centerOfMass = centerOfMass != null
            ? transform.InverseTransformPoint(centerOfMass.position)
            : fallbackCOM;

        visualStartRot = carVisual != null ? carVisual.localRotation : Quaternion.identity;

        SetupWheelFriction();
    }

    void Update()
    {
        ReadInput();
        UpdateWheelMeshes();
        UpdateBodyVisual();
        HandleBrakeLights();

        if (RaceManager.Instance != null && !RaceManager.Instance.raceStarted)
        {
            return;
        }
    }

    void FixedUpdate()
    {
        if (RaceManager.Instance != null && !RaceManager.Instance.raceStarted)
        {
            return;
        }

        SpeedKmh = rb.linearVelocity.magnitude * 3.6f;

        HandleSteering();
        HandleMotorAndBrakes();
        HandleSlipAngleDrift();
        ApplyDownforce();
    }

    void ReadInput()
    {
        throttle = 0f;
        steerInput = 0f;
        brakeInput = 0f;
        handbrake = false;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed)
                throttle = 1f;

            if (Keyboard.current.sKey.isPressed)
                brakeInput = 1f;

            if (Keyboard.current.aKey.isPressed)
                steerInput = -1f;

            if (Keyboard.current.dKey.isPressed)
                steerInput = 1f;

            handbrake =
                Keyboard.current.spaceKey.isPressed ||
                Keyboard.current.eKey.isPressed;
        }

        if (Gamepad.current != null)
        {
            float rt = Gamepad.current.rightTrigger.ReadValue();
            float lt = Gamepad.current.leftTrigger.ReadValue();
            float stickX = Gamepad.current.leftStick.x.ReadValue();

            if (rt < 0.08f) rt = 0f;
            if (lt < 0.08f) lt = 0f;
            if (Mathf.Abs(stickX) < 0.12f) stickX = 0f;

            throttle = Mathf.Pow(rt, 0.65f);
            brakeInput = Mathf.Pow(lt, 0.65f);

            steerInput =
                Mathf.Sign(stickX) *
                Mathf.Pow(Mathf.Abs(stickX), 0.75f);

            handbrake =
                Gamepad.current.buttonSouth.isPressed ||
                Gamepad.current.rightShoulder.isPressed;
        }

        throttle = Mathf.Clamp01(throttle);
        brakeInput = Mathf.Clamp01(brakeInput);
        steerInput = Mathf.Clamp(steerInput, -1f, 1f);
    }

    void HandleMotorAndBrakes()
    {
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float speedFactor = Mathf.Clamp01(SpeedKmh / topSpeedKmh);

        float torqueMultiplier = Mathf.Lerp(
            1f,
            0.45f,
            speedFactor
        );

        SetBrakeTorque(0f);
        SetMotorTorque(0f);

        if (throttle > 0.05f && SpeedKmh < topSpeedKmh)
        {
            float torque = motorTorque * torqueMultiplier;

            if (IsDrifting)
            {
                torque *= driftTorqueMultiplier;
            }

            SetMotorTorque(throttle * torque);

            EngineLoad = throttle;
        }
        else
        {
            EngineLoad = 0.15f;
        }

        if (brakeInput > 0.05f)
        {
            if (forwardSpeed > 2f)
            {
                SetBrakeTorque(brakeInput * brakeTorque);
            }
            else
            {
                SetMotorTorque(-brakeInput * reverseTorque);
            }

            EngineLoad = Mathf.Max(EngineLoad, brakeInput);
        }

        if (throttle < 0.05f && brakeInput < 0.05f)
        {
            SetBrakeTorque(idleBrakeTorque);
        }

        if (handbrake)
        {
            wheelRL.brakeTorque = handbrakeTorque;
            wheelRR.brakeTorque = handbrakeTorque;

            wheelFL.brakeTorque = 0f;
            wheelFR.brakeTorque = 0f;
        }
    }

    void HandleSteering()
    {
        float speed01 = Mathf.Clamp01(SpeedKmh / topSpeedKmh);
        float steerLimit = Mathf.Lerp(1f, highSpeedSteerLimit, speed01);

        float targetSteer = steerInput * maxSteerAngle * steerLimit;

        currentSteerAngle = Mathf.Lerp(
            currentSteerAngle,
            targetSteer,
            Time.fixedDeltaTime * steerResponse
        );

        wheelFL.steerAngle = currentSteerAngle;
        wheelFR.steerAngle = currentSteerAngle;
    }

    void HandleSlipAngleDrift()
    {
        Vector3 localVelocity =
            transform.InverseTransformDirection(rb.linearVelocity);

        float sidewaysSpeed = localVelocity.x;
        float forwardSpeed = Mathf.Abs(localVelocity.z);

        DriftAngle =
            Mathf.Atan2(Mathf.Abs(sidewaysSpeed), Mathf.Max(forwardSpeed, 0.1f))
            * Mathf.Rad2Deg;

        bool enoughSpeed = SpeedKmh > minDriftSpeedKmh;

        bool handbrakeInitiate =
            handbrake &&
            enoughSpeed &&
            Mathf.Abs(steerInput) > 0.12f;

        bool naturalDrift =
            enoughSpeed &&
            DriftAngle > driftStartAngle &&
            Mathf.Abs(sidewaysSpeed) > 1.5f;

        bool throttleDrift =
            enoughSpeed &&
            throttle > 0.35f &&
            Mathf.Abs(steerInput) > 0.55f &&
            DriftAngle > driftStartAngle * 0.6f;

        IsDrifting = handbrakeInitiate || naturalDrift || throttleDrift;

        float driftAmount = Mathf.InverseLerp(
            driftStartAngle,
            fullDriftAngle,
            DriftAngle
        );

        if (handbrakeInitiate)
            driftAmount = Mathf.Max(driftAmount, 0.75f);

        float targetRearGrip = normalRearSidewaysStiffness;

        if (IsDrifting)
        {
            targetRearGrip = Mathf.Lerp(
                normalRearSidewaysStiffness,
                driftRearSidewaysStiffness,
                driftAmount
            );

            if (handbrake)
            {
                targetRearGrip = Mathf.Lerp(
                    driftRearSidewaysStiffness,
                    handbrakeRearSidewaysStiffness,
                    0.75f
                );
            }
        }

        SetSidewaysStiffness(wheelRL, targetRearGrip);
        SetSidewaysStiffness(wheelRR, targetRearGrip);
        SetSidewaysStiffness(wheelFL, frontSidewaysStiffness);
        SetSidewaysStiffness(wheelFR, frontSidewaysStiffness);

        rb.angularDamping = IsDrifting ? angularDragDrift : angularDragNormal;

        if (!IsDrifting) return;

        float speedFactor = Mathf.Clamp01(SpeedKmh / 150f);

        float assistStrength = maxSlipAssist * driftAmount;
        assistStrength *= Mathf.Lerp(1f, 0.65f, speedFactor);

        if (handbrakeInitiate)
            assistStrength += handbrakeInitiationBoost;

        rb.AddTorque(
            Vector3.up * steerInput * assistStrength,
            ForceMode.Acceleration
        );

        bool counterSteering =
            Mathf.Sign(steerInput) != Mathf.Sign(sidewaysSpeed) &&
            Mathf.Abs(steerInput) > 0.2f &&
            Mathf.Abs(sidewaysSpeed) > 0.5f;

        if (counterSteering)
        {
            float yawDirection = Mathf.Sign(sidewaysSpeed);

            rb.AddTorque(
                Vector3.up * -yawDirection * counterSteerStability * driftAmount,
                ForceMode.Acceleration
            );
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

    void SetSidewaysStiffness(WheelCollider wheel, float target)
    {
        WheelFrictionCurve friction = wheel.sidewaysFriction;

        friction.stiffness = Mathf.Lerp(
            friction.stiffness,
            target,
            Time.fixedDeltaTime * gripChangeSpeed
        );

        wheel.sidewaysFriction = friction;
    }

    void SetupWheelFriction()
    {
        SetSidewaysStiffness(wheelFL, frontSidewaysStiffness);
        SetSidewaysStiffness(wheelFR, frontSidewaysStiffness);
        SetSidewaysStiffness(wheelRL, normalRearSidewaysStiffness);
        SetSidewaysStiffness(wheelRR, normalRearSidewaysStiffness);
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

        Vector3 localVel =
            transform.InverseTransformDirection(rb.linearVelocity);

        float roll = -localVel.x * bodyRollAmount / 12f;
        float pitch = -localVel.z * bodyPitchAmount / 35f;

        Quaternion target =
            visualStartRot * Quaternion.Euler(pitch, 0f, roll);

        carVisual.localRotation = Quaternion.Slerp(
            carVisual.localRotation,
            target,
            Time.deltaTime * bodyRollSpeed
        );
    }

    void HandleBrakeLights()
    {
        bool braking =
            brakeInput > 0.1f ||
            handbrake;

        float targetIntensity =
            braking ? brakeLightIntensity : 0f;

        foreach (Light light in brakeLights)
        {
            if (light == null) continue;

            light.intensity = Mathf.Lerp(
                light.intensity,
                targetIntensity,
                Time.deltaTime * 12f
            );
        }
    }
}