using UnityEngine;

public class CarAudio : MonoBehaviour
{
    [Header("References")]
    public CarController car;

    [Header("Audio Sources")]
    public AudioSource Engine;
    public AudioSource SkidSound;
    public AudioSource GearChangeSound;
    public AudioSource TurboFlutterSound;

    [Header("Engine Pitch")]
    public float minPitch = 0.9f;
    public float maxPitch = 2.2f;

    [Header("Volume")]
    public float idleVolume = 0.35f;
    public float maxVolume = 1f;

    [Header("Smoothing")]
    public float pitchSmoothSpeed = 8f;
    public float volumeSmoothSpeed = 5f;

    int lastGear;

    bool wasAccelerating;

    void Update()
    {
        EngineSound();

        GearShift();

        SkidControl();

        TurboFlutter();
    }

    void EngineSound()
    {
        float speed = car.SpeedKmh;

        // ================= RPM FEEL =================

        float gearFactor =
            Mathf.InverseLerp(
                0,
                car.topSpeed / car.maxGear,
                speed % (car.topSpeed / car.maxGear)
            );

        // Arcade exaggerated rev climb
        float targetPitch =
            Mathf.Lerp(minPitch, maxPitch, gearFactor);

        // Small throttle boost
        if (car.moveInput > 0.1f)
            targetPitch += 0.08f;

        // Smooth pitch
        Engine.pitch = Mathf.Lerp(
            Engine.pitch,
            targetPitch,
            Time.deltaTime * pitchSmoothSpeed
        );

        // ================= VOLUME =================

        float throttleAmount =
            Mathf.Abs(car.moveInput);

        float targetVolume =
            Mathf.Lerp(idleVolume, maxVolume, throttleAmount);

        // More aggressive at high speed
        targetVolume += speed / car.topSpeed * 0.15f;

        Engine.volume = Mathf.Lerp(
            Engine.volume,
            targetVolume,
            Time.deltaTime * volumeSmoothSpeed
        );
    }

    void GearShift()
    {
        if (car.CurrentGear != lastGear)
        {
            if (GearChangeSound != null)
            {
                GearChangeSound.pitch =
                    Random.Range(0.95f, 1.05f);

                GearChangeSound.Play();
            }

            // Fake RPM drop effect
            Engine.pitch *= 0.82f;

            lastGear = car.CurrentGear;
        }
    }

    void SkidControl()
    {
        bool drifting =
            car.handbrake &&
            car.SpeedKmh > 20f;

        if (drifting)
        {
            if (!SkidSound.isPlaying)
                SkidSound.Play();
        }
        else
        {
            if (SkidSound.isPlaying)
                SkidSound.Stop();
        }
    }

    void TurboFlutter()
    {
        bool accelerating =
            car.moveInput > 0.2f;

        bool throttleReleased =
            wasAccelerating &&
            car.moveInput < 0.05f;

        if (throttleReleased && car.SpeedKmh > 40f)
        {
            if (TurboFlutterSound != null)
            {
                TurboFlutterSound.pitch =
                    Random.Range(0.9f, 1.1f);

                TurboFlutterSound.Play();
            }
        }

        wasAccelerating = accelerating;
    }
}