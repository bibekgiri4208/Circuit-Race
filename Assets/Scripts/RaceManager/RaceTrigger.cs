using System.Collections.Generic;
using UnityEngine;

// One crossing per car, even when its body and wheels all enter the trigger.
public abstract class RaceTrigger : MonoBehaviour
{
    private readonly Dictionary<PlayerLapTracker, HashSet<Collider>> contacts =
        new Dictionary<PlayerLapTracker, HashSet<Collider>>();

    void OnTriggerEnter(Collider other)
    {
        PlayerLapTracker racer = GetRacer(other);
        if (racer == null) return;
        if (!contacts.TryGetValue(racer, out HashSet<Collider> colliders))
        {
            colliders = new HashSet<Collider>();
            contacts.Add(racer, colliders);
        }
        if (colliders.Add(other) && colliders.Count == 1)
            OnRacerEntered(racer);
    }

    void OnTriggerExit(Collider other)
    {
        PlayerLapTracker racer = GetRacer(other);
        if (racer == null || !contacts.TryGetValue(racer, out HashSet<Collider> colliders)) return;
        colliders.Remove(other);
        if (colliders.Count == 0) contacts.Remove(racer);
    }

    void OnDisable()
    {
        contacts.Clear();
    }

    static PlayerLapTracker GetRacer(Collider collider)
    {
        PlayerLapTracker racer = collider.attachedRigidbody != null
            ? collider.attachedRigidbody.GetComponent<PlayerLapTracker>() : null;
        return racer != null ? racer : collider.GetComponentInParent<PlayerLapTracker>();
    }

    protected abstract void OnRacerEntered(PlayerLapTracker racer);
}
