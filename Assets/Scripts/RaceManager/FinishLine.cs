public class FinishLine : RaceTrigger
{
    protected override void OnRacerEntered(PlayerLapTracker racer)
    {
        racer.CrossFinishLine();
    }
}
