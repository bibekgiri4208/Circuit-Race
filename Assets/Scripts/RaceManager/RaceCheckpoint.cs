using UnityEngine;

public class RaceCheckpoint : MonoBehaviour
{
    public int checkpointIndex;
    public bool isFinishLine = false;

    private void OnTriggerEnter(Collider other)
    {
        PlayerLapTracker tracker = null;

        if (other.attachedRigidbody != null)
            tracker = other.attachedRigidbody.GetComponent<PlayerLapTracker>();

        if (tracker == null)
            tracker = other.GetComponentInParent<PlayerLapTracker>();

        if (tracker == null)
            return;

        if (isFinishLine)
        {
            Debug.Log("FinishLine triggered by: " + other.gameObject.name);
            tracker.CrossFinishLine();
        }
        else
        {
            Debug.Log("Checkpoint " + checkpointIndex + " triggered by: " + other.gameObject.name);
            tracker.PassCheckpoint(checkpointIndex);
        }
    }
}