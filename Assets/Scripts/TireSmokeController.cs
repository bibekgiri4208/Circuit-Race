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
    public float smokeStartSpeed = 25f;
    public float smokeDriftAngle = 10f;
    public float smokeEmissionRate = 45f;

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

        bool shouldSmoke =
            car.IsDrifting &&
            car.SpeedKmh > smokeStartSpeed &&
            car.DriftAngle > smokeDriftAngle;

        SetSmoke(frontLeftSmoke, shouldSmoke);
        SetSmoke(frontRightSmoke, shouldSmoke);
        SetSmoke(rearLeftSmoke, shouldSmoke);
        SetSmoke(rearRightSmoke, shouldSmoke);
    }

    void SetupSmoke(ParticleSystem smoke)
    {
        if (smoke == null) return;

        var emission = smoke.emission;
        emission.rateOverTime = 0f;

        smoke.Stop();
    }

    void SetSmoke(ParticleSystem smoke, bool enabled)
    {
        if (smoke == null) return;

        var emission = smoke.emission;
        emission.rateOverTime = enabled ? smokeEmissionRate : 0f;

        if (enabled && !smoke.isPlaying)
            smoke.Play();

        if (!enabled && smoke.isPlaying)
            smoke.Stop();
    }
}