using UnityEngine;

public class FinishLine : MonoBehaviour
{
    private bool triggered = false;

    void OnTriggerEnter(Collider other)
    {
        if (triggered) return;

        PlayerLapTracker tracker = null;

        if (other.attachedRigidbody != null)
            tracker = other.attachedRigidbody.GetComponent<PlayerLapTracker>();

        if (tracker == null)
            tracker = other.GetComponentInParent<PlayerLapTracker>();

        if (tracker == null) return;

        triggered = true;

        if (RaceManager.Instance != null)
            RaceManager.Instance.StartFinishSequence();
    }
}
