using UnityEngine;

public static class ArcadeVehicleUtility
{
    // Wheel torque remains physical; fill the gap to an arcade acceleration target.
    // This keeps heavier cars responsive without applying extra power beyond top speed.
    public static void ApplyDrive(Rigidbody body, WheelCollider[] wheels, Vector3 forward, float input,
        float acceleration, float speedLimit, float contactFactor)
    {
        if (contactFactor < 0.5f || Mathf.Abs(input) < 0.01f) return;
        float speed = Vector3.Dot(body.linearVelocity, forward) * Mathf.Sign(input);
        float falloff = 1f - Mathf.InverseLerp(speedLimit * 0.7f, speedLimit, speed);
        float wheelAcceleration = 0f;
        foreach (WheelCollider wheel in wheels)
            if (wheel.isGrounded) wheelAcceleration += Mathf.Abs(wheel.motorTorque) / Mathf.Max(0.1f, wheel.radius) / body.mass;
        float extra = Mathf.Max(0f, acceleration * Mathf.Abs(input) * falloff - wheelAcceleration);
        body.AddForce(forward * Mathf.Sign(input) * extra * contactFactor, ForceMode.Acceleration);
    }

    public static int GroundContact(WheelCollider[] wheels, out Vector3 normal)
    {
        int count = 0;
        normal = Vector3.zero;
        foreach (WheelCollider wheel in wheels)
        {
            if (!wheel.GetGroundHit(out WheelHit hit)) continue;
            normal += hit.normal;
            count++;
        }
        normal = count > 0 ? normal.normalized : Vector3.up;
        return count;
    }

    public static void Recover(Rigidbody body, Vector3 position, Quaternion rotation)
    {
        body.linearVelocity = body.angularVelocity = Vector3.zero;
        body.position = position;
        body.rotation = rotation;
        body.WakeUp();
        foreach (WheelSkid skid in body.GetComponentsInChildren<WheelSkid>()) skid.ResetTrail();
    }

    public static bool IsRecoverySpaceClear(Vector3 position, Quaternion rotation, Transform car)
    {
        // Leave the road alone; only reject a recovery location occupied by another car.
        Collider[] nearby = Physics.OverlapBox(position + Vector3.up * 0.7f, new Vector3(1.15f, 0.6f, 2.4f),
            rotation, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        foreach (Collider collider in nearby)
        {
            Rigidbody other = collider.attachedRigidbody;
            if (other != null && other.transform != car &&
                (other.GetComponent<CarController>() != null || other.GetComponent<SimpleAICarController>() != null)) return false;
        }
        return true;
    }
}
