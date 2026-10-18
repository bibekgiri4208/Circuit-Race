using UnityEngine;

// A distance-based closed route shared by navigation, standings and recovery.
// Sampling along segments avoids steering at a point that has already gone behind the car.
public sealed class RaceRoute
{
    readonly Vector3[] points;
    readonly float[] distances;
    readonly float[] speedLimits;
    public int Count => points.Length;
    public float Length { get; }
    public bool IsValid => Count >= 3 && Length > 1f;

    public RaceRoute(Transform[] waypoints)
    {
        int count = waypoints != null ? waypoints.Length : 0;
        points = new Vector3[count];
        distances = new float[count + 1];
        speedLimits = new float[count];
        for (int i = 0; i < count; i++)
        {
            if (waypoints[i] == null) return;
            points[i] = waypoints[i].position;
            AIWaypoint data = waypoints[i].GetComponent<AIWaypoint>();
            speedLimits[i] = data != null ? Mathf.Max(15f, data.targetSpeedKmh) / 3.6f : float.PositiveInfinity;
        }
        for (int i = 0; i < count; i++)
            distances[i + 1] = distances[i] + Vector3.Distance(points[i], points[(i + 1) % count]);
        Length = distances[count];
    }

    public float Project(Vector3 position, ref int segment, out float distanceFromRoute)
    {
        float best = float.PositiveInfinity;
        float progress = 0f;
        int closest = segment;
        // Stay on the current branch at hairpins and crossings. Search the whole route only
        // on initial acquisition or when genuinely far off the local route.
        if (segment >= 0 && segment < Count)
            for (int offset = -2; offset <= 3; offset++)
                ProjectSegment(position, (segment + offset + Count) % Count, ref best, ref progress, ref closest);
        if (best > 35f * 35f)
            for (int i = 0; i < Count; i++)
                ProjectSegment(position, i, ref best, ref progress, ref closest);
        segment = closest;
        distanceFromRoute = Mathf.Sqrt(best);
        return progress;
    }

    void ProjectSegment(Vector3 position, int index, ref float best, ref float progress, ref int closest)
    {
        Vector3 edge = points[(index + 1) % Count] - points[index];
        float t = Mathf.Clamp01(Vector3.Dot(position - points[index], edge) / Mathf.Max(0.001f, edge.sqrMagnitude));
        float distance = (position - (points[index] + edge * t)).sqrMagnitude;
        if (distance >= best) return;
        best = distance;
        closest = index;
        progress = Mathf.Lerp(distances[index], distances[index + 1], t);
    }

    public Vector3 Sample(float distance, out Vector3 direction)
    {
        int segment = SegmentAt(distance);
        float wrapped = Mathf.Repeat(distance, Length);
        direction = (points[(segment + 1) % Count] - points[segment]).normalized;
        float t = Mathf.InverseLerp(distances[segment], distances[segment + 1], wrapped);
        return Vector3.Lerp(points[segment], points[(segment + 1) % Count], t);
    }

    public float SpeedLimitAt(float distance) => speedLimits[(SegmentAt(distance) + 1) % Count];

    int SegmentAt(float distance)
    {
        float wrapped = Mathf.Repeat(distance, Length);
        int low = 0;
        int high = Count - 1;
        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            if (distances[middle] <= wrapped) low = middle;
            else high = middle - 1;
        }
        return low;
    }
}
