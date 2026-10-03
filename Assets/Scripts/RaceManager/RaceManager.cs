using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

[System.Serializable]
public class CountdownCameraAngle
{
    public string label;
    public Vector3 positionOffset;
    public Vector3 lookAtOffset;
    public float fov;
}

[System.Serializable]
public class CameraKeyframe
{
    public Vector3 positionOffset;
    public Vector3 lookAtOffset;
    public float fov;
    public float time;
}

[DefaultExecutionOrder(-200)]
public class RaceManager : MonoBehaviour
{
    public static RaceManager Instance;

    [Header("Race State")]
    public bool raceStarted;
    public bool raceFinished;
    public int playerPosition;
    [Min(1)] public int totalLaps = 3;

    [Header("Checkpoint Fallback")]
    [Tooltip("Create three ordered checkpoints along the AI route when the track has no checkpoints.")]
    public bool createCheckpointsFromAIPath = true;
    public Vector3 autoCheckpointSize = new Vector3(50f, 16f, 6f);

    [Header("Countdown Timing")]
    public float startDelay = 0.75f;
    public float numberStayTime = 0.5f;
    public float numberAnimTime = 0.2f;
    public float fadeOutTime = 0.15f;
    public float goStayTime = 0.6f;
    public float cameraTransitionTime = 0.6f;

    [Header("Countdown Text (scene objects)")]
    public GameObject countdownParent;
    public TextMeshPro textThree;
    public TextMeshPro textTwo;
    public TextMeshPro textOne;
    public TextMeshPro textGo;

    [Header("Countdown Colors")]
    public Color colorThree = new Color(1f, 0.15f, 0.15f, 1f);
    public Color colorTwo = new Color(1f, 0.35f, 0.05f, 1f);
    public Color colorOne = new Color(1f, 0.6f, 0f, 1f);
    public Color colorGo = new Color(0f, 1f, 0.3f, 1f);

    [Header("Camera Angles (relative to car)")]
    public CountdownCameraAngle angleThree = new CountdownCameraAngle
    {
        label = "Rear Left Quarter", positionOffset = new Vector3(-4f, 2f, -6f),
        lookAtOffset = new Vector3(0f, 1f, 0f), fov = 45f
    };
    public CountdownCameraAngle angleTwo = new CountdownCameraAngle
    {
        label = "Front Low", positionOffset = new Vector3(0f, 1f, 6f),
        lookAtOffset = new Vector3(0f, 0.8f, 0f), fov = 40f
    };
    public CountdownCameraAngle angleOne = new CountdownCameraAngle
    {
        label = "Side Close-Up", positionOffset = new Vector3(5f, 2.5f, 0f),
        lookAtOffset = new Vector3(0f, 1f, 0f), fov = 35f
    };
    public CountdownCameraAngle angleGo = new CountdownCameraAngle
    {
        label = "Rear Chase", positionOffset = new Vector3(0f, 2.5f, -6f),
        lookAtOffset = new Vector3(0f, 1f, 2f), fov = 50f
    };

    [Header("Finish Cinematic")]
    [Min(0f)] public float finishCinematicDuration = 3f;
    [Range(0.1f, 1f)] public float finishSlowMotionScale = 0.4f;
    [Min(0f)] public float finishSlowMotionEnd = 1.25f;
    public List<CameraKeyframe> finishKeyframes = new List<CameraKeyframe>
    {
        new CameraKeyframe { positionOffset = new Vector3(0f, 2.5f, -6f), lookAtOffset = new Vector3(0f, 1f, 2f), fov = 50f, time = 0f },
        new CameraKeyframe { positionOffset = new Vector3(6f, 1.5f, 1f), lookAtOffset = new Vector3(0f, 0.8f, 1f), fov = 40f, time = 1f },
        new CameraKeyframe { positionOffset = new Vector3(4f, 2f, 5f), lookAtOffset = new Vector3(0f, 0.8f, 0f), fov = 38f, time = 2f },
        new CameraKeyframe { positionOffset = new Vector3(0f, 3f, -7f), lookAtOffset = new Vector3(0f, 1f, 0f), fov = 45f, time = 3f }
    };

    private readonly List<PlayerLapTracker> racers = new List<PlayerLapTracker>();
    private readonly List<PlayerLapTracker> finishedRacers = new List<PlayerLapTracker>();
    private readonly Vector3[] originalScales = new Vector3[4];
    private CarSpawner carSpawner;
    private ChaseCamera chaseCam;
    private Camera mainCam;
    private Coroutine cameraTransition;
    private RaceResultsUI resultsUI;
    private string raceMessage;
    private float messageUntil;

    public float RaceTime { get; private set; }
    public PlayerLapTracker PlayerTracker { get; private set; }
    public int CheckpointCount { get; private set; }
    public int RacerCount => racers.Count;
    public float PreviousBestTime { get; private set; }
    public bool IsNewBestTime { get; private set; }
    public string RaceMessage => Time.unscaledTime < messageUntil ? raceMessage : null;

    void Awake()
    {
        Instance = this;
        raceStarted = raceFinished = false;
        playerPosition = 0;
        RaceTime = 0f;
    }

    void Start()
    {
        carSpawner = FindAnyObjectByType<CarSpawner>();
        mainCam = Camera.main;
        chaseCam = mainCam != null ? mainCam.GetComponent<ChaseCamera>() : null;
        if (chaseCam != null) chaseCam.holdPosition = true;

        SetupCheckpoints();
        foreach (SimpleAICarController ai in FindObjectsByType<SimpleAICarController>())
            RegisterRacer(ai.gameObject);
        RaceUIBinder.EnsureRaceHUD(transform);

        TextMeshPro[] texts = { textThree, textTwo, textOne, textGo };
        for (int i = 0; i < texts.Length; i++)
        {
            originalScales[i] = texts[i] != null ? texts[i].transform.localScale : Vector3.one;
            if (texts[i] != null) texts[i].gameObject.SetActive(false);
        }
        if (countdownParent != null) countdownParent.SetActive(false);
        StartCoroutine(StartCountdown());
    }

    void Update()
    {
        if (raceStarted && !raceFinished) RaceTime += Time.deltaTime;
    }

    void SetupCheckpoints()
    {
        RaceCheckpoint[] checkpoints = FindObjectsByType<RaceCheckpoint>();
        foreach (RaceCheckpoint checkpoint in checkpoints)
        {
            if (!checkpoint.isFinishLine) CheckpointCount = Mathf.Max(CheckpointCount, checkpoint.checkpointIndex + 1);
        }
        if (CheckpointCount > 0 || !createCheckpointsFromAIPath) return;

        foreach (SimpleAICarController ai in FindObjectsByType<SimpleAICarController>())
        {
            Transform[] path = ai.waypoints;
            if (path == null || path.Length < 4) continue;
            bool valid = true;
            for (int i = 0; i < path.Length; i++)
                if (path[i] == null) valid = false;
            if (!valid) continue;

            for (int i = 0; i < 3; i++)
            {
                int index = path.Length * (i + 1) / 4;
                Vector3 direction = path[(index + 1) % path.Length].position - path[index - 1].position;
                direction.y = 0f;
                GameObject checkpointObject = new GameObject("RouteCheckpoint_" + i);
                checkpointObject.transform.SetParent(transform, false);
                checkpointObject.transform.SetPositionAndRotation(path[index].position + Vector3.up * 2f,
                    Quaternion.LookRotation(direction.sqrMagnitude > 0.01f ? direction : Vector3.forward));
                BoxCollider trigger = checkpointObject.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = autoCheckpointSize;
                checkpointObject.AddComponent<RaceCheckpoint>().checkpointIndex = i;
            }
            CheckpointCount = 3;
            return;
        }
    }

    public void RegisterRacer(GameObject car)
    {
        if (car == null) return;
        PlayerLapTracker tracker = car.GetComponent<PlayerLapTracker>();
        if (tracker == null) tracker = car.AddComponent<PlayerLapTracker>();
        if (racers.Contains(tracker)) return;
        tracker.ResetProgress(totalLaps, CheckpointCount);
        racers.Add(tracker);
        if (tracker.IsPlayer) PlayerTracker = tracker;
    }

    public void ShowRaceMessage(string message)
    {
        raceMessage = message;
        messageUntil = Time.unscaledTime + 3f;
    }

    public void RegisterFinish(PlayerLapTracker racer)
    {
        if (!raceStarted || raceFinished || racer == null || !racer.RaceCompleted || finishedRacers.Contains(racer)) return;
        racer.FinishPosition = finishedRacers.Count + 1;
        finishedRacers.Add(racer);
        if (!racer.IsPlayer) return;
        playerPosition = racer.FinishPosition;
        SaveBestTimes();
        StartFinishSequence();
    }

    void SaveBestTimes()
    {
        if (PlayerTracker == null || !PlayerTracker.RaceCompleted || PlayerTracker.FinishTime <= 0f) return;
        string track = SceneManager.GetActiveScene().name;
        string raceKey = "BestRaceTime_" + track;
        string lapKey = "BestLapTime_" + track;
        PreviousBestTime = PlayerPrefs.GetFloat(raceKey, 0f);
        IsNewBestTime = PreviousBestTime <= 0f || PlayerTracker.FinishTime < PreviousBestTime;
        if (IsNewBestTime) PlayerPrefs.SetFloat(raceKey, PlayerTracker.FinishTime);
        float previousLap = PlayerPrefs.GetFloat(lapKey, 0f);
        if (PlayerTracker.BestLapTime > 0f && (previousLap <= 0f || PlayerTracker.BestLapTime < previousLap))
            PlayerPrefs.SetFloat(lapKey, PlayerTracker.BestLapTime);
        PlayerPrefs.Save();
    }

    public List<PlayerLapTracker> GetStandings()
    {
        List<PlayerLapTracker> standings = racers.FindAll(racer => racer != null);
        standings.Sort((a, b) =>
        {
            if (a.RaceCompleted != b.RaceCompleted) return a.RaceCompleted ? -1 : 1;
            if (a.RaceCompleted) return a.FinishPosition.CompareTo(b.FinishPosition);
            int lapOrder = b.currentLap.CompareTo(a.currentLap);
            return lapOrder != 0 ? lapOrder : b.nextCheckpointIndex.CompareTo(a.nextCheckpointIndex);
        });
        return standings;
    }

    public void StartFinishSequence()
    {
        if (!raceStarted || raceFinished) return;
        raceFinished = true;
        if (playerPosition <= 0) playerPosition = Mathf.Max(1, finishedRacers.Count);
        StartCoroutine(FinishRaceSequence());
    }

    Transform GetCarTransform()
    {
        if (PlayerTracker != null) return PlayerTracker.transform;
        return carSpawner != null && carSpawner.SpawnedCar != null ? carSpawner.SpawnedCar.transform : null;
    }

    IEnumerator StartCountdown()
    {
        while (GetCarTransform() == null && carSpawner != null) yield return null;
        Transform car = GetCarTransform();
        if (car == null)
        {
            Debug.LogWarning("RaceManager: No player car available for countdown.", this);
            yield break;
        }
        yield return new WaitForSeconds(Mathf.Max(0f, startDelay));
        if (countdownParent != null) countdownParent.SetActive(true);
        yield return ShowCountdownNumber(textThree, colorThree, angleThree, car, originalScales[0]);
        yield return ShowCountdownNumber(textTwo, colorTwo, angleTwo, car, originalScales[1]);
        yield return ShowCountdownNumber(textOne, colorOne, angleOne, car, originalScales[2]);

        if (cameraTransition != null) StopCoroutine(cameraTransition);
        yield return TransitionCamera(car, angleGo);
        // The green GO and player/AI controls become active in the same frame.
        raceStarted = true;
        RaceTime = 0f;
        if (chaseCam != null) chaseCam.holdPosition = false;
        if (textGo != null)
        {
            textGo.gameObject.SetActive(true);
            textGo.color = colorGo;
            yield return AnimateCountdownText(textGo, originalScales[3], numberAnimTime);
        }
        yield return new WaitForSeconds(Mathf.Max(0f, goStayTime));
        yield return FadeText(textGo);
        if (countdownParent != null) countdownParent.SetActive(false);
    }

    IEnumerator ShowCountdownNumber(TextMeshPro text, Color color, CountdownCameraAngle angle, Transform car, Vector3 scale)
    {
        if (cameraTransition != null) StopCoroutine(cameraTransition);
        cameraTransition = StartCoroutine(TransitionCamera(car, angle));
        if (text == null)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, numberAnimTime + numberStayTime + fadeOutTime));
            yield break;
        }
        text.gameObject.SetActive(true);
        text.color = color;
        yield return AnimateCountdownText(text, scale, numberAnimTime);
        yield return new WaitForSeconds(Mathf.Max(0f, numberStayTime));
        yield return FadeText(text);
    }

    IEnumerator AnimateCountdownText(TextMeshPro text, Vector3 scale, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration && text != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            text.transform.localScale = scale * Mathf.Lerp(1.6f, 1f, 1f - Mathf.Pow(1f - t, 3f));
            yield return null;
        }
        if (text != null) text.transform.localScale = scale;
    }

    IEnumerator FadeText(TextMeshPro text)
    {
        if (text == null) yield break;
        Color color = text.color;
        float elapsed = 0f;
        while (elapsed < fadeOutTime && text != null)
        {
            elapsed += Time.deltaTime;
            color.a = 1f - Mathf.Clamp01(elapsed / fadeOutTime);
            text.color = color;
            yield return null;
        }
        if (text != null) text.gameObject.SetActive(false);
    }

    IEnumerator TransitionCamera(Transform car, CountdownCameraAngle angle)
    {
        if (mainCam == null || car == null || angle == null) yield break;
        Vector3 target = car.position + car.rotation * angle.positionOffset;
        Vector3 lookAt = car.position + car.rotation * angle.lookAtOffset;
        Quaternion rotation = Quaternion.LookRotation(lookAt - target);
        Vector3 start = mainCam.transform.position;
        Quaternion startRotation = mainCam.transform.rotation;
        float startFov = mainCam.fieldOfView;
        float elapsed = 0f;
        while (elapsed < cameraTransitionTime && mainCam != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / cameraTransitionTime);
            mainCam.transform.SetPositionAndRotation(Vector3.Lerp(start, target, t), Quaternion.Slerp(startRotation, rotation, t));
            mainCam.fieldOfView = Mathf.Lerp(startFov, angle.fov, t);
            yield return null;
        }
        if (mainCam != null)
        {
            mainCam.transform.SetPositionAndRotation(target, rotation);
            mainCam.fieldOfView = angle.fov;
        }
    }

    IEnumerator FinishRaceSequence()
    {
        foreach (PauseMenu pause in FindObjectsByType<PauseMenu>())
            pause.CloseForRaceFinish();
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Transform car = GetCarTransform();
        if (car != null)
        {
            CarController controller = car.GetComponent<CarController>();
            if (controller != null) controller.SetControlsEnabled(false);
        }
        if (chaseCam != null) chaseCam.enabled = false;

        GameObject resultsObject = new GameObject("RaceResults");
        resultsObject.transform.SetParent(transform, false);
        resultsUI = resultsObject.AddComponent<RaceResultsUI>();
        resultsUI.ShowFinishBanner(this);
        if (finishKeyframes != null) finishKeyframes.Sort((a, b) => a.time.CompareTo(b.time));

        float elapsed = 0f;
        Rigidbody body = car != null ? car.GetComponent<Rigidbody>() : null;
        float duration = Mathf.Max(finishCinematicDuration, finishSlowMotionEnd);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(finishSlowMotionScale, 1f,
                finishSlowMotionEnd > 0f ? Mathf.Clamp01(elapsed / finishSlowMotionEnd) : 1f);
            if (car != null) EvaluateFinishCamera(car, duration > 0f ? elapsed / duration : 1f);
            yield return null;
        }
        Time.timeScale = 1f;
        if (body != null)
        {
            body.linearVelocity = body.angularVelocity = Vector3.zero;
            body.isKinematic = true;
        }
        yield return resultsUI.ShowResults(this);
    }

    void EvaluateFinishCamera(Transform car, float t)
    {
        if (mainCam == null || finishKeyframes == null || finishKeyframes.Count < 2) return;
        float targetTime = Mathf.Lerp(finishKeyframes[0].time, finishKeyframes[finishKeyframes.Count - 1].time, Mathf.Clamp01(t));
        int index = 0;
        while (index < finishKeyframes.Count - 2 && targetTime > finishKeyframes[index + 1].time) index++;
        CameraKeyframe from = finishKeyframes[index];
        CameraKeyframe to = finishKeyframes[index + 1];
        float blend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from.time, to.time, targetTime));
        Vector3 position = car.position + car.rotation * Vector3.Lerp(from.positionOffset, to.positionOffset, blend);
        Vector3 lookAt = car.position + car.rotation * Vector3.Lerp(from.lookAtOffset, to.lookAtOffset, blend);
        mainCam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
        mainCam.fieldOfView = Mathf.Lerp(from.fov, to.fov, blend);
    }

    void OnDestroy()
    {
        if (Instance != this) return;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Instance = null;
    }
}
