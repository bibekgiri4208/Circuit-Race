using UnityEngine;

public class Level2Setup : MonoBehaviour
{
    [Header("Finish Line Settings")]
    public Transform finishLine;
    public float triggerWidth = 25f;
    public float triggerHeight = 12f;
    public float triggerDepth = 3f;

    void Start()
    {
        SpawnFinishLine();
    }

    void SpawnFinishLine()
    {
        if (finishLine == null)
        {
            Debug.LogWarning("Level2Setup: No finish line assigned.");
            return;
        }

        GameObject fl = new GameObject("FinishLine");
        fl.transform.position = finishLine.position;
        fl.transform.rotation = finishLine.rotation;

        BoxCollider col = fl.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(triggerWidth, triggerHeight, triggerDepth);

        RaceCheckpoint rc = fl.AddComponent<RaceCheckpoint>();
        rc.checkpointIndex = 0;
        rc.isFinishLine = true;

        PlayerLapTracker tracker = FindAnyObjectByType<PlayerLapTracker>();
        if (tracker != null)
        {
            tracker.totalCheckpoints = 0;
        }

        Debug.Log("Level2: Finish line spawned");
    }

    void OnDrawGizmos()
    {
        if (finishLine == null) return;
        Gizmos.color = new Color(0f, 1f, 0f, 0.7f);
        Gizmos.matrix = finishLine.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
}
