using UnityEngine;

// Camera-specific rendering keeps the editor grid out of level previews.
public sealed class EditorGridRenderer
{
    static readonly int SpacingId = Shader.PropertyToID("_Spacing");
    static readonly int OriginId = Shader.PropertyToID("_GridOrigin");
    static readonly int RightId = Shader.PropertyToID("_GridRight");
    static readonly int UpId = Shader.PropertyToID("_GridUp");
    readonly Material material;
    readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
    readonly Mesh quad;
    readonly int layer;

    public EditorGridRenderer(Material material)
    {
        this.material = material;
        layer = LayerMask.NameToLayer("EditorGuides");
        quad = new Mesh { name = "Editor Grid Quad", hideFlags = HideFlags.HideAndDontSave };
        quad.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
        };
        quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quad.RecalculateBounds();
    }

    public void Draw(Camera camera, float spacing, Vector3 origin, Vector3 right, Vector3 up)
    {
        if (camera == null || material == null || layer < 0 || spacing <= 0f)
            return;

        float depth = camera.WorldToScreenPoint(Vector3.zero).z;
        if (depth <= camera.nearClipPlane || depth >= camera.farClipPlane)
            return;
        Vector3 bottomLeft = camera.ViewportToWorldPoint(new Vector3(0f, 0f, depth));
        Vector3 bottomRight = camera.ViewportToWorldPoint(new Vector3(1f, 0f, depth));
        Vector3 topLeft = camera.ViewportToWorldPoint(new Vector3(0f, 1f, depth));
        Vector3 center = (bottomRight + topLeft) * 0.5f;
        Matrix4x4 matrix = Matrix4x4.identity;
        matrix.SetColumn(0, bottomRight - bottomLeft);
        matrix.SetColumn(1, topLeft - bottomLeft);
        matrix.SetColumn(2, camera.transform.forward);
        matrix.SetColumn(3, new Vector4(center.x, center.y, center.z, 1f));

        properties.Clear();
        properties.SetFloat(SpacingId, spacing);
        properties.SetVector(OriginId, origin);
        properties.SetVector(RightId, right);
        properties.SetVector(UpId, up);
        Graphics.DrawMesh(quad, matrix, material, layer, camera, 0, properties, false, false);
    }

    public void Dispose()
    {
        if (quad != null)
            Object.Destroy(quad);
    }
}
