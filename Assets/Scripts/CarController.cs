using UnityEngine;
using UnityEngine.InputSystem;

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

    [Header("Visual")]
    public Transform carVisual;

    [Header("Engine")]
    public float motorPower = 3200f;
    public float reversePower = 1800f;
    public float topSpeed = 220f;

    [Header("Steering")]
    public float maxSteerAngle = 32f;
    public float highSpeedSteerAngle = 12f;
    public float steeringSmoothness = 5f;

    [Header("Brakes")]
    public float brakePower = 4500f;
    public float handbrakePower = 12000f;

    [Header("Drift")]
    public float normalRearGrip = 2.2f;
    public float driftRearGrip = 0.55f;
    public float driftAssist = 10f;

    [Header("Stability")]
    public float downforce = 80f;
    public float antiRoll = 6000f;

    [Header("Transmission")]
    public int CurrentGear = 1;
    public int maxGear = 6;
    public float EngineRPM;
    public float idleRPM = 900f;
    public float maxRPM = 7500f;

    [Header("Stats")]
    public float SpeedKmh;

    [HideInInspector] public float moveInput;
    [HideInInspector] public float steerInput;
    [HideInInspector] public bool handbrake;

    Rigidbody rb;

    float currentSteerAngle;

    Quaternion visualStartRot;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        rb.mass = 1400f;

        // Center of mass object
        rb.centerOfMass =
            transform.Find("CenterOfMass").localPosition;

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        visualStartRot = carVisual.localRotation;

        SetupWheelFriction();
    }

    void Update()
    {
        ReadInput();

        UpdateWheelMeshes();

        UpdateVisualRoll();
    }

    void FixedUpdate()
    {
        SpeedKmh = rb.linearVelocity.magnitude * 3.6f;

        HandleMotor();

        HandleSteering();

        HandleBrakes();

        HandleDrift();

        ApplyDownforce();

        ApplyAntiRoll();

        UpdateTransmission();
    }

    void ReadInput()
    {
        moveInput = 0f;
        steerInput = 0f;
        handbrake = false;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed)
                moveInput = 1f;

            if (Keyboard.current.sKey.isPressed)
                moveInput = -1f;

            if (Keyboard.current.aKey.isPressed)
                steerInput = -1f;

            if (Keyboard.current.dKey.isPressed)
                steerInput = 1f;

            handbrake = Keyboard.current.spaceKey.isPressed;
        }

        if (Gamepad.current != null)
        {
            moveInput =
                Gamepad.current.rightTrigger.ReadValue()
                - Gamepad.current.leftTrigger.ReadValue();

            steerInput = Gamepad.current.leftStick.x.ReadValue();

            handbrake = Gamepad.current.rightShoulder.isPressed;
        }
    }

    void HandleMotor()
    {
        float speedFactor = Mathf.Clamp01(SpeedKmh / topSpeed);

        float powerCurve = 1f - (speedFactor * speedFactor);

        float torque = motorPower * moveInput * powerCurve;

        if (moveInput > 0f)
        {
            wheelRL.motorTorque = torque;
            wheelRR.motorTorque = torque;
        }
        else if (moveInput < 0f)
        {
            wheelRL.motorTorque = moveInput * reversePower;
            wheelRR.motorTorque = moveInput * reversePower;
        }
        else
        {
            wheelRL.motorTorque = 0f;
            wheelRR.motorTorque = 0f;
        }
    }

    void HandleSteering()
    {
        float speedPercent = Mathf.Clamp01(SpeedKmh / topSpeed);

        float steerLimit =
            Mathf.Lerp(
                maxSteerAngle,
                highSpeedSteerAngle,
                speedPercent
            );

        float targetSteer =
            steerInput * steerLimit;

        currentSteerAngle = Mathf.Lerp(
            currentSteerAngle,
            targetSteer,
            Time.fixedDeltaTime * steeringSmoothness
        );

        wheelFL.steerAngle = currentSteerAngle;
        wheelFR.steerAngle = currentSteerAngle;
    }

    void HandleBrakes()
    {
        bool braking =
            moveInput < -0.1f &&
            Vector3.Dot(rb.linearVelocity, transform.forward) > 5f;

        float brakeTorque = braking ? brakePower : 0f;

        wheelFL.brakeTorque = brakeTorque;
        wheelFR.brakeTorque = brakeTorque;
        wheelRL.brakeTorque = brakeTorque;
        wheelRR.brakeTorque = brakeTorque;

        if (handbrake)
        {
            wheelRL.brakeTorque = handbrakePower;
            wheelRR.brakeTorque = handbrakePower;
        }
    }

    void HandleDrift()
    {
        bool drifting =
            handbrake &&
            SpeedKmh > 25f;

        WheelFrictionCurve rl = wheelRL.sidewaysFriction;
        WheelFrictionCurve rr = wheelRR.sidewaysFriction;

        float targetGrip =
            drifting ? driftRearGrip : normalRearGrip;

        rl.stiffness = Mathf.Lerp(
            rl.stiffness,
            targetGrip,
            Time.fixedDeltaTime * 8f
        );

        rr.stiffness = Mathf.Lerp(
            rr.stiffness,
            targetGrip,
            Time.fixedDeltaTime * 8f
        );

        wheelRL.sidewaysFriction = rl;
        wheelRR.sidewaysFriction = rr;

        if (drifting)
        {
            rb.AddTorque(
                Vector3.up * steerInput * driftAssist,
                ForceMode.Acceleration
            );
        }
    }

    void ApplyDownforce()
    {
        rb.AddForce(
            -transform.up * downforce * rb.linearVelocity.magnitude
        );
    }

    void ApplyAntiRoll()
    {
        ApplyAntiRollToAxle(wheelFL, wheelFR);
        ApplyAntiRollToAxle(wheelRL, wheelRR);
    }

    void ApplyAntiRollToAxle(WheelCollider left, WheelCollider right)
    {
        WheelHit hit;

        float travelL = 1f;
        float travelR = 1f;

        bool groundedL = left.GetGroundHit(out hit);

        if (groundedL)
        {
            travelL =
                (-left.transform.InverseTransformPoint(hit.point).y
                - left.radius)
                / left.suspensionDistance;
        }

        bool groundedR = right.GetGroundHit(out hit);

        if (groundedR)
        {
            travelR =
                (-right.transform.InverseTransformPoint(hit.point).y
                - right.radius)
                / right.suspensionDistance;
        }

        float antiRollForce =
            (travelL - travelR) * antiRoll;

        if (groundedL)
            rb.AddForceAtPosition(
                left.transform.up * -antiRollForce,
                left.transform.position
            );

        if (groundedR)
            rb.AddForceAtPosition(
                right.transform.up * antiRollForce,
                right.transform.position
            );
    }

    void UpdateTransmission()
    {
        float speedPerGear = topSpeed / maxGear;

        CurrentGear =
            Mathf.Clamp(
                Mathf.FloorToInt(SpeedKmh / speedPerGear) + 1,
                1,
                maxGear
            );

        float gearMin =
            speedPerGear * (CurrentGear - 1);

        float gearMax =
            speedPerGear * CurrentGear;

        float gearProgress =
            Mathf.InverseLerp(
                gearMin,
                gearMax,
                SpeedKmh
            );

        EngineRPM =
            Mathf.Lerp(
                idleRPM,
                maxRPM,
                gearProgress
            );
    }

    void SetupWheelFriction()
    {
        WheelFrictionCurve rearLeft = wheelRL.sidewaysFriction;
        WheelFrictionCurve rearRight = wheelRR.sidewaysFriction;

        rearLeft.stiffness = normalRearGrip;
        rearRight.stiffness = normalRearGrip;

        wheelRL.sidewaysFriction = rearLeft;
        wheelRR.sidewaysFriction = rearRight;
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
        col.GetWorldPose(out Vector3 pos, out Quaternion rot);

        mesh.position = pos;
        mesh.rotation = rot;
    }

    void UpdateVisualRoll()
    {
        if (carVisual == null)
            return;

        Vector3 localVel =
            transform.InverseTransformDirection(rb.linearVelocity);

        float roll = -localVel.x * 0.8f;

        roll = Mathf.Clamp(roll, -10f, 10f);

        Quaternion targetRot =
            visualStartRot * Quaternion.Euler(0f, 0f, roll);

        carVisual.localRotation =
            Quaternion.Slerp(
                carVisual.localRotation,
                targetRot,
                Time.deltaTime * 6f
            );
    }
}