using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class PlayerLapTracker : MonoBehaviour
{
    [Header("Lap Settings")]
    [Min(1)] public int totalLaps = 3;
    [Min(0)] public int totalCheckpoints = 3;

    [Header("Current Progress")]
    public int currentLap = 1;
    public int nextCheckpointIndex;

    [Header("UI")]
    public TextMeshProUGUI lapText;
    public TextMeshProUGUI checkpointText;

    private readonly List<float> lapTimes = new List<float>();
    private float lastLapFinishTime;
    private bool raceCompleted;

    public bool IsPlayer => GetComponent<CarController>() != null;
    public bool RaceCompleted => raceCompleted;
    public int FinishPosition { get; internal set; }
    public float FinishTime { get; private set; }
    public float BestLapTime { get; private set; }
    public float CurrentLapTime => RaceManager.Instance != null ? RaceManager.Instance.RaceTime - lastLapFinishTime : 0f;
    public IReadOnlyList<float> LapTimes => lapTimes;

    void Start()
    {
        RefreshUI();
    }

    public void ResetProgress(int laps, int checkpoints)
    {
        totalLaps = Mathf.Max(1, laps);
        totalCheckpoints = Mathf.Max(0, checkpoints);
        currentLap = 1;
        nextCheckpointIndex = FinishPosition = 0;
        FinishTime = BestLapTime = lastLapFinishTime = 0f;
        raceCompleted = false;
        lapTimes.Clear();
        RefreshUI();
    }

    public void PassCheckpoint(int checkpointIndex)
    {
        if (!CanRecordProgress() || checkpointIndex != nextCheckpointIndex || checkpointIndex >= totalCheckpoints)
            return;
        nextCheckpointIndex++;
        RefreshUI();
    }

    public void CrossFinishLine()
    {
        if (!CanRecordProgress()) return;
        if (nextCheckpointIndex < totalCheckpoints)
        {
            if (IsPlayer) RaceManager.Instance.ShowRaceMessage("PASS CHECKPOINT " + (nextCheckpointIndex + 1) + " / " + totalCheckpoints);
            return;
        }
        float elapsed = RaceManager.Instance.RaceTime;
        float lapTime = elapsed - lastLapFinishTime;
        // Ignore the starting grid crossing on tracks with no checkpoint route.
        if (totalCheckpoints == 0 && lapTime < 5f)
        {
            if (IsPlayer) RaceManager.Instance.ShowRaceMessage("RACE STARTED - COMPLETE THE COURSE");
            return;
        }

        lapTimes.Add(lapTime);
        BestLapTime = BestLapTime <= 0f ? lapTime : Mathf.Min(BestLapTime, lapTime);
        lastLapFinishTime = elapsed;
        nextCheckpointIndex = 0;

        if (currentLap >= totalLaps)
        {
            raceCompleted = true;
            FinishTime = elapsed;
            RaceManager.Instance.RegisterFinish(this);
        }
        else
        {
            currentLap++;
            if (IsPlayer) RaceManager.Instance.ShowRaceMessage("LAP " + currentLap + " / " + totalLaps +
                (currentLap == totalLaps ? " - FINAL LAP" : ""));
        }
        RefreshUI();
    }

    bool CanRecordProgress()
    {
        return !raceCompleted && RaceManager.Instance != null &&
            RaceManager.Instance.raceStarted && !RaceManager.Instance.raceFinished;
    }

    public void RefreshUI()
    {
        if (!IsPlayer) return;
        if (lapText != null)
            lapText.text = raceCompleted ? "FINISHED" : "LAP " + currentLap + " / " + totalLaps;
        if (checkpointText != null)
            checkpointText.text = raceCompleted ? "Race complete" : "CHECKPOINT " + nextCheckpointIndex + " / " + totalCheckpoints;
    }
}
