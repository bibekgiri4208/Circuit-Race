using UnityEngine;
using TMPro;

public class PlayerLapTracker : MonoBehaviour
{
    [Header("Lap Settings")]
    public int totalLaps = 3;
    public int totalCheckpoints = 3;

    [Header("Current Progress")]
    public int currentLap = 1;
    public int nextCheckpointIndex = 0;

    [Header("UI")]
    public TextMeshProUGUI lapText;
    public TextMeshProUGUI checkpointText;

    private bool raceCompleted = false;

    private void Start()
    {
        UpdateUI();
    }

    public void PassCheckpoint(int checkpointIndex)
    {
        if (raceCompleted)
            return;

        if (RaceManager.Instance != null && !RaceManager.Instance.raceStarted)
            return;

        if (checkpointIndex == nextCheckpointIndex)
        {
            nextCheckpointIndex++;

            Debug.Log("Checkpoint passed: " + checkpointIndex);

            UpdateUI();
        }
        else
        {
            Debug.Log("Wrong checkpoint. Expected: " + nextCheckpointIndex + " but got: " + checkpointIndex);
        }
    }

    public void CrossFinishLine()
    {
        if (raceCompleted)
            return;

        if (RaceManager.Instance != null && !RaceManager.Instance.raceStarted)
            return;

        if (nextCheckpointIndex < totalCheckpoints)
        {
            Debug.Log("Finish line crossed too early. Missing checkpoints.");
            return;
        }

        // Player completed all checkpoints of this lap
        nextCheckpointIndex = 0;

        // If this was the final lap, finish the race immediately
        if (currentLap >= totalLaps)
        {
            FinishRace();
            return;
        }

        // Otherwise move to next lap
        currentLap++;

        Debug.Log("Lap completed. Current lap: " + currentLap);

        UpdateUI();
    }

    private void FinishRace()
    {
        raceCompleted = true;

        Debug.Log("Race Finished!");

        if (RaceManager.Instance != null)
        {
            RaceManager.Instance.raceFinished = true;
        }

        if (lapText != null)
            lapText.text = "FINISHED";

        if (checkpointText != null)
            checkpointText.text = "Race Complete";
    }

    private void UpdateUI()
    {
        if (lapText != null)
            lapText.text = "Lap: " + currentLap + " / " + totalLaps;

        if (checkpointText != null)
            checkpointText.text = "Checkpoint: " + nextCheckpointIndex + " / " + totalCheckpoints;
    }
}