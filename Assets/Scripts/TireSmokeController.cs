using UnityEngine;

public class TireSmokeController : MonoBehaviour
{
    [Header("References")]
    public SimcadeCarController car;
    public Rigidbody rb;

    [Header("Smoke Particle Systems")]
    public ParticleSystem frontLeftSmoke;
    public ParticleSystem frontRightSmoke;
    public ParticleSystem rearLeftSmoke;
    public ParticleSystem rearRightSmoke;

    [Header("Smoke Conditions")]
    public float smokeStartSpeed = 10f;
    public float smokeDriftAngle = 8f;

    [Header("Smoke Amount")]
    public float normalSmokeRate = 35f;
    public float driftSmokeRate = 60f;
    public float handbrakeSmokeRate = 80f;
    public float burnoutSmokeRate = 90f;
    public float brakingSmokeRate = 55f;

    [Header("Acceleration / Braking Detection")]
    public float hardAccelerationThreshold = 0.75f;
    public float hardBrakeThreshold = 0.65f;
    public float lowSpeedBurnoutLimit = 35f;

    void Start()
    {
        if (car == null)
            car = GetComponentInParent<SimcadeCarController>();

        if (rb == null && car != null)
            rb = car.GetComponent<Rigidbody>();

        SetupSmoke(frontLeftSmoke);
        SetupSmoke(frontRightSmoke);
        SetupSmoke(rearLeftSmoke);
        SetupSmoke(rearRightSmoke);
    }

    void Update()
    {
        if (car == null || rb == null) return;

        bool drifting =
            car.IsDrifting &&
            car.SpeedKmh > smokeStartSpeed &&
            car.DriftAngle > smokeDriftAngle;

        bool handbraking =
            car.IsHandbraking &&
            car.SpeedKmh > 5f;

        bool hardAcceleration =
            car.ThrottleInput > hardAccelerationThreshold &&
            car.SpeedKmh < lowSpeedBurnoutLimit;

        bool hardBraking =
            IsBrakingHard();

        float rearSmokeRate = 0f;
        float frontSmokeRate = 0f;

        if (drifting)
            rearSmokeRate = driftSmokeRate;

        if (handbraking)
            rearSmokeRate = Mathf.Max(rearSmokeRate, handbrakeSmokeRate);

        if (hardAcceleration)
            rearSmokeRate = Mathf.Max(rearSmokeRate, burnoutSmokeRate);

        if (hardBraking)
        {
            frontSmokeRate = brakingSmokeRate;
            rearSmokeRate = Mathf.Max(rearSmokeRate, brakingSmokeRate * 0.5f);
        }

        SetSmoke(frontLeftSmoke, frontSmokeRate);
        SetSmoke(frontRightSmoke, frontSmokeRate);
        SetSmoke(rearLeftSmoke, rearSmokeRate);
        SetSmoke(rearRightSmoke, rearSmokeRate);
    }

    bool IsBrakingHard()
    {
        // If your controller exposes brakeInput publicly later, use that.
        // For now, detect braking using deceleration.
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

        bool movingForward = localVelocity.z > 5f;

        // Strong deceleration estimate
        float forwardSpeed = localVelocity.z;

        return movingForward && car.ThrottleInput < 0.05f && forwardSpeed > 8f && rb.linearVelocity.magnitude < forwardSpeed + 1f;
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