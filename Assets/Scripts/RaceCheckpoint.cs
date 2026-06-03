using UnityEngine;

public class RaceCheckpoint : MonoBehaviour
{
    public int checkpointIndex;
    public bool isFinishLine = false;

    private void OnTriggerEnter(Collider other)
    {
        PlayerLapTracker tracker = other.GetComponentInParent<PlayerLapTracker>();

        if (tracker == null)
            return;

        if (isFinishLine)
        {
            tracker.CrossFinishLine();
        }
        else
        {
            tracker.PassCheckpoint(checkpointIndex);
        }
    }
}