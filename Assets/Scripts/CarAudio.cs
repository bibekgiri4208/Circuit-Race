using UnityEngine;

public class CarAudio : MonoBehaviour
{
    public SimcadeCarController car;
    public Rigidbody rb;

    [Header("Audio Sources")]
    public AudioSource engineSource;
    public AudioSource gearShiftSource;
    public AudioSource skidSource;

    [Header("Engine Pitch")]
    public float idlePitch = 0.75f;
    public float maxPitch = 2.25f;
    public float pitchSmooth = 6f;

    [Header("Engine Volume")]
    public float idleVolume = 0.35f;
    public float maxVolume = 0.95f;

    [Header("Gear System")]
    public float[] gearSpeeds = { 30f, 60f, 90f, 125f, 160f, 190f };
    public float rpmDropAmount = 0.45f;
    public float rpmDropRecoverySpeed = 4f;
    public float shiftCooldown = 0.35f;

    [Header("Turbo Flutter")]
    public AudioSource turboFlutterSource;
    public float turboMinSpeedKmh = 30f;
    public float gasReleaseThreshold = 0.65f;
    public float turboCooldown = 0.45f;

    float previousThrottle;
    float lastTurboTime;

    int currentGear = 0;
    float rpmDrop = 0f;
    float lastShiftTime;

    void Start()
    {
        if (car == null)
            car = GetComponent<SimcadeCarController>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        // ENGINE
        if (engineSource != null)
        {
            engineSource.loop = true;
            engineSource.playOnAwake = true;

            if (!engineSource.isPlaying)
                engineSource.Play();
        }

        // SKID
        if (skidSource != null)
        {
            skidSource.loop = true;
            skidSource.playOnAwake = false;
            skidSource.volume = 0f;
            skidSource.Stop();
        }

        // TURBO
        if (turboFlutterSource != null)
        {
            turboFlutterSource.loop = false;
            turboFlutterSource.playOnAwake = false;
        }
    }

    void Update()
    {
        if (car == null || rb == null) return;

        float speedKmh = rb.linearVelocity.magnitude * 3.6f;

        HandleGearShift(speedKmh);
        HandleEngine(speedKmh);
        HandleTurboFlutter(speedKmh);
        HandleSkid();
    }

    void HandleEngine(float speedKmh)
    {
        if (engineSource == null) return;

        float gearMinSpeed = currentGear == 0 ? 0f : gearSpeeds[currentGear - 1];
        float gearMaxSpeed = currentGear >= gearSpeeds.Length
            ? car.topSpeedKmh
            : gearSpeeds[currentGear];

        float gearProgress = Mathf.InverseLerp(
            gearMinSpeed,
            gearMaxSpeed,
            speedKmh
        );

        float fakeRPM = Mathf.Lerp(0.25f, 1f, gearProgress);

        rpmDrop = Mathf.Lerp(
            rpmDrop,
            0f,
            Time.deltaTime * rpmDropRecoverySpeed
        );

        fakeRPM -= rpmDrop;
        fakeRPM = Mathf.Clamp01(fakeRPM);

        float load = Mathf.Clamp01(Mathf.Max(car.EngineLoad, car.ThrottleInput));

        float targetPitch = Mathf.Lerp(idlePitch, maxPitch, fakeRPM);

        targetPitch += load * 0.05f;

        engineSource.pitch = Mathf.Lerp(
            engineSource.pitch,
            targetPitch,
            Time.deltaTime * pitchSmooth
        );

        engineSource.volume = Mathf.Lerp(
            idleVolume,
            maxVolume,
            Mathf.Max(load, fakeRPM * 0.6f)
        );
    }

    void HandleTurboFlutter(float speedKmh)
    {
        if (turboFlutterSource == null) return;

        float currentThrottle = car.ThrottleInput;
        float throttleDrop = previousThrottle - currentThrottle;

        bool releasedGas =
     throttleDrop > gasReleaseThreshold && currentThrottle < 0.35f;

        bool canFlutter =
            speedKmh >= turboMinSpeedKmh &&
            Time.time > lastTurboTime + turboCooldown;

        if (releasedGas && canFlutter)
        {
            turboFlutterSource.pitch = Random.Range(0.92f, 1.08f);
            turboFlutterSource.Play();

            lastTurboTime = Time.time;
        }

        previousThrottle = currentThrottle;
    }

    void HandleGearShift(float speedKmh)
    {
        int newGear = 0;

        for (int i = 0; i < gearSpeeds.Length; i++)
        {
            if (speedKmh > gearSpeeds[i])
                newGear = i + 1;
        }

        if (newGear != currentGear && Time.time > lastShiftTime + shiftCooldown)
        {
            currentGear = newGear;
            lastShiftTime = Time.time;

            rpmDrop = rpmDropAmount;

            if (gearShiftSource != null)
                gearShiftSource.Play();
        }
    }

    void HandleSkid()
    {
        if (skidSource == null || car == null || rb == null) return;

        float speedKmh = rb.linearVelocity.magnitude * 3.6f;

        // HARD STOP skid audio at low speed
        if (speedKmh < 22f)
        {
            skidSource.volume = 0f;
            skidSource.Stop();
            return;
        }

        Vector3 localVelocity =
            transform.InverseTransformDirection(rb.linearVelocity);

        float sidewaysSpeed = Mathf.Abs(localVelocity.x);
        float forwardSpeed = Mathf.Abs(localVelocity.z);

        float driftAngle =
            Mathf.Atan2(sidewaysSpeed, forwardSpeed) * Mathf.Rad2Deg;

        bool realDrift =
            speedKmh > 30f &&
            driftAngle > 14f &&
            Mathf.Abs(car.ThrottleInput) > 0.15f;

        bool handbrakeDrift =
            car.IsHandbraking &&
            speedKmh > 28f &&
            driftAngle > 10f;

        bool shouldSkid = realDrift || handbrakeDrift;

        if (shouldSkid)
        {
            skidSource.volume = Mathf.Lerp(
                skidSource.volume,
                0.75f,
                Time.deltaTime * 12f
            );

            if (!skidSource.isPlaying)
                skidSource.Play();
        }
        else
        {
            skidSource.volume = Mathf.Lerp(
                skidSource.volume,
                0f,
                Time.deltaTime * 18f
            );

            if (skidSource.volume < 0.05f)
            {
                skidSource.volume = 0f;
                skidSource.Stop();
            }
        }
    }
}