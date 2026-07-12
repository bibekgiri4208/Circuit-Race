using UnityEngine;
using Fusion;

[RequireComponent(typeof(Rigidbody))]
public class CarController : NetworkBehaviour // 1. Switched to NetworkBehaviour
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

    [Header("Engine & Performance")]
    public float motorTorque = 2600f;
    public float reverseTorque = 1200f;
    public float topSpeedKmh = 220f;

    [Header("Steering (High Speed Safety)")]
    public float maxSteerAngle = 35f;
    public float steerResponse = 12f;
    [Range(0.1f, 0.5f)]
    public float highSpeedSteerLimit = 0.25f;

    [Header("Brakes")]
    public float brakeTorque = 6000f;
    public float idleBrakeTorque = 300f;
    public float handbrakeTorque = 8000f;

    [Header("Brake Lights")]
    public Light[] brakeLights;
    public float brakeLightIntensity = 2.5f;

    [Header("Aerodynamics & Stability")]
    public float downforce = 120f;
    public float angularDragNormal = 1.8f;

    [Header("Visual Body Roll")]
    public Transform carVisual;
    public float bodyRollAmount = 4f;
    public float bodyPitchAmount = 2f;
    public float bodyRollSpeed = 8f;

    Rigidbody rb;

    // These values are now fetched directly from the network stream
    private float throttle;
    private float steerInput;
    private float brakeInput;
    private bool handbrake;

    float currentSteerAngle;
    Quaternion visualStartRot;

    public float SpeedKmh { get; private set; }
    public float ThrottleInput => throttle;
    public bool IsHandbraking => handbrake;
    public float EngineLoad { get; private set; }

    // Make sure our local simulation changes don't override the network authority
    public override void Spawned()
    {
        rb = GetComponent<Rigidbody>();

        rb.mass = 1300f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.angularDamping = angularDragNormal;

        rb.centerOfMass = centerOfMass != null
            ? transform.InverseTransformPoint(centerOfMass.position)
            : fallbackCOM;

        visualStartRot = carVisual != null ? carVisual.localRotation : Quaternion.identity;
    }

    // Update runs locally on every machine to handle visual effects and wheel animations smoothly
    void Update()
    {
        UpdateWheelMeshes();
        UpdateBodyVisual();
        HandleBrakeLights();
    }

    // 2. Switched from FixedUpdate to FixedUpdateNetwork for unified physics synchronization
    public override void FixedUpdateNetwork()
    {
        // Fetch network inputs from the Connection Handler struct
        if (GetInput(out NetworkInputData data))
        {
            throttle = Mathf.Clamp01(data.acceleration);
            brakeInput = Mathf.Clamp01(data.brake);
            steerInput = Mathf.Clamp(data.steering, -1f, 1f);
            handbrake = data.handbrake;
        }

        // Optional logic from your singleplayer codebase checking race manager status
        if (RaceManager.Instance != null && !RaceManager.Instance.raceStarted)
        {
            SetMotorTorque(0f);
            SetBrakeTorque(idleBrakeTorque);
            return;
        }

        SpeedKmh = rb.linearVelocity.magnitude * 3.6f;

        HandleSteering();
        HandleMotorAndBrakes();
        ApplyDownforce();
    }

    void HandleMotorAndBrakes()
    {
        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float speedFactor = Mathf.Clamp01(SpeedKmh / topSpeedKmh);
        float torqueMultiplier = Mathf.Lerp(1f, 0.1f, speedFactor);

        SetBrakeTorque(0f);
        SetMotorTorque(0f);

        if (throttle > 0.05f && SpeedKmh < topSpeedKmh)
        {
            SetMotorTorque(throttle * (motorTorque * torqueMultiplier));
            EngineLoad = throttle;
        }
        else
        {
            EngineLoad = 0.15f;
        }

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

        if (throttle < 0.05f && brakeInput < 0.05f)
        {
            SetBrakeTorque(idleBrakeTorque);
        }

        if (handbrake)
        {
            wheelRL.brakeTorque = handbrakeTorque;
            wheelRR.brakeTorque = handbrakeTorque;
        }
    }

    void HandleSteering()
    {
        float speedFactor = Mathf.Clamp01(SpeedKmh / topSpeedKmh);
        float dynamicSteerLimit = Mathf.Lerp(1f, highSpeedSteerLimit, speedFactor);
        float targetSteer = steerInput * maxSteerAngle * dynamicSteerLimit;

        currentSteerAngle = Mathf.Lerp(
            currentSteerAngle,
            targetSteer,
            Runner.DeltaTime * steerResponse // Switched to Runner.DeltaTime for network safety
        );

        wheelFL.steerAngle = currentSteerAngle;
        wheelFR.steerAngle = currentSteerAngle;
    }

    void ApplyDownforce()
    {
        rb.AddForce(
            -transform.up * (downforce * rb.linearVelocity.magnitude),
            ForceMode.Force
        );
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