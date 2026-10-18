using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

public class RaceUIBinder : MonoBehaviour
{
    public TextMeshProUGUI lapText;
    public TextMeshProUGUI checkpointText;

    static readonly Color Accent = new Color(0.2f, 0.85f, 1f);
    static readonly Color Gold = new Color(1f, 0.8f, 0.25f);
    TextMeshProUGUI positionText, speedText, gearText, messageText, driftText, controlsText;
    Image speedFill;
    CarController car;
    CarAudio carAudio;
    Canvas instruments;
    readonly List<RectTransform> mapIcons = new List<RectTransform>();
    Vector2 mapCentre;
    float mapScale;
    bool mapBuilt;

    public static void EnsureRaceHUD(Transform parent)
    {
        if (FindAnyObjectByType<RaceUIBinder>() != null) return;
        GameObject hud = new GameObject("RaceHUD");
        hud.transform.SetParent(parent, false);
        hud.AddComponent<RaceUIBinder>();
    }

    void Start()
    {
        BuildHUD();
        StartCoroutine(BindUIToPlayer());
    }

    void BuildHUD()
    {
        GameObject canvasObject = new GameObject("ArcadeInstruments", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        instruments = canvasObject.GetComponent<Canvas>();
        instruments.renderMode = RenderMode.ScreenSpaceOverlay;
        instruments.sortingOrder = 20;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform position = Panel("Position", new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(240f, 116f));
        Text(position, "Label", "POSITION", new Vector2(0f, 32f), new Vector2(220f, 30f), 18f, Accent);
        positionText = Text(position, "Rank", "-- / --", new Vector2(0f, -12f), new Vector2(220f, 66f), 48f, Color.white);

        if (lapText == null || checkpointText == null)
        {
            RectTransform progress = Panel("RaceProgress", Vector2.one, new Vector2(-30f, -30f), new Vector2(390f, 116f));
            if (lapText == null) lapText = Text(progress, "Lap", "GET READY", new Vector2(0f, 26f), new Vector2(370f, 46f), 30f, Color.white);
            if (checkpointText == null) checkpointText = Text(progress, "Time", "00:00.000", new Vector2(0f, -25f), new Vector2(370f, 36f), 22f, Accent);
        }

        RectTransform speed = Panel("Speedometer", new Vector2(1f, 0f), new Vector2(-30f, 30f), new Vector2(270f, 178f));
        speedText = Text(speed, "Speed", "0", new Vector2(-15f, 20f), new Vector2(220f, 84f), 72f, Color.white);
        gearText = Text(speed, "Gear", "GEAR 1   /   KM/H", new Vector2(0f, -40f), new Vector2(245f, 36f), 18f, Accent);
        Image bar = ImageRect(speed, "SpeedBand", new Vector2(0f, -69f), new Vector2(236f, 7f), new Color(1f, 1f, 1f, 0.15f));
        speedFill = ImageRect(bar.transform, "Fill", Vector2.zero, new Vector2(236f, 7f), Accent);
        speedFill.rectTransform.anchorMin = speedFill.rectTransform.anchorMax = speedFill.rectTransform.pivot = new Vector2(0f, 0.5f);

        messageText = Text(instruments.transform, "RaceMessage", "", Vector2.zero, new Vector2(1000f, 80f), 40f, Gold);
        Anchor(messageText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -125f));
        driftText = Text(instruments.transform, "Drift", "", Vector2.zero, new Vector2(700f, 65f), 28f, Gold);
        Anchor(driftText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 90f));
        controlsText = Text(instruments.transform, "Controls", "", Vector2.zero, new Vector2(1400f, 36f), 19f, new Color(0.75f, 0.85f, 0.95f));
        Anchor(controlsText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 25f));
    }

    IEnumerator BindUIToPlayer()
    {
        PlayerLapTracker tracker = null;
        while (tracker == null)
        {
            if (RaceManager.Instance != null) tracker = RaceManager.Instance.PlayerTracker;
            if (tracker == null) yield return null;
        }
        car = tracker.GetComponent<CarController>();
        carAudio = tracker.GetComponent<CarAudio>();
        tracker.lapText = lapText;
        tracker.checkpointText = checkpointText;
        tracker.RefreshUI();
        BuildMap(RaceManager.Instance);
    }

    void Update()
    {
        RaceManager race = RaceManager.Instance;
        if (race == null || race.PlayerTracker == null) return;
        instruments.gameObject.SetActive(!race.raceFinished);
        if (race.raceFinished) return;
        PlayerLapTracker tracker = race.PlayerTracker;
        if (lapText != null) lapText.text = "LAP " + tracker.currentLap + " / " + tracker.totalLaps;
        if (checkpointText != null) checkpointText.text = "TIME " + RaceResultsUI.FormatTime(race.RaceTime);
        positionText.text = Mathf.Max(1, tracker.LivePosition) + " <size=28>/ " + Mathf.Max(1, race.RacerCount) + "</size>";
        bool sceneCountdown = race.textThree != null && race.textTwo != null && race.textOne != null && race.textGo != null;
        messageText.text = tracker.IsWrongWay ? "WRONG WAY  •  TURN AROUND" :
            !string.IsNullOrEmpty(race.RaceMessage) ? race.RaceMessage :
            !sceneCountdown && !string.IsNullOrEmpty(race.CountdownLabel) ? race.CountdownLabel : "";
        messageText.color = tracker.IsWrongWay ? new Color(1f, 0.3f, 0.2f) : Gold;
        if (car != null)
        {
            speedText.text = Mathf.RoundToInt(car.SpeedKmh).ToString();
            bool reverse = Vector3.Dot(car.CarRigidbody.linearVelocity, car.transform.forward) < -0.5f;
            gearText.text = "GEAR " + (reverse ? "R" : carAudio != null ? carAudio.CurrentGear.ToString() : "D") + "   /   KM/H";
            speedFill.rectTransform.sizeDelta = new Vector2(236f * Mathf.Clamp01(car.SpeedKmh / (car.maxForwardSpeed * 3.6f)), 7f);
            speedFill.color = car.IsDrifting ? Gold : Accent;
            driftText.text = car.CurrentDriftPoints > 0 ? "DRIFT  +" + car.CurrentDriftPoints + "   x" + car.DriftMultiplier.ToString("0.0") :
                car.DriftScore > 0 ? "DRIFT SCORE  " + car.DriftScore.ToString("N0") : "";
        }
        bool gamepad = Gamepad.current != null;
        controlsText.text = gamepad ? "RT: ACCELERATE   LT: BRAKE   X: HANDBRAKE   Y: RESET   A: REAR VIEW" :
            "WASD / ARROWS: DRIVE   SPACE: HANDBRAKE   R: RESET   C: REAR VIEW";
        Color controlColor = controlsText.color;
        controlColor.a = !race.raceStarted ? 1f : Mathf.Lerp(1f, 0.35f, Mathf.InverseLerp(8f, 16f, race.RaceTime));
        controlsText.color = controlColor;
        UpdateMap(race);
    }

    void BuildMap(RaceManager race)
    {
        if (race == null || race.Route == null || !race.Route.IsValid) return;
        const int samples = 100;
        Vector2[] points = new Vector2[samples];
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < samples; i++)
        {
            Vector3 world = race.Route.Sample(race.Route.Length * i / samples, out _);
            points[i] = new Vector2(world.x, world.z);
            min = Vector2.Min(min, points[i]);
            max = Vector2.Max(max, points[i]);
        }
        mapCentre = (min + max) * 0.5f;
        mapScale = Mathf.Min(218f / Mathf.Max(1f, max.x - min.x), 190f / Mathf.Max(1f, max.y - min.y));
        RectTransform map = Panel("CircuitMap", Vector2.zero, new Vector2(30f, 80f), new Vector2(260f, 250f));
        Text(map, "Label", "CIRCUIT", new Vector2(0f, 103f), new Vector2(240f, 28f), 17f, Accent);
        for (int i = 0; i < samples; i++)
        {
            Vector2 from = MapPosition(points[i]);
            Vector2 to = MapPosition(points[(i + 1) % samples]);
            Vector2 edge = to - from;
            Image line = ImageRect(map, "Road_" + i, (from + to) * 0.5f, new Vector2(edge.magnitude + 2f, 4f), new Color(0.5f, 0.65f, 0.75f, 0.8f));
            line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(edge.y, edge.x) * Mathf.Rad2Deg);
        }
        for (int i = 0; i < race.Racers.Count; i++)
        {
            bool player = race.Racers[i].IsPlayer;
            Image icon = ImageRect(map, "Racer_" + i, Vector2.zero, Vector2.one * (player ? 12f : 8f), player ? Accent : Gold);
            mapIcons.Add(icon.rectTransform);
            if (player) icon.transform.SetAsLastSibling();
        }
        mapBuilt = true;
    }

    Vector2 MapPosition(Vector2 world) => (world - mapCentre) * mapScale + new Vector2(0f, -9f);

    void UpdateMap(RaceManager race)
    {
        if (!mapBuilt) return;
        for (int i = 0; i < mapIcons.Count && i < race.Racers.Count; i++)
        {
            PlayerLapTracker racer = race.Racers[i];
            if (racer == null) continue;
            Vector3 position = racer.transform.position;
            mapIcons[i].anchoredPosition = MapPosition(new Vector2(position.x, position.z));
            mapIcons[i].localRotation = Quaternion.Euler(0f, 0f, -racer.transform.eulerAngles.y);
        }
    }

    RectTransform Panel(string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        Image image = ImageRect(instruments.transform, name, position, size, new Color(0.025f, 0.05f, 0.08f, 0.8f));
        RectTransform rect = image.rectTransform;
        Anchor(rect, anchor, position);
        rect.pivot = anchor;
        ImageRect(rect, "Accent", new Vector2(0f, size.y * 0.5f - 2f), new Vector2(size.x, 4f), Accent);
        return rect;
    }

    static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
    }

    static Image ImageRect(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        GameObject item = new GameObject(name, typeof(RectTransform), typeof(Image));
        item.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)item.transform;
        Anchor(rect, new Vector2(0.5f, 0.5f), position);
        rect.sizeDelta = size;
        Image image = item.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static TextMeshProUGUI Text(Transform parent, string name, string content, Vector2 position, Vector2 size, float fontSize, Color color)
    {
        GameObject item = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        item.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)item.transform;
        Anchor(rect, new Vector2(0.5f, 0.5f), position);
        rect.sizeDelta = size;
        TextMeshProUGUI text = item.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }
}
