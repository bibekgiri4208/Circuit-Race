using UnityEngine;

public class FinishLine : MonoBehaviour
{
    private static int finishCount = 0;

    void Awake()
    {
        finishCount = 0;
    }

    void OnTriggerEnter(Collider other)
    {
        if (RaceManager.Instance == null) return;
        if (RaceManager.Instance.raceFinished) return;

        // Check if this is the player
        PlayerLapTracker tracker = null;
        if (other.attachedRigidbody != null)
            tracker = other.attachedRigidbody.GetComponent<PlayerLapTracker>();
        if (tracker == null)
            tracker = other.GetComponentInParent<PlayerLapTracker>();

        bool isPlayer = tracker != null;

        // Detect AI cars by tag or car layer
        bool isAI = other.GetComponentInParent<SimpleAICarController>() != null;

        if (!isPlayer && !isAI) return;

        finishCount++;

        if (isPlayer)
        {
            RaceManager.Instance.playerPosition = finishCount;
            RaceManager.Instance.StartFinishSequence();
        }
    }
}
