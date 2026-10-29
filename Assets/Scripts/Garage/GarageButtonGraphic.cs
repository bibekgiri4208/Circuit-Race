using UnityEngine;
using UnityEngine.UI;

// Resolution-independent artwork: the same mesh is visible in the editor and in builds.
[AddComponentMenu("UI/Garage Button Graphic")]
public class GarageButtonGraphic : Image
{
    public enum Artwork { Menu, Previous, Next, Sound }

    [SerializeField] private Artwork artwork;
    [SerializeField] private bool muted;

    public override Texture mainTexture => Texture2D.whiteTexture;

    public void SetMuted(bool value)
    {
        muted = value;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect bounds = GetPixelAdjustedRect();
        if (bounds.width <= 0f || bounds.height <= 0f) return;

        if (artwork == Artwork.Previous || artwork == Artwork.Next)
        {
            DrawArrow(mesh, bounds, artwork == Artwork.Previous);
            return;
        }

        float unit = bounds.height / 120f;
        Rounded(mesh, bounds, 19f * unit, Hex(0x03251d), Hex(0x082b23));
        Rounded(mesh, Inset(bounds, 4f * unit, 4f * unit, 4f * unit, 5f * unit),
            16f * unit, Hex(0x00592d), Hex(0x098243));
        Rounded(mesh, Inset(bounds, 8f * unit, 13f * unit, 8f * unit, 8f * unit),
            13f * unit, Hex(0x0baf3a), Hex(0x70e615));
        Rect rim = Inset(bounds, 9f * unit, 20f * unit, 9f * unit, 8f * unit);
        Rounded(mesh, rim, 13f * unit, Hex(0xb8fa34), Hex(0xf0ff84));
        Rect face = Inset(rim, 4f * unit, 4f * unit, 4f * unit, 4f * unit);
        Rounded(mesh, face, 10f * unit, Hex(0x8ee916), Hex(0xc5ff20));

        if (artwork == Artwork.Sound)
            DrawSpeaker(mesh, face);
        else
        {
            // Racing stripes stay at the ends, leaving a clear field behind the label.
            Stripe(mesh, face, face.xMin + 3f * unit, 14f * unit, unit);
            Stripe(mesh, face, face.xMin + 29f * unit, 8f * unit, unit);
            Stripe(mesh, face, face.xMax - face.height - 12f * unit, 14f * unit, unit);
            Stripe(mesh, face, face.xMax - face.height + 14f * unit, 8f * unit, unit);
        }

        Rect gloss = Inset(bounds, 23f * unit, bounds.height - 18f * unit,
            23f * unit, 14f * unit);
        Rounded(mesh, gloss, 2f * unit, new Color(1f, 1f, 0.6f, 0.38f),
            new Color(1f, 1f, 0.6f, 0.38f));
    }

    private void Stripe(VertexHelper mesh, Rect face, float start, float width, float unit)
    {
        // Clip a diagonal parallelogram against the face's inset rectangular area.
        float bottom = face.yMin + 6f * unit;
        float top = face.yMax - 6f * unit;
        float slope = (top - bottom) * 0.85f;
        float left = face.xMin + 5f * unit;
        float right = face.xMax - 5f * unit;
        for (int i = 0; i < 24; i++)
        {
            float y0 = Mathf.Lerp(bottom, top, i / 24f);
            float y1 = Mathf.Lerp(bottom, top, (i + 1) / 24f);
            float x0 = start + slope * (i / 24f);
            float x1 = start + slope * ((i + 1) / 24f);
            Polygon(mesh, new[] {
                new Vector2(Mathf.Clamp(x0, left, right), y0),
                new Vector2(Mathf.Clamp(x1, left, right), y1),
                new Vector2(Mathf.Clamp(x1 + width, left, right), y1),
                new Vector2(Mathf.Clamp(x0 + width, left, right), y0)
            }, new Color(0.25f, 0.65f, 0.02f, 0.28f));
        }
    }

    private void DrawArrow(VertexHelper mesh, Rect rect, bool left)
    {
        Vector2[] triangle = {
            new Vector2(-0.43f, 0f), new Vector2(0.32f, 0.44f),
            new Vector2(0.32f, -0.44f)
        };
        for (int layer = 0; layer < 4; layer++)
        {
            float scale = 1f - layer * 0.105f;
            var points = new Vector2[3];
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 p = triangle[i];
                if (!left) p.x = -p.x;
                points[i] = rect.center + Vector2.Scale(p * scale, rect.size);
                if (layer == 0) points[i].y -= 3f;
            }
            Color shade = layer == 0 ? Hex(0x032725) : layer == 1 ? Hex(0x031f33) :
                layer == 2 ? Hex(0x9dfaff) : Hex(0x18c9ff);
            Polygon(mesh, points, shade, layer == 3 ? Hex(0x6ce9ff) : shade);
        }
        Vector2 center = rect.center;
        float direction = left ? 1f : -1f;
        Polygon(mesh, new[] {
            center + new Vector2(-0.28f * direction * rect.width, 0f),
            center + new Vector2(0.22f * direction * rect.width, 0.32f * rect.height),
            center + new Vector2(0.22f * direction * rect.width, 0f)
        }, new Color(0.75f, 1f, 1f, 0.35f));
    }

    private void DrawSpeaker(VertexHelper mesh, Rect face)
    {
        Vector2 center = face.center;
        float size = Mathf.Min(face.width, face.height);
        Vector2[] icon = {
            new Vector2(-0.34f, -0.12f), new Vector2(-0.19f, -0.12f),
            new Vector2(0.01f, -0.29f), new Vector2(0.01f, 0.29f),
            new Vector2(-0.19f, 0.12f), new Vector2(-0.34f, 0.12f)
        };
        for (int layer = 0; layer < 2; layer++)
        {
            var points = new Vector2[icon.Length];
            for (int i = 0; i < points.Length; i++)
                points[i] = center + icon[i] * size * (layer == 0 ? 1.13f : 1f);
            Polygon(mesh, points, layer == 0 ? Hex(0x134825) : Color.white);
        }
        if (muted)
        {
            Line(mesh, center + new Vector2(0.11f, -0.16f) * size,
                center + new Vector2(0.36f, 0.16f) * size, size * 0.07f, Hex(0x134825));
            Line(mesh, center + new Vector2(0.11f, 0.16f) * size,
                center + new Vector2(0.36f, -0.16f) * size, size * 0.07f, Hex(0x134825));
            return;
        }
        for (int wave = 0; wave < 3; wave++)
        {
            float radius = size * (0.19f + wave * 0.105f);
            for (int i = 0; i < 16; i++)
            {
                float a = Mathf.Lerp(-0.85f, 0.85f, i / 16f);
                float b = Mathf.Lerp(-0.85f, 0.85f, (i + 1) / 16f);
                Vector2 from = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                Vector2 to = center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius;
                Line(mesh, from, to, size * 0.065f, Hex(0x134825));
                Line(mesh, from, to, size * 0.032f, Color.white);
            }
        }
    }

    private void Line(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color shade)
    {
        Vector2 delta = to - from;
        Vector2 normal = new Vector2(-delta.y, delta.x).normalized * width * 0.5f;
        Polygon(mesh, new[] { from - normal, from + normal, to + normal, to - normal }, shade);
    }

    private void Rounded(VertexHelper mesh, Rect rect, float radius, Color bottom, Color top)
    {
        radius = Mathf.Min(radius, Mathf.Min(rect.width, rect.height) * 0.5f);
        var points = new Vector2[36];
        for (int corner = 0; corner < 4; corner++)
        {
            Vector2 center = new Vector2(corner < 2 ? rect.xMax - radius : rect.xMin + radius,
                corner == 0 || corner == 3 ? rect.yMin + radius : rect.yMax - radius);
            for (int step = 0; step < 9; step++)
            {
                float angle = (-90f + corner * 90f + step * 90f / 8f) * Mathf.Deg2Rad;
                points[corner * 9 + step] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
        }
        Polygon(mesh, points, bottom, top);
    }

    private void Polygon(VertexHelper mesh, Vector2[] points, Color bottom)
    {
        Polygon(mesh, points, bottom, bottom);
    }

    private void Polygon(VertexHelper mesh, Vector2[] points, Color bottom, Color top)
    {
        int first = mesh.currentVertCount;
        float minY = points[0].y;
        float maxY = minY;
        foreach (Vector2 point in points) { minY = Mathf.Min(minY, point.y); maxY = Mathf.Max(maxY, point.y); }
        for (int i = 0; i < points.Length; i++)
        {
            Color shade = Color.Lerp(bottom, top, Mathf.InverseLerp(minY, maxY, points[i].y)) * color;
            mesh.AddVert(points[i], shade, Vector2.zero);
        }
        for (int i = 1; i < points.Length - 1; i++) mesh.AddTriangle(first, first + i, first + i + 1);
    }

    private static Rect Inset(Rect rect, float left, float bottom, float right, float top)
    {
        return Rect.MinMaxRect(rect.xMin + left, rect.yMin + bottom, rect.xMax - right, rect.yMax - top);
    }

    private static Color Hex(uint rgb)
    {
        return new Color((rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f, 1f);
    }
}
