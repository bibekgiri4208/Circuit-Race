using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class SimcadeCarController : MonoBehaviour
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
    public Vector3 fallbackCOM = new Vector3(0f, -0.65f, 0.05f); // Kept low and central for racing stability

    [Header("Engine & Performance")]
    public float motorTorque = 2600f;
    public float reverseTorque = 1200f;
    public float topSpeedKmh = 220f; // Adjusted for pure racing performance

    [Header("Steering (High Speed Safety)")]
    public float maxSteerAngle = 35f;      // Tightened for racing lines
    public float steerResponse = 12f;
    [Range(0.1f, 0.5f)]
    public float highSpeedSteerLimit = 0.25f; // Limits maximum turn angle at top speed to prevent rolling over

    [Header("Brakes")]
    public float brakeTorque = 6000f;
    public float idleBrakeTorque = 300f;
    public float handbrakeTorque = 8000f;

    [Header("Brake Lights")]
    public Light[] brakeLights;
    public float brakeLightIntensity = 2.5f;

    [Header("Aerodynamics & Stability")]
    public float downforce = 120f;        // Increased significantly to glue the car to the track at speed
    public float angularDragNormal = 1.8f; // Dampens erratic physics movements

    [Header("Visual Body Roll")]
    public Transform carVisual;
    public float bodyRollAmount = 4f;      // Reduced for a stiffer, track-focused feel
    public float bodyPitchAmount = 2f;
    public float bodyRollSpeed = 8f;

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
    public float EngineLoad { get; private set; }

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        rb.mass = 1300f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.angularDamping = angularDragNormal;

        // Ensure Center of Mass is very low to prevent tipping over during aggressive cornering
        rb.centerOfMass = centerOfMass != null
            ? transform.InverseTransformPoint(centerOfMass.position)
            : fallbackCOM;

        visualStartRot = carVisual != null ? carVisual.localRotation : Quaternion.identity;
    }

    void Update()
    {
        ReadInput();
        UpdateWheelMeshes();
        UpdateBodyVisual();
        HandleBrakeLights();
    }

    void FixedUpdate()
    {
        if (RaceManager.Instance != null && !RaceManager.Instance.raceStarted)
        {
            // Reset wheel torques if race hasn't started yet
            SetMotorTorque(0f);
            SetBrakeTorque(idleBrakeTorque);
            return;
        }

        SpeedKmh = rb.linearVelocity.magnitude * 3.6f;

        HandleSteering();
        HandleMotorAndBrakes();
        ApplyDownforce();
    }

    void ReadInput()
    {
        throttle = 0f;
        steerInput = 0f;
        brakeInput = 0f;
        handbrake = false;

        // Keyboard Inputs
        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) throttle = 1f;
            if (Keyboard.current.sKey.isPressed) brakeInput = 1f;
            if (Keyboard.current.aKey.isPressed) steerInput = -1f;
            if (Keyboard.current.dKey.isPressed) steerInput = 1f;
            handbrake = Keyboard.current.spaceKey.isPressed || Keyboard.current.eKey.isPressed;
        }

        // Controller Inputs
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
            steerInput = Mathf.Sign(stickX) * Mathf.Pow(Mathf.Abs(stickX), 0.75f);
            handbrake = Gamepad.current.buttonSouth.isPressed || Gamepad.current.rightShoulder.isPressed;
        }

        throttle = Mathf.Clamp01(throttle);
        brakeInput = Mathf.Clamp01(brakeInput);
        steerInput = Mathf.Clamp(steerInput, -1f, 1f);
    }

    void HandleMotorAndBrakes()
    {
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float speedFactor = Mathf.Clamp01(SpeedKmh / topSpeedKmh);

        // Gradually reduce engine torque near top speed to blend smoothly into the limit
        float torqueMultiplier = Mathf.Lerp(1f, 0.1f, speedFactor);

        SetBrakeTorque(0f);
        SetMotorTorque(0f);

        // Acceleration
        if (throttle > 0.05f && SpeedKmh < topSpeedKmh)
        {
            SetMotorTorque(throttle * (motorTorque * torqueMultiplier));
            EngineLoad = throttle;
        }
        else
        {
            EngineLoad = 0.15f;
        }

        // Foot-braking / Reverse
        if (brakeInput > 0.05f)
        {
            if (forwardSpeed > 1f)
            {
                SetBrakeTorque(brakeInput * brakeTorque);
            }
            else
            {
                SetMotorTorque(-brakeInput * reverseTorque);
            }
            EngineLoad = Mathf.Max(EngineLoad, brakeInput);
        }

        // Natural Idle Brake/Rolling resistance
        if (throttle < 0.05f && brakeInput < 0.05f)
        {
            SetBrakeTorque(idleBrakeTorque);
        }

        // Emergency Handbrake
        if (handbrake)
        {
            wheelRL.brakeTorque = handbrakeTorque;
            wheelRR.brakeTorque = handbrakeTorque;
        }
    }

    void HandleSteering()
    {
        float speedFactor = Mathf.Clamp01(SpeedKmh / topSpeedKmh);

        // Dynamically reduce maximum turn angle based on current speed
        // This ensures razor-sharp turns at 30km/h and tight, safe adjustments at 200km/h
        float dynamicSteerLimit = Mathf.Lerp(1f, highSpeedSteerLimit, speedFactor);
        float targetSteer = steerInput * maxSteerAngle * dynamicSteerLimit;

        currentSteerAngle = Mathf.Lerp(
            currentSteerAngle,
            targetSteer,
            Time.fixedDeltaTime * steerResponse
        );

        wheelFL.steerAngle = currentSteerAngle;
        wheelFR.steerAngle = currentSteerAngle;
    }

    void ApplyDownforce()
    {
        // Downforce increases exponentially relative to velocity magnitude, keeping the car glued to the track
        rb.AddForce(
            -transform.up * (downforce * rb.linearVelocity.magnitude),
            ForceMode.Force
        );
    }

    void SetMotorTorque(float torque)
    {
        // For standard track driving, rear-wheel drive or all-wheel drive distribution works best. 
        // Currently configured as RWD.
        wheelRL.motorTorque = torque;
        wheelRR.motorTorque = torque;
    }

    void SetBrakeTorque(float torque)
    {
        // standard braking applies to all 4 wheels for stopping power
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
        float roll = -localVel.x * bodyRollAmount / 12f;
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