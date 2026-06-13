using UnityEngine;

public class TireSmokeController : MonoBehaviour
{
    [Header("References")]
    public SimcadeCarController car;

    [Header("Wheel Smoke")]
    public ParticleSystem frontLeftSmoke;
    public ParticleSystem frontRightSmoke;
    public ParticleSystem rearLeftSmoke;
    public ParticleSystem rearRightSmoke;

    [Header("Drift Smoke")]
    public float smokeStartSpeed = 12f;
    public float smokeDriftAngle = 8f;
    public float driftSmokeRate = 55f;

    [Header("Handbrake Smoke")]
    public float handbrakeMinSpeed = 8f;
    public float handbrakeSmokeRate = 75f;

    [Header("Launch / Wheelspin Smoke")]
    public float launchSmokeMaxSpeed = 28f;
    public float launchThrottleThreshold = 0.75f;
    public float launchSmokeRate = 65f;

    [Header("Front Tire Smoke")]
    public bool enableFrontSmoke = true;
    public float frontSmokeMultiplier = 0.45f;

    void Start()
    {
        if (car == null)
            car = GetComponentInParent<SimcadeCarController>();

        SetupSmoke(frontLeftSmoke);
        SetupSmoke(frontRightSmoke);
        SetupSmoke(rearLeftSmoke);
        SetupSmoke(rearRightSmoke);
    }

    void Update()
    {
        if (car == null) return;

        bool driftSmoke =
            car.IsDrifting &&
            car.SpeedKmh > smokeStartSpeed &&
            car.DriftAngle > smokeDriftAngle;

        bool handbrakeSmoke =
            car.IsHandbraking &&
            car.SpeedKmh > handbrakeMinSpeed;

        bool launchSmoke =
            car.ThrottleInput > launchThrottleThreshold &&
            car.SpeedKmh < launchSmokeMaxSpeed;

        float rearRate = 0f;
        float frontRate = 0f;

        if (driftSmoke)
            rearRate = Mathf.Max(rearRate, driftSmokeRate);

        if (handbrakeSmoke)
            rearRate = Mathf.Max(rearRate, handbrakeSmokeRate);

        if (launchSmoke)
            rearRate = Mathf.Max(rearRate, launchSmokeRate);

        if (enableFrontSmoke && driftSmoke)
            frontRate = rearRate * frontSmokeMultiplier;

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