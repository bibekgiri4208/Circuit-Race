using UnityEngine;
using TMPro;

public class RaceUIBinder : MonoBehaviour
{
    public TextMeshProUGUI lapText;
    public TextMeshProUGUI checkpointText;

    private void Start()
    {
        Invoke(nameof(BindUIToPlayer), 0.2f);
    }

    private void BindUIToPlayer()
    {
        PlayerLapTracker tracker = Object.FindAnyObjectByType<PlayerLapTracker>();  
        if (tracker == null)
        {
            Debug.LogWarning("No PlayerLapTracker found in scene.");
            return;
        }

        tracker.lapText = lapText;
        tracker.checkpointText = checkpointText;

        Debug.Log("Race UI bound to player lap tracker.");
    }
}