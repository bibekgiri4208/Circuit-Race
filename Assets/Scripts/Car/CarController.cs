using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    [FormerlySerializedAs("wheelFL")] public WheelCollider frontLeftCollider;
    [FormerlySerializedAs("wheelFR")] public WheelCollider frontRightCollider;
    [FormerlySerializedAs("wheelRL")] public WheelCollider rearLeftCollider;
    [FormerlySerializedAs("wheelRR")] public WheelCollider rearRightCollider;

    [Header("Wheel Meshes")]
    [FormerlySerializedAs("meshFL")] public Transform frontLeftMesh;
    [FormerlySerializedAs("meshFR")] public Transform frontRightMesh;
    [FormerlySerializedAs("meshRL")] public Transform rearLeftMesh;
    [FormerlySerializedAs("meshRR")] public Transform rearRightMesh;

    [Header("Power and Brakes")]
    [FormerlySerializedAs("motorTorque"), Min(0f)] public float motorForce = 3500f;
    [FormerlySerializedAs("brakeTorque"), Min(0f)] public float brakeForce = 4500f;
    [FormerlySerializedAs("handbrakeTorque"), Min(0f)] public float handbrakeForce = 5000f;
    [Min(0f)] public float idleBrakeTorque = 100f;
    [Min(0f)] public float throttleResponse = 8f;
    [Min(0f)] public float reverseEngageSpeed = 0.5f;
    [Min(0f)] public float brakingDeceleration = 18f;
    [Min(0f)] public float coastingDeceleration = 0.8f;

    [Header("Drivetrain (normalized axle split)")]
    [Range(0f, 1f)] public float frontTorqueRatio = 0.4f;
    [Range(0f, 1f)] public float rearTorqueRatio = 0.6f;
    public bool frontWheelDrive = true;
    public bool rearWheelDrive = true;

    [Header("Speed Limits (metres per second)")]
    [Min(1f)] public float maxForwardSpeed = 50f;
    [Min(1f)] public float maxReverseSpeed = 10f;
    [SerializeField, HideInInspector, FormerlySerializedAs("topSpeedKmh")] private float legacyTopSpeedKmh;

    [Header("NOS Boost")]
    [Min(0f)] public float boostForce = 9000f;
    [Min(1f)] public float boostMaxSpeed = 65f;

    [Header("Steering")]
    [Range(1f, 50f)] public float maxSteerAngle = 35f;
    [FormerlySerializedAs("steerResponse"), Min(0f)] public float steerSmoothSpeed = 10f;
    [Range(1f, 20f)] public float minSteerAngleAtHighSpeed = 8f;
    [Min(1f)] public float steeringSpeedForMinAngle = 45f;
    [Min(0.1f)] public float steerExponent = 1f;
    [Min(0f)] public float steeringAssist = 3f;
    [Range(10f, 120f)] public float maxYawRate = 65f;
    [Min(0f)] public float maxYawAcceleration = 4f;

    [Header("Grip and Drift")]
    [Min(0f)] public float lateralCorrectionForce = 5000f;
    [Min(0f)] public float maxGripAcceleration = 22f;
    [Range(0f, 1f)] public float driftGripMultiplier = 0.25f;
    [Range(0.1f, 1f)] public float driftRearFrictionMultiplier = 0.45f;
    [Min(0f)] public float gripRecoverySpeed = 4f;
    [Min(0f)] public float rearSidewaysStiffness = 1.65f;
    [Min(0f)] public float frontSidewaysStiffness = 1.7f;

    [Header("Grounded Stability")]
    [FormerlySerializedAs("downforce"), Min(0f)] public float downForce = 70f;
    [FormerlySerializedAs("fallbackCOM")] public Vector3 centerOfMassOffset = new Vector3(0f, -0.45f, 0.1f);
    [SerializeField, FormerlySerializedAs("centerOfMass")] private Transform centerOfMassTransform;
    [Min(0f)] public float antiRollForce = 6000f;
    [Range(1f, 60f)] public float maxTiltAngle = 15f;
    [Min(0f)] public float tiltCorrectionForce = 150f;

    [Header("Body Visual")]
    public Transform carVisual;
    public float bodyRollAmount = 5f;
    public float bodyPitchAmount = 2f;
    public float bodyRollSpeed = 8f;

    [Header("Brake Lights")]
    public Light[] brakeLights;
    public float brakeLightIntensity = 2.5f;

    private WheelCollider[] wheels;
    private RigidbodyConstraints originalConstraints;
    private bool gridLocked;
    private bool controlsEnabled = true;
    private float steeringInput;
    private float acceleratorInput;
    private float reverseInput;
    private float driveInput;
    private float currentSteerAngle;
    private float driftBlend;
    private float forwardSpeed;
    private float longitudinalAcceleration;
    private float previousForwardSpeed;
    private float wheelbase = 2.6f;
    private int groundedWheels;
    private Vector3 groundNormal = Vector3.up;
    private Quaternion visualStartRotation;
    private bool boostRequested;

    public bool IsBoosting { get; private set; }
    public bool IsHandbraking { get; private set; }
    public bool IsGrounded => groundedWheels > 0;
    public bool CanDrive => controlsEnabled && Time.timeScale > 0f &&
        (RaceManager.Instance == null || (RaceManager.Instance.raceStarted && !RaceManager.Instance.raceFinished));
    public Rigidbody CarRigidbody { get; private set; }
    public float SpeedKmh { get; private set; }
    public float ThrottleInput => driveInput;
    public float BrakeInput { get; private set; }
    public float EngineLoad => Mathf.Clamp01(Mathf.Abs(driveInput));

    void Awake()
    {
        CarRigidbody = GetComponent<Rigidbody>();
        originalConstraints = CarRigidbody.constraints;
        CarRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        CarRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        CarRigidbody.angularDamping = 1f;
        CarRigidbody.maxAngularVelocity = 5f;
        CarRigidbody.centerOfMass = centerOfMassTransform != null
            ? transform.InverseTransformPoint(centerOfMassTransform.position) : centerOfMassOffset;

        // Older car prefabs store their top speed in km/h, not m/s.
        if (legacyTopSpeedKmh > 0f)
        {
            maxForwardSpeed = legacyTopSpeedKmh / 3.6f;
            boostMaxSpeed = Mathf.Max(boostMaxSpeed, maxForwardSpeed * 1.25f);
        }

        wheels = new[] { frontLeftCollider, frontRightCollider, rearLeftCollider, rearRightCollider };
        foreach (WheelCollider wheel in wheels)
        {
            if (wheel == null)
            {
                Debug.LogError("CarController needs all four wheel colliders: " + name, this);
                enabled = false;
                return;
            }
            wheel.ConfigureVehicleSubsteps(10f, 5, 5);
        }

        Vector3 front = (frontLeftCollider.transform.position + frontRightCollider.transform.position) * 0.5f;
        Vector3 rear = (rearLeftCollider.transform.position + rearRightCollider.transform.position) * 0.5f;
        wheelbase = Mathf.Max(1f, Vector3.Distance(front, rear));
        visualStartRotation = carVisual != null ? carVisual.localRotation : Quaternion.identity;
        UpdateGrip();
        if (RaceManager.Instance != null && !RaceManager.Instance.raceStarted)
            SetGridLocked(true);
    }

    void Update()
    {
        ReadInput();
        UpdateWheel(frontLeftCollider, frontLeftMesh);
        UpdateWheel(frontRightCollider, frontRightMesh);
        UpdateWheel(rearLeftCollider, rearLeftMesh);
        UpdateWheel(rearRightCollider, rearRightMesh);
        UpdateBodyVisual();
        UpdateBrakeLights();
    }

    void FixedUpdate()
    {
        forwardSpeed = Vector3.Dot(CarRigidbody.linearVelocity, transform.forward);
        SpeedKmh = CarRigidbody.linearVelocity.magnitude * 3.6f;
        longitudinalAcceleration = (forwardSpeed - previousForwardSpeed) / Time.fixedDeltaTime;
        previousForwardSpeed = forwardSpeed;
        UpdateGroundContact();

        bool countdown = RaceManager.Instance != null && !RaceManager.Instance.raceStarted;
        SetGridLocked(countdown);
        if (countdown)
        {
            ClearInput();
            SetMotorTorque(0f);
            SetBrakes(brakeForce, brakeForce);
            // Allow suspension to settle vertically, but never creep off the grid.
            CarRigidbody.linearVelocity = new Vector3(0f, CarRigidbody.linearVelocity.y, 0f);
            CarRigidbody.angularVelocity = Vector3.zero;
            return;
        }

        if (!CanDrive)
        {
            ClearInput();
            SetMotorTorque(0f);
            BrakeInput = 1f;
            SetBrakes(brakeForce, brakeForce);
        }
        else
        {
            UpdateSteering();
            UpdatePowerAndBrakes();
        }

        driftBlend = Mathf.MoveTowards(driftBlend, IsHandbraking && Mathf.Abs(forwardSpeed) > 5f ? 1f : 0f,
            gripRecoverySpeed * Time.fixedDeltaTime);
        UpdateGrip();
        ApplyGroundedAssists();
    }

    void ReadInput()
    {
        acceleratorInput = reverseInput = steeringInput = 0f;
        IsHandbraking = boostRequested = IsBoosting = false;
        if (!CanDrive)
        {
            ClearInput();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            steeringInput = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) -
                (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
            acceleratorInput = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f;
            reverseInput = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f;
            IsHandbraking = keyboard.spaceKey.isPressed;
            boostRequested = keyboard.leftShiftKey.isPressed;
        }

        Gamepad gamepad = Gamepad.current;
        if (gamepad != null)
        {
            float stick = gamepad.leftStick.ReadValue().x;
            if (Mathf.Abs(stick) > Mathf.Abs(steeringInput)) steeringInput = stick;
            acceleratorInput = Mathf.Max(acceleratorInput, gamepad.rightTrigger.ReadValue());
            reverseInput = Mathf.Max(reverseInput, gamepad.leftTrigger.ReadValue());
            IsHandbraking |= gamepad.buttonWest.isPressed;
            boostRequested |= gamepad.buttonSouth.isPressed;
        }
    }

    void ClearInput()
    {
        steeringInput = acceleratorInput = reverseInput = driveInput = BrakeInput = 0f;
        IsHandbraking = boostRequested = IsBoosting = false;
    }

    public void SetControlsEnabled(bool value)
    {
        controlsEnabled = value;
        if (!value)
        {
            ClearInput();
            SetMotorTorque(0f);
            SetBrakes(brakeForce, brakeForce);
        }
    }

    void SetGridLocked(bool value)
    {
        if (gridLocked == value) return;
        gridLocked = value;
        CarRigidbody.constraints = value
            ? originalConstraints | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation
            : originalConstraints;
        if (value) SetMotorTorque(0f);
    }

    void UpdatePowerAndBrakes()
    {
        BrakeInput = 0f;
        float requestedDrive = acceleratorInput - reverseInput;
        if (reverseInput > 0.05f && forwardSpeed > reverseEngageSpeed)
        {
            BrakeInput = reverseInput;
            requestedDrive = 0f;
        }
        else if (acceleratorInput > 0.05f && forwardSpeed < -reverseEngageSpeed)
        {
            BrakeInput = acceleratorInput;
            requestedDrive = 0f;
        }

        driveInput = BrakeInput > 0f ? 0f : Mathf.MoveTowards(driveInput, requestedDrive, throttleResponse * Time.fixedDeltaTime);
        IsBoosting = boostRequested && acceleratorInput > 0.05f && reverseInput < 0.05f &&
            !IsHandbraking && IsGrounded && forwardSpeed >= -reverseEngageSpeed;

        float speedLimit = driveInput < 0f ? maxReverseSpeed : IsBoosting ? boostMaxSpeed : maxForwardSpeed;
        float speedInDriveDirection = forwardSpeed * Mathf.Sign(driveInput);
        float torqueFalloff = 1f - Mathf.InverseLerp(speedLimit * 0.85f, speedLimit, speedInDriveDirection);
        float torque = driveInput * motorForce * torqueFalloff;
        if (driveInput < 0f) torque *= 0.6f;
        SetMotorTorque(torque);

        float braking = BrakeInput * brakeForce;
        if (Mathf.Abs(forwardSpeed) < 0.5f && Mathf.Abs(requestedDrive) < 0.05f)
            braking = Mathf.Max(braking, idleBrakeTorque);
        SetBrakes(braking, IsHandbraking ? Mathf.Max(braking, handbrakeForce) : braking);

        if (IsBoosting && forwardSpeed < boostMaxSpeed)
        {
            float boostFalloff = 1f - Mathf.InverseLerp(boostMaxSpeed * 0.9f, boostMaxSpeed, forwardSpeed);
            CarRigidbody.AddForce(transform.forward * boostForce * boostFalloff, ForceMode.Force);
        }

        // Ease excess powered speed away without clamping collision impulses or vertical velocity.
        if (IsGrounded && Mathf.Abs(driveInput) > 0.05f && speedInDriveDirection > speedLimit)
            CarRigidbody.AddForce(-transform.forward * Mathf.Sign(driveInput) * (speedInDriveDirection - speedLimit) * 3f,
                ForceMode.Acceleration);
    }

    void SetMotorTorque(float torque)
    {
        if (wheels == null) return;
        float frontShare = frontWheelDrive ? frontTorqueRatio : 0f;
        float rearShare = rearWheelDrive ? rearTorqueRatio : 0f;
        float total = frontShare + rearShare;
        if (total <= 0f) total = 1f;
        if (frontLeftCollider != null) frontLeftCollider.motorTorque = torque * frontShare / total * 0.5f;
        if (frontRightCollider != null) frontRightCollider.motorTorque = torque * frontShare / total * 0.5f;
        if (rearLeftCollider != null) rearLeftCollider.motorTorque = IsHandbraking ? 0f : torque * rearShare / total * 0.5f;
        if (rearRightCollider != null) rearRightCollider.motorTorque = IsHandbraking ? 0f : torque * rearShare / total * 0.5f;
    }

    void SetBrakes(float front, float rear)
    {
        if (frontLeftCollider != null) frontLeftCollider.brakeTorque = front;
        if (frontRightCollider != null) frontRightCollider.brakeTorque = front;
        if (rearLeftCollider != null) rearLeftCollider.brakeTorque = rear;
        if (rearRightCollider != null) rearRightCollider.brakeTorque = rear;
    }

    void UpdateSteering()
    {
        float speedFactor = Mathf.Pow(Mathf.Clamp01(Mathf.Abs(forwardSpeed) / steeringSpeedForMinAngle), steerExponent);
        float angleLimit = Mathf.Lerp(maxSteerAngle, minSteerAngleAtHighSpeed, speedFactor);
        float blend = 1f - Mathf.Exp(-steerSmoothSpeed * Time.fixedDeltaTime);
        currentSteerAngle = Mathf.Lerp(currentSteerAngle, steeringInput * angleLimit, blend);
        frontLeftCollider.steerAngle = currentSteerAngle;
        frontRightCollider.steerAngle = currentSteerAngle;
    }

    void UpdateGroundContact()
    {
        groundedWheels = 0;
        Vector3 normal = Vector3.zero;
        foreach (WheelCollider wheel in wheels)
        {
            if (!wheel.GetGroundHit(out WheelHit hit)) continue;
            groundedWheels++;
            normal += hit.normal;
        }
        if (groundedWheels > 0) groundNormal = normal.normalized;
    }

    void UpdateGrip()
    {
        SetSidewaysFriction(frontLeftCollider, frontSidewaysStiffness);
        SetSidewaysFriction(frontRightCollider, frontSidewaysStiffness);
        float rearGrip = rearSidewaysStiffness * Mathf.Lerp(1f, driftRearFrictionMultiplier, driftBlend);
        SetSidewaysFriction(rearLeftCollider, rearGrip);
        SetSidewaysFriction(rearRightCollider, rearGrip);
    }

    static void SetSidewaysFriction(WheelCollider wheel, float stiffness)
    {
        if (wheel == null) return;
        WheelFrictionCurve friction = wheel.sidewaysFriction;
        friction.stiffness = stiffness;
        wheel.sidewaysFriction = friction;
    }

    void ApplyGroundedAssists()
    {
        // No invisible grip, boost or uprighting forces while jumping.
        if (groundedWheels < 2) return;
        float contactFactor = groundedWheels * 0.25f;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, groundNormal).normalized;
        float deceleration = BrakeInput > 0f ? brakingDeceleration * BrakeInput :
            Mathf.Abs(driveInput) < 0.05f ? coastingDeceleration : 0f;
        deceleration = Mathf.Min(deceleration, Mathf.Abs(forwardSpeed) / Time.fixedDeltaTime);
        CarRigidbody.AddForce(-forward * Mathf.Sign(forwardSpeed) * deceleration * contactFactor, ForceMode.Acceleration);
        float lateralSpeed = Vector3.Dot(CarRigidbody.linearVelocity, right);
        float grip = lateralCorrectionForce / 1000f * Mathf.Lerp(1f, driftGripMultiplier, driftBlend);
        float lateralAcceleration = Mathf.Clamp(-lateralSpeed * grip, -maxGripAcceleration, maxGripAcceleration);
        CarRigidbody.AddForce(right * lateralAcceleration * contactFactor, ForceMode.Acceleration);
        CarRigidbody.AddForce(-groundNormal * downForce * Mathf.Abs(forwardSpeed) * contactFactor, ForceMode.Force);

        if (CanDrive && Mathf.Abs(forwardSpeed) > 1f)
        {
            float desiredYaw = Mathf.Tan(currentSteerAngle * Mathf.Deg2Rad) * forwardSpeed / wheelbase;
            float yawLimit = Mathf.Lerp(maxYawRate, maxYawRate * 0.45f,
                Mathf.Clamp01(Mathf.Abs(forwardSpeed) / maxForwardSpeed)) * Mathf.Deg2Rad;
            desiredYaw = Mathf.Clamp(desiredYaw, -yawLimit, yawLimit);
            float yaw = Vector3.Dot(CarRigidbody.angularVelocity, groundNormal);
            float correction = Mathf.Clamp((desiredYaw - yaw) * steeringAssist, -maxYawAcceleration, maxYawAcceleration);
            CarRigidbody.AddTorque(groundNormal * correction * contactFactor, ForceMode.Acceleration);
        }

        ApplyAntiRoll(frontLeftCollider, frontRightCollider);
        ApplyAntiRoll(rearLeftCollider, rearRightCollider);
        if (Vector3.Angle(transform.up, groundNormal) > maxTiltAngle)
        {
            Vector3 alignment = Vector3.Cross(transform.up, groundNormal);
            Vector3 tiltVelocity = Vector3.ProjectOnPlane(CarRigidbody.angularVelocity, groundNormal);
            CarRigidbody.AddTorque((alignment - tiltVelocity * 0.4f) * (tiltCorrectionForce / 100f) * contactFactor,
                ForceMode.Acceleration);
        }
    }

    void ApplyAntiRoll(WheelCollider left, WheelCollider right)
    {
        bool leftGrounded = left.GetGroundHit(out WheelHit leftHit);
        bool rightGrounded = right.GetGroundHit(out WheelHit rightHit);
        float leftTravel = leftGrounded ? SuspensionTravel(left, leftHit) : 1f;
        float rightTravel = rightGrounded ? SuspensionTravel(right, rightHit) : 1f;
        float force = (leftTravel - rightTravel) * antiRollForce;
        if (leftGrounded) CarRigidbody.AddForceAtPosition(-groundNormal * force, left.transform.position);
        if (rightGrounded) CarRigidbody.AddForceAtPosition(groundNormal * force, right.transform.position);
    }

    static float SuspensionTravel(WheelCollider wheel, WheelHit hit)
    {
        float localHeight = wheel.transform.InverseTransformPoint(hit.point).y - wheel.center.y;
        return Mathf.Clamp01((-localHeight - wheel.radius) / Mathf.Max(0.01f, wheel.suspensionDistance));
    }

    static void UpdateWheel(WheelCollider wheel, Transform mesh)
    {
        if (wheel == null || mesh == null) return;
        wheel.GetWorldPose(out Vector3 position, out Quaternion rotation);
        mesh.SetPositionAndRotation(position, rotation);
    }

    void UpdateBodyVisual()
    {
        if (carVisual == null) return;
        float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 20f);
        float roll = Mathf.Clamp(-steeringInput * speedFactor * bodyRollAmount, -8f, 8f);
        float pitch = Mathf.Clamp(-longitudinalAcceleration * bodyPitchAmount * 0.1f, -4f, 4f);
        Quaternion target = visualStartRotation * Quaternion.Euler(pitch, 0f, roll);
        carVisual.localRotation = Quaternion.Slerp(carVisual.localRotation, target, 1f - Mathf.Exp(-bodyRollSpeed * Time.deltaTime));
    }

    void UpdateBrakeLights()
    {
        if (brakeLights == null) return;
        float target = BrakeInput > 0.05f || IsHandbraking || gridLocked ? brakeLightIntensity : 0f;
        foreach (Light light in brakeLights)
        {
            if (light != null) light.intensity = Mathf.Lerp(light.intensity, target, 1f - Mathf.Exp(-12f * Time.deltaTime));
        }
    }

    void OnDisable()
    {
        ClearInput();
        SetMotorTorque(0f);
        SetBrakes(brakeForce, brakeForce);
        if (CarRigidbody != null && gridLocked) SetGridLocked(false);
    }
}
