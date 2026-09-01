using UnityEngine;

public class TireSmokeController : MonoBehaviour
{
    [Header("References")]
    public CarController car;

    [Header("Wheel Smoke")]
    public ParticleSystem frontLeftSmoke;
    public ParticleSystem frontRightSmoke;
    public ParticleSystem rearLeftSmoke;
    public ParticleSystem rearRightSmoke;

    [Header("Launch / Wheelspin Smoke")]
    public float launchThrottleThreshold = 0.8f;
    public float launchSmokeMaxSpeed = 45f;
    public float launchSmokeRate = 65f;

    [Header("Brake Lockup / Skid Smoke")]
    public float skidMinSpeed = 15f;
    public float brakeSkidThreshold = 0.6f;
    public float brakeSkidSmokeRate = 80f;

    [Header("Lateral Slide / Cornering Smoke")]
    public float lateralSlideThreshold = 2.5f;
    public float corneringSmokeRate = 45f;

    [Header("Drift Smoke")]
    public float driftSmokeRate = 100f;

    void Start()
    {
        if (car == null)
            car = GetComponentInParent<CarController>();

        SetupSmoke(frontLeftSmoke);
        SetupSmoke(frontRightSmoke);
        SetupSmoke(rearLeftSmoke);
        SetupSmoke(rearRightSmoke);
    }

    void Update()
    {
        if (car == null) return;

        // 1. Calculate physics vectors from the vehicle's Rigidbody to find lateral slide
        Rigidbody carRb = car.GetComponent<Rigidbody>();
        Vector3 localVelocity = car.transform.InverseTransformDirection(carRb.linearVelocity);
        float sidewaysSpeed = Mathf.Abs(localVelocity.x);

        // 2. Check for Racing Smoke Conditions

        // Wheelspin on launch (Rear wheels)
        bool rearWheelspin = car.ThrottleInput > launchThrottleThreshold && car.SpeedKmh < launchSmokeMaxSpeed;

        // Heavy braking lockup (All wheels)
        bool brakeLockup = car.SpeedKmh > skidMinSpeed && (car.IsHandbraking || car.GetComponent<CarController>().EngineLoad > brakeSkidThreshold && Input.GetKey(KeyCode.S));
        // Note: Checking if brake input is high via generalized conditions

        // Pushing too hard into a corner / Understeer / Oversteer slide (All wheels)
        bool lateralSlide = car.SpeedKmh > skidMinSpeed && sidewaysSpeed > lateralSlideThreshold;

        // Drift smoke
        bool drifting = car.IsDrifting;

        // 3. Assign rates dynamically based on racing events
        float frontRate = 0f;
        float rearRate = 0f;

        // Handle Rear Wheels (Grip loss from power, braking, or sliding)
        if (rearWheelspin) rearRate = Mathf.Max(rearRate, launchSmokeRate);
        if (brakeLockup) rearRate = Mathf.Max(rearRate, brakeSkidSmokeRate);
        if (lateralSlide) rearRate = Mathf.Max(rearRate, corneringSmokeRate);
        if (drifting) rearRate = Mathf.Max(rearRate, driftSmokeRate);

        // Handle Front Wheels (Grip loss from heavy braking or severe understeer cornering)
        if (brakeLockup) frontRate = Mathf.Max(frontRate, brakeSkidSmokeRate);
        if (lateralSlide) frontRate = Mathf.Max(frontRate, corneringSmokeRate);
        if (drifting) frontRate = Mathf.Max(frontRate, driftSmokeRate * 0.6f);

        // 4. Apply to Particle Systems
        SetSmoke(frontLeftSmoke, frontRate);
        SetSmoke(frontRightSmoke, frontRate);
        SetSmoke(rearLeftSmoke, rearRate);
        SetSmoke(rearRightSmoke, rearRate);
    }

    void SetupSmoke(ParticleSystem smoke)
    {
        if (smoke == null) return;
        var emission = smoke.emission;
        emission.rateOverTime = 0f;
        smoke.Stop();
    }

    void SetSmoke(ParticleSystem smoke, float rate)
    {
        if (smoke == null) return;

        var emission = smoke.emission;
        emission.rateOverTime = rate;

        if (rate > 0f && !smoke.isPlaying)
            smoke.Play();

        if (rate <= 0f && smoke.isPlaying)
            smoke.Stop();
    }
}