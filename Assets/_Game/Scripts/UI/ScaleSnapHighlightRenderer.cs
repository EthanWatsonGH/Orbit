using UnityEngine;
using UnityEngine.Rendering;

// Draws only through the active editor camera, so highlights never enter previews.
public sealed class ScaleSnapHighlightRenderer
{
    static readonly int OriginId = Shader.PropertyToID("_PathOrigin");
    static readonly int DirectionId = Shader.PropertyToID("_PathDirection");
    static readonly int MinimumId = Shader.PropertyToID("_PathMinimum");
    static readonly int MaximumId = Shader.PropertyToID("_PathMaximum");
    static readonly int SpacingId = Shader.PropertyToID("_Spacing");
    static readonly int CurrentId = Shader.PropertyToID("_CurrentPoint");
    static readonly int ShowCandidatesId = Shader.PropertyToID("_ShowCandidates");
    static readonly int CurrentRadiusId = Shader.PropertyToID("_CurrentRadiusPixels");
    static readonly int CandidateRadiusId = Shader.PropertyToID("_CandidateRadiusPixels");

    readonly Material material;
    readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
    readonly CommandBuffer commands = new CommandBuffer { name = "Editor transform highlights" };
    readonly Mesh quad;
    readonly int layer;
    Camera currentCamera;

    public ScaleSnapHighlightRenderer(Material material)
    {
        this.material = material;
        layer = LayerMask.NameToLayer("EditorGuides");
        quad = new Mesh { name = "Scale Snap Highlights Quad", hideFlags = HideFlags.HideAndDontSave };
        quad.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
        };
        quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quad.RecalculateBounds();
    }

    public void BeginFrame(Camera camera)
    {
        if (currentCamera != camera)
        {
            Clear();
            currentCamera = camera;
            if (currentCamera != null)
                currentCamera.AddCommandBuffer(CameraEvent.BeforeImageEffects, commands);
        }
        commands.Clear();
    }

    public void Clear()
    {
        if (currentCamera != null)
            currentCamera.RemoveCommandBuffer(CameraEvent.BeforeImageEffects, commands);
        currentCamera = null;
        commands.Clear();
    }

    public void Draw(Camera camera, Vector3 origin, Vector3 direction, float minimum, float maximum, float spacing, Vector3 currentPoint)
    {
        Draw(camera, origin, direction, minimum, maximum, spacing, currentPoint, true);
    }

    public void DrawPoint(Camera camera, Vector3 point)
    {
        Draw(camera, point, Vector3.right, 0f, 0f, 1f, point, false);
    }

    void Draw(Camera camera, Vector3 origin, Vector3 direction, float minimum, float maximum, float spacing, Vector3 currentPoint, bool showCandidates)
    {
        if (camera == null || camera != currentCamera || material == null || layer < 0 ||
            (camera.cullingMask & (1 << layer)) == 0 || spacing <= 0f)
            return;

        float depth = camera.WorldToScreenPoint(Vector3.zero).z;
        if (depth <= camera.nearClipPlane || depth >= camera.farClipPlane)
            return;

        Vector3 screenOrigin3D = camera.WorldToScreenPoint(origin);
        Vector3 screenNext3D = camera.WorldToScreenPoint(origin + direction);
        if (screenOrigin3D.z <= 0f || screenNext3D.z <= 0f)
            return;
        Vector2 screenOrigin = screenOrigin3D;
        Vector2 screenDirection = (Vector2)screenNext3D - screenOrigin;
        if (screenDirection.sqrMagnitude < 0.0001f)
            return;

        float radius = Mathf.Max(0f, Mathf.Max(material.GetFloat(CurrentRadiusId), material.GetFloat(CandidateRadiusId))) + 2f;
        Rect rect = camera.pixelRect;
        rect.xMin -= radius;
        rect.xMax += radius;
        rect.yMin -= radius;
        rect.yMax += radius;
        float visibleMinimum = minimum;
        float visibleMaximum = maximum;
        if (!EditorScreenLineClipper.Clip(screenOrigin, screenDirection, rect, ref visibleMinimum, ref visibleMaximum))
            return;

        // Shade only a narrow strip around the visible path, not the full screen.
        Vector2 along = screenDirection.normalized;
        Vector2 perpendicular = new Vector2(-along.y, along.x);
        Vector2 start = screenOrigin + screenDirection * visibleMinimum - along * radius;
        Vector2 end = screenOrigin + screenDirection * visibleMaximum + along * radius;
        Vector2 center = (start + end) * 0.5f;
        Vector2 top = center + perpendicular * (2f * radius);
        Vector3 worldStart = camera.ScreenToWorldPoint(new Vector3(start.x, start.y, depth));
        Vector3 worldEnd = camera.ScreenToWorldPoint(new Vector3(end.x, end.y, depth));
        Vector3 worldCenter = camera.ScreenToWorldPoint(new Vector3(center.x, center.y, depth));
        Vector3 worldTop = camera.ScreenToWorldPoint(new Vector3(top.x, top.y, depth));
        Matrix4x4 matrix = Matrix4x4.identity;
        matrix.SetColumn(0, worldEnd - worldStart);
        matrix.SetColumn(1, worldTop - worldCenter);
        matrix.SetColumn(2, camera.transform.forward);
        matrix.SetColumn(3, new Vector4(worldCenter.x, worldCenter.y, worldCenter.z, 1f));

        properties.Clear();
        properties.SetVector(OriginId, origin);
        properties.SetVector(DirectionId, direction);
        properties.SetFloat(MinimumId, minimum);
        properties.SetFloat(MaximumId, maximum);
        properties.SetFloat(SpacingId, spacing);
        properties.SetVector(CurrentId, currentPoint);
        properties.SetFloat(ShowCandidatesId, showCandidates ? 1f : 0f);
        // Sprite sorting order takes priority over the material's render queue.
        // Submit after scene geometry so these highlights stay above every object.
        commands.DrawMesh(quad, matrix, material, 0, 0, properties);
    }

    public void Dispose()
    {
        Clear();
        commands.Release();
        if (quad != null)
            Object.Destroy(quad);
    }
}
