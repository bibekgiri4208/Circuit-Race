using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class CarController : MonoBehaviour
{
    [Header("Wheel Colliders")]
    public WheelCollider frontLeftCollider;
    public WheelCollider frontRightCollider;
    public WheelCollider rearLeftCollider;
    public WheelCollider rearRightCollider;

    [Header("Wheel Meshes")]
    public Transform frontLeftMesh;
    public Transform frontRightMesh;
    public Transform rearLeftMesh;
    public Transform rearRightMesh;

    [Header("Car Settings")]
    public float motorForce = 1500f;
    public float brakeForce = 3000f;
    public float maxSteerAngle = 30f;
    public float handbrakeForce = 3500f;

    [Header("All Wheel Drive")]
    [Range(0f, 1f)] public float frontTorqueRatio = 0.4f;
    [Range(0f, 1f)] public float rearTorqueRatio = 0.6f;
    public bool frontWheelDrive = true;
    public bool rearWheelDrive = true;

    [Header("Speed Limit")]
    public float maxForwardSpeed = 35f;
    public float maxReverseSpeed = 12f;

    [Header("NOS Boost Settings")]
    public float boostForce = 9000f;
    public float boostMaxSpeed = 55f;

    [Header("Steering")]
    public float steerSmoothSpeed = 8f;
    public float minSteerAngleAtHighSpeed = 4f;
    public float steeringSpeedForMinAngle = 30f;
    public float steerExponent = 2f;

    [Header("Stability")]
    public float downForce = 120f;
    public Vector3 centerOfMassOffset = new Vector3(0f, -0.5f, 0f);
    public float lateralCorrectionForce = 4000f;
    public float maxTiltAngle = 6f;
    public float tiltCorrectionForce = 150f;

    [Header("Wheel Friction")]
    public float rearSidewaysStiffness = 1.8f;
    public float frontSidewaysStiffness = 1.0f;

    [Header("Body Visual")]
    public Transform carVisual;
    public float bodyRollAmount = 2f;
    public float bodyRollSpeed = 4f;

    [Header("Brake Lights")]
    public Light[] brakeLights;
    public float brakeLightIntensity = 2.5f;

    private float horizontalInput;
    private float verticalInput;
    private bool isHandbraking;
    private float currentSteerAngle;
    private Quaternion visualStartRot;

    public bool IsBoosting { get; private set; }
    public Rigidbody CarRigidbody { get; private set; }
    public float SpeedKmh { get; private set; }
    public float ThrottleInput => verticalInput;
    public float EngineLoad => Mathf.Clamp01(Mathf.Abs(verticalInput));
    public bool IsHandbraking => isHandbraking;

    private void Start()
    {
        CarRigidbody = GetComponent<Rigidbody>();
        CarRigidbody.mass = 1300f;
        CarRigidbody.angularDamping = 0.5f;
        CarRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        CarRigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (centerOfMassOffset != Vector3.zero)
        {
            CarRigidbody.centerOfMass += centerOfMassOffset;
        }

        visualStartRot = carVisual != null ? carVisual.localRotation : Quaternion.identity;

        ApplyWheelFriction();
    }

    private void ApplyWheelFriction()
    {
        SetSidewaysFriction(frontLeftCollider, frontSidewaysStiffness);
        SetSidewaysFriction(frontRightCollider, frontSidewaysStiffness);
        SetSidewaysFriction(rearLeftCollider, rearSidewaysStiffness);
        SetSidewaysFriction(rearRightCollider, rearSidewaysStiffness);
    }

    private void SetSidewaysFriction(WheelCollider wheel, float stiffness)
    {
        if (wheel == null) return;
        WheelFrictionCurve curve = wheel.sidewaysFriction;
        curve.stiffness = stiffness;
        wheel.sidewaysFriction = curve;
    }

    private void Update()
    {
        GetInput();
        UpdateWheelMeshes();
        UpdateBodyVisual();
        HandleBrakeLights();
    }

    private void FixedUpdate()
    {
        SpeedKmh = CarRigidbody.linearVelocity.magnitude * 3.6f;

        HandleMotor();
        HandleSteering();
        HandleBraking();
        HandleBoost();
        ApplyDownforce();
        ApplyLateralCorrection();
        ClampTilt();
    }

    private void GetInput()
    {
        horizontalInput = 0f;
        verticalInput = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed)
                horizontalInput -= 1f;
            if (Keyboard.current.dKey.isPressed)
                horizontalInput += 1f;
            if (Keyboard.current.wKey.isPressed)
                verticalInput += 1f;
            if (Keyboard.current.sKey.isPressed)
                verticalInput -= 1f;

            isHandbraking = Keyboard.current.spaceKey.isPressed;
            IsBoosting = Keyboard.current.leftShiftKey.isPressed;
        }

        if (Gamepad.current != null)
        {
            Vector2 leftStick = Gamepad.current.leftStick.ReadValue();
            float r2 = Gamepad.current.rightTrigger.ReadValue();
            float l2 = Gamepad.current.leftTrigger.ReadValue();

            horizontalInput += leftStick.x;
            verticalInput += r2 - l2;

            if (Gamepad.current.buttonSouth.isPressed)
            {
                IsBoosting = true;
            }
        }

        horizontalInput = Mathf.Clamp(horizontalInput, -1f, 1f);
        verticalInput = Mathf.Clamp(verticalInput, -1f, 1f);
    }

    private void HandleMotor()
    {
        float forwardSpeed = Vector3.Dot(CarRigidbody.linearVelocity, transform.forward);

        bool overForwardSpeed = forwardSpeed >= maxForwardSpeed && verticalInput > 0f && !IsBoosting;
        bool overReverseSpeed = forwardSpeed <= -maxReverseSpeed && verticalInput < 0f;

        if (overForwardSpeed || overReverseSpeed)
        {
            SetAllMotorTorque(0f);
            return;
        }

        float steerFraction = Mathf.Abs(currentSteerAngle) / maxSteerAngle;
        float torqueMultiplier = 1f - steerFraction * 0.35f;

        float torque = verticalInput * motorForce * torqueMultiplier;
        float frontTorque = frontWheelDrive ? torque * frontTorqueRatio : 0f;
        float rearTorque = rearWheelDrive ? torque * rearTorqueRatio : 0f;

        frontLeftCollider.motorTorque = frontTorque;
        frontRightCollider.motorTorque = frontTorque;
        rearLeftCollider.motorTorque = rearTorque;
        rearRightCollider.motorTorque = rearTorque;
    }

    private void SetAllMotorTorque(float torque)
    {
        frontLeftCollider.motorTorque = frontWheelDrive ? torque : 0f;
        frontRightCollider.motorTorque = frontWheelDrive ? torque : 0f;
        rearLeftCollider.motorTorque = rearWheelDrive ? torque : 0f;
        rearRightCollider.motorTorque = rearWheelDrive ? torque : 0f;
    }

    private void HandleBoost()
    {
        if (!IsBoosting)
            return;

        float forwardSpeed = Vector3.Dot(CarRigidbody.linearVelocity, transform.forward);

        if (forwardSpeed >= boostMaxSpeed)
            return;

        CarRigidbody.AddForce(transform.forward * boostForce, ForceMode.Force);
    }

    private void HandleSteering()
    {
        float speed = CarRigidbody.linearVelocity.magnitude;
        float speedPercent = Mathf.Clamp01(speed / steeringSpeedForMinAngle);

        float speedReduction = Mathf.Pow(speedPercent, steerExponent);
        float adjustedMaxSteerAngle = Mathf.Lerp(
            maxSteerAngle,
            minSteerAngleAtHighSpeed,
            speedReduction
        );

        float targetSteerAngle = horizontalInput * adjustedMaxSteerAngle;

        currentSteerAngle = Mathf.Lerp(
            currentSteerAngle,
            targetSteerAngle,
            steerSmoothSpeed * Time.fixedDeltaTime
        );

        frontLeftCollider.steerAngle = currentSteerAngle;
        frontRightCollider.steerAngle = currentSteerAngle;
    }

    private void HandleBraking()
    {
        float forwardSpeed = Vector3.Dot(CarRigidbody.linearVelocity, transform.forward);

        bool pressingReverse = verticalInput < -0.1f;
        bool movingForward = forwardSpeed > 1f;

        float currentBrakeForce = 0f;

        if (pressingReverse && movingForward)
        {
            currentBrakeForce = brakeForce;
            SetAllMotorTorque(0f);
        }

        if (isHandbraking)
        {
            frontLeftCollider.brakeTorque = brakeForce * 0.2f;
            frontRightCollider.brakeTorque = brakeForce * 0.2f;
            rearLeftCollider.brakeTorque = handbrakeForce;
            rearRightCollider.brakeTorque = handbrakeForce;
        }
        else
        {
            frontLeftCollider.brakeTorque = currentBrakeForce;
            frontRightCollider.brakeTorque = currentBrakeForce;
            rearLeftCollider.brakeTorque = currentBrakeForce;
            rearRightCollider.brakeTorque = currentBrakeForce;
        }
    }

    private void ApplyDownforce()
    {
        float speed = CarRigidbody.linearVelocity.magnitude;
        CarRigidbody.AddForce(-transform.up * downForce * speed);
    }

    private void ApplyLateralCorrection()
    {
        Vector3 localVel = transform.InverseTransformDirection(CarRigidbody.linearVelocity);
        float lateralSpeed = localVel.x;
        float forwardSpeed = Mathf.Abs(localVel.z);

        if (forwardSpeed < 1f) return;

        float speedFactor = Mathf.Clamp01(SpeedKmh / maxForwardSpeed);
        float correction = -lateralSpeed * lateralCorrectionForce * speedFactor;
        CarRigidbody.AddForce(transform.right * correction * Time.fixedDeltaTime, ForceMode.Acceleration);
    }

    private void ClampTilt()
    {
        Vector3 currentEuler = transform.eulerAngles;
        float pitch = NormalizeAngle(currentEuler.x);
        float roll = NormalizeAngle(currentEuler.z);

        if (Mathf.Abs(pitch) > maxTiltAngle || Mathf.Abs(roll) > maxTiltAngle)
        {
            float correctionPitch = pitch > maxTiltAngle ? pitch - maxTiltAngle : (pitch < -maxTiltAngle ? pitch + maxTiltAngle : 0f);
            float correctionRoll = roll > maxTiltAngle ? roll - maxTiltAngle : (roll < -maxTiltAngle ? roll + maxTiltAngle : 0f);

            Vector3 correctionTorque = transform.right * -correctionPitch * tiltCorrectionForce
                                     + transform.forward * -correctionRoll * tiltCorrectionForce;
            CarRigidbody.AddTorque(correctionTorque * Time.fixedDeltaTime, ForceMode.Force);
        }
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    private void UpdateWheelMeshes()
    {
        UpdateSingleWheel(frontLeftCollider, frontLeftMesh);
        UpdateSingleWheel(frontRightCollider, frontRightMesh);
        UpdateSingleWheel(rearLeftCollider, rearLeftMesh);
        UpdateSingleWheel(rearRightCollider, rearRightMesh);
    }

    private void UpdateSingleWheel(WheelCollider wheelCollider, Transform wheelMesh)
    {
        if (wheelCollider == null || wheelMesh == null) return;

        Vector3 position;
        Quaternion rotation;

        wheelCollider.GetWorldPose(out position, out rotation);

        wheelMesh.position = position;
        wheelMesh.rotation = rotation;
    }

    private void UpdateBodyVisual()
    {
        if (carVisual == null) return;

        if (bodyRollAmount <= 0f)
        {
            carVisual.localRotation = Quaternion.Slerp(carVisual.localRotation, visualStartRot, Time.deltaTime * bodyRollSpeed);
            return;
        }

        Vector3 localVel = transform.InverseTransformDirection(CarRigidbody.linearVelocity);
        float lateralG = localVel.x / 9.81f;
        float roll = Mathf.Clamp(-lateralG * bodyRollAmount, -12f, 12f);

        Quaternion target = visualStartRot * Quaternion.Euler(0f, 0f, roll);
        carVisual.localRotation = Quaternion.Slerp(
            carVisual.localRotation,
            target,
            Time.deltaTime * bodyRollSpeed
        );
    }

    private void HandleBrakeLights()
    {
        if (brakeLights == null) return;

        bool braking = verticalInput < -0.1f || isHandbraking;
        float target = braking ? brakeLightIntensity : 0f;

        foreach (Light light in brakeLights)
        {
            if (light == null) continue;
            light.intensity = Mathf.Lerp(light.intensity, target, Time.deltaTime * 12f);
        }
    }
}
