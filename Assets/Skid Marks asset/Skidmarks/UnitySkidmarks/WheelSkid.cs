using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Example skid script. Put this on a WheelCollider.
// Copyright 2017 Nition, BSD licence (see LICENCE file). http://nition.co
[RequireComponent(typeof(WheelCollider))]
public class WheelSkid : MonoBehaviour
{
    // INSPECTOR SETTINGS
    public AudioSource SkidSound;
    public float SkidSoundMultiplyer;

    [SerializeField]
    Rigidbody rb;

    [SerializeField]
    Skidmarks skidmarksController;

    // END INSPECTOR SETTINGS

    WheelCollider wheelCollider;
    WheelHit wheelHitInfo;

    const float SKID_FX_SPEED = 0.5f;
    const float MAX_SKID_INTENSITY = 20.0f;
    const float WHEEL_SLIP_MULTIPLIER = 10.0f;

    int lastSkid = -1;
    float lastFixedUpdateTime;

    protected void Awake()
    {
        wheelCollider = GetComponent<WheelCollider>();
        lastFixedUpdateTime = Time.time;
    }

    protected void Start()
    {
        // Needed for spawned cars
        if (rb == null)
        {
            rb = GetComponentInParent<Rigidbody>();
        }

        // Needed for spawned cars
        if (skidmarksController == null)
        {
            skidmarksController = FindAnyObjectByType<Skidmarks>();
        }

        if (rb == null)
        {
            Debug.LogError("WheelSkid: Rigidbody is missing on " + gameObject.name);
        }

        if (skidmarksController == null)
        {
            Debug.LogError("WheelSkid: SkidmarksController is missing in scene.");
        }
    }

    protected void FixedUpdate()
    {
        lastFixedUpdateTime = Time.time;
    }

    protected void LateUpdate()
    {
        if (rb == null || skidmarksController == null)
        {
            return;
        }

        if (wheelCollider.GetGroundHit(out wheelHitInfo))
        {
            // Check sideways speed

            // Gives velocity with +z being the car's forward axis
            Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
            float skidTotal = Mathf.Abs(localVelocity.x);

            // Check wheel spin as well

            float wheelAngularVelocity = wheelCollider.radius * ((2 * Mathf.PI * wheelCollider.rpm) / 60);
            float carForwardVel = Vector3.Dot(rb.linearVelocity, transform.forward);
            float wheelSpin = Mathf.Abs(carForwardVel - wheelAngularVelocity) * WHEEL_SLIP_MULTIPLIER;

            // NOTE: This extra line should not be needed and you can take it out if you have decent wheel physics
            // The built-in Unity demo car is actually skidding its wheels the ENTIRE time you're accelerating,
            // so this fades out the wheelspin-based skid as speed increases to make it look almost OK
            wheelSpin = Mathf.Max(0, wheelSpin * (10 - Mathf.Abs(carForwardVel)));

            skidTotal += wheelSpin;

            // Skid if we should
            if (skidTotal >= SKID_FX_SPEED)
            {
                float intensity = Mathf.Clamp01(skidTotal / MAX_SKID_INTENSITY);

                // Account for further movement since the last FixedUpdate
                Vector3 skidPoint = wheelHitInfo.point + (rb.linearVelocity * (Time.time - lastFixedUpdateTime));

                lastSkid = skidmarksController.AddSkidMark(
                    skidPoint,
                    wheelHitInfo.normal,
                    intensity,
                    lastSkid
                );

                if (SkidSound != null && SkidSoundMultiplyer != 0)
                {
                    SkidSound.volume = intensity / SkidSoundMultiplyer;
                }
            }
            else
            {
                lastSkid = -1;
            }
        }
        else
        {
            lastSkid = -1;
        }
    }

    // Used by CarSpawner after spawning the car
    public void SetSkidmarksController(Skidmarks controller)
    {
        skidmarksController = controller;
    }

    // Used by CarSpawner after spawning the car
    public void SetRigidbody(Rigidbody carRb)
    {
        rb = carRb;
    }
}