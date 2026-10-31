using System.Collections.Generic;
using UnityEngine;

// Draws editor-only outlines without adding children to the level hierarchy or
// changing the selected objects' materials.
public sealed class SelectionOutlineRenderer
{
    struct Entry
    {
        public SpriteRenderer renderer;
        public bool isCircle;
    }

    static readonly int HalfSizePixelsId = Shader.PropertyToID("_HalfSizePixels");
    static readonly int IsCircleId = Shader.PropertyToID("_IsCircle");
    static readonly int WidthPixelsId = Shader.PropertyToID("_WidthPixels");

    readonly List<Entry> entries = new List<Entry>();
    readonly HashSet<SpriteRenderer> uniqueRenderers = new HashSet<SpriteRenderer>();
    readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
    readonly Material material;
    readonly Mesh quad;
    readonly int layer;

    public SelectionOutlineRenderer(Material material)
    {
        this.material = material;
        layer = LayerMask.NameToLayer("EditorGuides");
        quad = new Mesh { name = "Selection Outline Quad", hideFlags = HideFlags.HideAndDontSave };
        quad.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
        };
        quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
        quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quad.RecalculateBounds();
    }

    public void SetSelection(IEnumerable<Transform> selectionRoots)
    {
        entries.Clear();
        uniqueRenderers.Clear();
        foreach (Transform root in selectionRoots)
        {
            if (root == null)
                continue;

            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer == null || !uniqueRenderers.Add(renderer))
                    continue;

                // A start-point sprite is a child of its circle collider; the
                // other circular level-object sprites share the collider's object.
                CircleCollider2D circle = renderer.GetComponentInParent<CircleCollider2D>();
                entries.Add(new Entry { renderer = renderer, isCircle = circle != null });
            }
        }
    }

    public void Clear()
    {
        entries.Clear();
        uniqueRenderers.Clear();
    }

    public void Draw(Camera camera)
    {
        if (camera == null || material == null || layer < 0 || entries.Count == 0)
            return;

        float widthPixels = Mathf.Max(material.GetFloat(WidthPixelsId), 0.1f);
        float paddingPixels = widthPixels + 2f;
        foreach (Entry entry in entries)
        {
            SpriteRenderer renderer = entry.renderer;
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.sprite == null)
                continue;

            Bounds bounds = renderer.sprite.bounds;
            if (bounds.size.x <= 0f || bounds.size.y <= 0f)
                continue;

            Transform target = renderer.transform;
            Vector3 center = target.TransformPoint(bounds.center);
            Vector3 centerOnScreen = camera.WorldToScreenPoint(center);
            if (centerOnScreen.z <= 0f)
                continue;

            Vector3 rightOnScreen = camera.WorldToScreenPoint(target.TransformPoint(bounds.center + Vector3.right * bounds.size.x));
            Vector3 upOnScreen = camera.WorldToScreenPoint(target.TransformPoint(bounds.center + Vector3.up * bounds.size.y));
            float widthOnScreen = Vector2.Distance(centerOnScreen, rightOnScreen);
            float heightOnScreen = Vector2.Distance(centerOnScreen, upOnScreen);
            if (widthOnScreen < 0.001f || heightOnScreen < 0.001f)
                continue;

            Vector3 expandedSize = new Vector3(
                bounds.size.x * (1f + 2f * paddingPixels / widthOnScreen),
                bounds.size.y * (1f + 2f * paddingPixels / heightOnScreen),
                1f);
            Matrix4x4 matrix = target.localToWorldMatrix * Matrix4x4.TRS(bounds.center, Quaternion.identity, expandedSize);
            properties.Clear();
            properties.SetVector(HalfSizePixelsId, new Vector4(widthOnScreen * 0.5f, heightOnScreen * 0.5f, 0f, 0f));
            properties.SetFloat(IsCircleId, entry.isCircle ? 1f : 0f);
            Graphics.DrawMesh(quad, matrix, material, layer, camera, 0, properties, false, false);
        }
    }

    public void Dispose()
    {
        Clear();
        if (quad != null)
            Object.Destroy(quad);
    }
}
