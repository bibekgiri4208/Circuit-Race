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

    [Header("Performance")]
    public float motorPower = 1500f;
    public float brakePower = 3000f;
    public float topSpeedKmh = 240f;

    [Header("Steering")]
    public float maxSteerAngle = 35f;

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
    Quaternion visualStartRot;

    public float SpeedKmh { get; private set; }
    public float ThrottleInput => gasInput;
    public bool IsHandbraking => Input.GetKey(KeyCode.Space);
    public float EngineLoad { get; private set; }
    public float LateralG => lateralG;
    public bool IsDrifting { get; private set; }
    public float DriftAngle { get; private set; }

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
        CheckInput();

        if (RaceManager.Instance != null && (!RaceManager.Instance.raceStarted || RaceManager.Instance.raceFinished))
        {
            SetMotorTorque(0f);
            SetBrakeTorque(300f);
            return;
        }

        SpeedKmh = rb.linearVelocity.magnitude * 3.6f;

        ApplyMotor();
        ApplySteering();
        ApplyBrake();
        DetectDrift();
        CalculateLateralG();
    }

    void CheckInput()
    {
        gasInput = Input.GetAxis("Vertical");
        steeringInput = Input.GetAxis("Horizontal");

        slipAngle = Vector3.Angle(transform.forward, rb.linearVelocity);

        float movingDirection = Vector3.Dot(transform.forward, rb.linearVelocity);
        if (movingDirection < -0.5f && gasInput > 0)
        {
            brakeInput = Mathf.Abs(gasInput);
            gasInput = 0;
        }
        else if (movingDirection > 0.5f && gasInput < 0)
        {
            brakeInput = Mathf.Abs(gasInput);
            gasInput = 0;
        }
        else
        {
            brakeInput = 0;
        }

        if (Input.GetKey(KeyCode.Space))
        {
            brakeInput = 1f;
            gasInput = 0f;
        }
    }

    void ApplyMotor()
    {
        wheelRL.motorTorque = motorPower * gasInput;
        wheelRR.motorTorque = motorPower * gasInput;
    }

    void ApplySteering()
    {
        float steeringAngle = steeringInput * maxSteerAngle;

        if (slipAngle < 120f)
        {
            Vector3 vel = rb.linearVelocity;
            vel.y = 0f;
            if (vel.sqrMagnitude > 1f)
            {
                steeringAngle += Vector3.SignedAngle(transform.forward, vel + transform.forward, Vector3.up);
            }
        }

        steeringAngle = Mathf.Clamp(steeringAngle, -90f, 90f);
        wheelFL.steerAngle = steeringAngle;
        wheelFR.steerAngle = steeringAngle;
    }

    void ApplyBrake()
    {
        wheelFL.brakeTorque = brakeInput * brakePower * 0.7f;
        wheelFR.brakeTorque = brakeInput * brakePower * 0.7f;
        wheelRL.brakeTorque = brakeInput * brakePower * 0.3f;
        wheelRR.brakeTorque = brakeInput * brakePower * 0.3f;
    }

    void DetectDrift()
    {
        IsDrifting = slipAngle > 20f && SpeedKmh > 20f;
        DriftAngle = IsDrifting ? Mathf.Lerp(DriftAngle, slipAngle, Time.fixedDeltaTime * 8f)
                                : Mathf.Lerp(DriftAngle, 0f, Time.fixedDeltaTime * 5f);
    }

    void CalculateLateralG()
    {
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        float targetLateralG = localVel.x / 9.81f;
        lateralG = Mathf.Lerp(lateralG, targetLateralG, Time.fixedDeltaTime * 10f);
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
        bool braking = brakeInput > 0.1f || Input.GetKey(KeyCode.Space);
        float targetIntensity = braking ? brakeLightIntensity : 0f;

        foreach (Light light in brakeLights)
        {
            if (light == null) continue;
            light.intensity = Mathf.Lerp(light.intensity, targetIntensity, Time.deltaTime * 12f);
        }
    }
}
