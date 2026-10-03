using UnityEngine;

public class RaceCheckpoint : RaceTrigger
{
    [Min(0)] public int checkpointIndex;
    public bool isFinishLine;

    protected override void OnRacerEntered(PlayerLapTracker racer)
    {
        if (isFinishLine) racer.CrossFinishLine();
        else racer.PassCheckpoint(checkpointIndex);
    }
}
