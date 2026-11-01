using UnityEngine;

// Draws editor guides only for the active world camera, never the preview camera.
public sealed class EditorGuideRenderer
{
    static readonly int WidthPixelsId = Shader.PropertyToID("_WidthPixels");

    readonly Material material;
    readonly Mesh quad;
    readonly int layer;

    bool showMoveGuides;
    Vector3 moveOrigin;
    Vector3 moveRight;
    Vector3 moveUp;
    bool showRotationGuide;
    Vector3 rotationStart;
    Vector3 rotationEnd;

    public EditorGuideRenderer(Material material)
    {
        this.material = material;
        layer = LayerMask.NameToLayer("EditorGuides");
        quad = new Mesh { name = "Editor Guide Quad", hideFlags = HideFlags.HideAndDontSave };
        quad.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
        };
        quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quad.RecalculateBounds();
    }

    public void ShowMoveGuides(Vector3 origin, Vector3 right, Vector3 up)
    {
        moveOrigin = origin;
        moveRight = right;
        moveUp = up;
        showMoveGuides = true;
    }

    public void HideMoveGuides() => showMoveGuides = false;

    public void SetRotationGuide(Vector3 start, Vector3 end)
    {
        rotationStart = start;
        rotationEnd = end;
        showRotationGuide = true;
    }

    public void HideRotationGuide() => showRotationGuide = false;

    public void Draw(Camera camera)
    {
        if (camera == null || material == null || layer < 0 || camera.pixelWidth <= 0 || camera.pixelHeight <= 0)
            return;

        float widthPixels = Mathf.Max(material.GetFloat(WidthPixelsId), 0.1f);
        if (showMoveGuides)
        {
            DrawLine(camera, moveOrigin, moveOrigin + moveRight, true, widthPixels);
            DrawLine(camera, moveOrigin, moveOrigin + moveUp, true, widthPixels);
        }
        if (showRotationGuide)
            DrawLine(camera, rotationStart, rotationEnd, false, widthPixels);
    }

    void DrawLine(Camera camera, Vector3 start, Vector3 end, bool infinite, float widthPixels)
    {
        Vector3 screenStart = camera.WorldToScreenPoint(start);
        Vector3 screenEnd = camera.WorldToScreenPoint(end);
        if (screenStart.z <= 0f || screenEnd.z <= 0f)
            return;

        Vector2 origin = screenStart;
        Vector2 direction = (Vector2)screenEnd - origin;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        // Clip to the camera's viewport before constructing the narrow quad. This
        // keeps the movement guides visually infinite without huge world geometry.
        Rect rect = camera.pixelRect;
        float padding = widthPixels + 2f;
        rect.xMin -= padding;
        rect.xMax += padding;
        rect.yMin -= padding;
        rect.yMax += padding;
        float min = infinite ? float.NegativeInfinity : 0f;
        float max = infinite ? float.PositiveInfinity : 1f;
        if (!EditorScreenLineClipper.Clip(origin, direction, rect, ref min, ref max))
            return;

        Vector2 clippedStart = origin + direction * min;
        Vector2 clippedEnd = origin + direction * max;
        Vector2 clippedDirection = clippedEnd - clippedStart;
        if (clippedDirection.sqrMagnitude < 0.0001f)
            return;

        Vector2 perpendicular = new Vector2(-clippedDirection.y, clippedDirection.x).normalized;
        Vector2 center = (clippedStart + clippedEnd) * 0.5f;
        float depth = camera.nearClipPlane + (camera.farClipPlane - camera.nearClipPlane) * 0.01f;
        Vector3 worldCenter = camera.ScreenToWorldPoint(new Vector3(center.x, center.y, depth));
        Vector3 worldStart = camera.ScreenToWorldPoint(new Vector3(clippedStart.x, clippedStart.y, depth));
        Vector3 worldEnd = camera.ScreenToWorldPoint(new Vector3(clippedEnd.x, clippedEnd.y, depth));
        Vector2 top = center + perpendicular * (widthPixels + 2f);
        Vector3 worldTop = camera.ScreenToWorldPoint(new Vector3(top.x, top.y, depth));

        Matrix4x4 matrix = Matrix4x4.identity;
        matrix.SetColumn(0, worldEnd - worldStart);
        matrix.SetColumn(1, worldTop - worldCenter);
        matrix.SetColumn(2, camera.transform.forward);
        matrix.SetColumn(3, new Vector4(worldCenter.x, worldCenter.y, worldCenter.z, 1f));
        Graphics.DrawMesh(quad, matrix, material, layer, camera, 0, null, false, false);
    }

    public void Dispose()
    {
        if (quad != null)
            Object.Destroy(quad);
    }
}
