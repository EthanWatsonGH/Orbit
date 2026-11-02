using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Owns the shared row and the runtime-created prefab entries and previews.
public class PrefabBar : MonoBehaviour
{
    [SerializeField] ScrollRect scrollView;
    [SerializeField] RectTransform viewport;
    [SerializeField] RectTransform content;
    [SerializeField] PrefabBarEntry entryTemplate;
    [SerializeField, Min(0f)] float dragThresholdPixels = 20f;
    [SerializeField, Min(1f)] float entryWidth = 100f;

    readonly List<GameObject> createdEntries = new List<GameObject>();
    readonly List<Sprite> savedSprites = new List<Sprite>();
    readonly List<Texture2D> savedTextures = new List<Texture2D>();
    readonly Vector3[] barCorners = new Vector3[4];
    LevelEditor editor;
    bool dirty = true;
    int activePointerId = int.MinValue;
    int visibleEntryCount;
    float lastViewportWidth = -1f;

    public ScrollRect ScrollView => scrollView;
    public float DragThresholdPixels => dragThresholdPixels;

    public void Bind(LevelEditor targetEditor)
    {
        if (editor != targetEditor)
        {
            editor = targetEditor;
            dirty = true;
        }
        RefreshIfNeeded();
    }

    public void MarkDirty()
    {
        dirty = true;
        RefreshIfNeeded();
    }

    void OnEnable() => RefreshIfNeeded();

    void LateUpdate()
    {
        if (viewport != null && !Mathf.Approximately(lastViewportWidth, viewport.rect.width))
            UpdateContentWidth();
    }

    void OnDestroy() => ClearEntries();

    public bool BeginGesture(int pointerId)
    {
        if (activePointerId != int.MinValue)
            return false;
        activePointerId = pointerId;
        return true;
    }

    public void EndGesture(int pointerId)
    {
        if (activePointerId != pointerId)
            return;
        activePointerId = int.MinValue;
        RefreshIfNeeded();
    }

    public bool BeginPlacement(string builtInType, string savedId, int pointerId, Vector2 screenPosition)
    {
        return editor != null && editor.BeginPrefabBarPlacement(builtInType, savedId, pointerId, screenPosition);
    }

    public void EndPlacement(int pointerId, Vector2 screenPosition, bool canceled)
    {
        if (editor != null)
            editor.EndPrefabBarPlacement(pointerId, screenPosition, canceled);
    }

    public bool IsAboveBar(Vector2 screenPosition, Camera eventCamera)
    {
        RectTransform bar = transform as RectTransform;
        if (bar == null)
            return false;
        bar.GetWorldCorners(barCorners);
        float top = Mathf.Max(RectTransformUtility.WorldToScreenPoint(eventCamera, barCorners[1]).y,
                              RectTransformUtility.WorldToScreenPoint(eventCamera, barCorners[2]).y);
        return screenPosition.y > top;
    }

    void RefreshIfNeeded()
    {
        if (!dirty || !gameObject.activeInHierarchy || activePointerId != int.MinValue ||
            editor == null || content == null || entryTemplate == null || viewport == null)
            return;

        List<SavedPrefabRecord> records = editor.GetSavedPrefabs();
        if (records == null)
            return;

        ClearEntries();
        visibleEntryCount = 0;
        var entriesByKey = new Dictionary<string, PrefabBarEntry>();
        var defaultOrder = new List<string>();
        foreach (BaseObjectDefinition item in BaseObjectCatalog.All)
        {
            string key = PrefabOrder.BuiltInKey(item.Type);
            if (!entriesByKey.ContainsKey(key))
            {
                PrefabBarEntry entry = CreateEntry(item.Name, item.Type, null, item.LoadIcon());
                entriesByKey.Add(key, entry);
                defaultOrder.Add(key);
            }
        }

        foreach (SavedPrefabRecord record in records)
        {
            if (record == null || string.IsNullOrEmpty(record.id))
                continue;
            string key = PrefabOrder.PlayerKey(record.id);
            if (entriesByKey.ContainsKey(key))
                continue;
            byte[] bytes = editor.GetSavedPrefabPreview(record.id);
            Texture2D texture = null;
            if (bytes != null && bytes.Length > 0)
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (!texture.LoadImage(bytes))
                    {
                        Destroy(texture);
                        texture = null;
                    }
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning("Prefab preview could not be displayed: " + exception.Message);
                    Destroy(texture);
                    texture = null;
                }
            }
            Sprite sprite = null;
            if (texture != null)
            {
                sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                savedSprites.Add(sprite);
                savedTextures.Add(texture);
            }
            PrefabBarEntry entry = CreateEntry(record.name ?? "Unnamed prefab", null, record.id, sprite);
            entriesByKey.Add(key, entry);
            defaultOrder.Add(key);
        }

        foreach (string key in PrefabOrder.Merge(defaultOrder, editor.GetPrefabOrder()))
            PositionEntry(entriesByKey[key].transform as RectTransform, visibleEntryCount++);

        dirty = false;
        UpdateContentWidth();
    }

    PrefabBarEntry CreateEntry(string name, string builtInType, string savedId, Sprite preview)
    {
        PrefabBarEntry entry = Instantiate(entryTemplate, content, false);
        entry.name = "Prefab " + name;
        entry.Configure(builtInType, savedId);
        createdEntries.Add(entry.gameObject);

        TMP_Text label = entry.GetComponentInChildren<TMP_Text>(true);
        Image image = entry.GetComponent<Image>();
        if (label != null)
        {
            label.text = name;
            label.gameObject.SetActive(preview == null);
        }
        if (image != null && preview != null)
        {
            image.sprite = preview;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
        }
        return entry;
    }

    void PositionEntry(RectTransform entry, int index)
    {
        if (entry == null)
            return;
        entry.anchorMin = entry.anchorMax = new Vector2(0f, 0.5f);
        entry.pivot = new Vector2(0f, 0.5f);
        entry.anchoredPosition = new Vector2(index * entryWidth, 0f);
    }

    void UpdateContentWidth()
    {
        if (viewport == null || content == null)
            return;
        lastViewportWidth = viewport.rect.width;
        Vector2 size = content.sizeDelta;
        size.x = Mathf.Max(lastViewportWidth, visibleEntryCount * entryWidth);
        content.sizeDelta = size;
        if (scrollView != null)
            scrollView.horizontalNormalizedPosition = Mathf.Clamp01(scrollView.horizontalNormalizedPosition);
    }

    void ClearEntries()
    {
        foreach (GameObject entry in createdEntries)
        {
            if (entry != null)
            {
                entry.SetActive(false);
                Destroy(entry);
            }
        }
        foreach (Sprite sprite in savedSprites)
            if (sprite != null) Destroy(sprite);
        foreach (Texture2D texture in savedTextures)
            if (texture != null) Destroy(texture);
        createdEntries.Clear();
        savedSprites.Clear();
        savedTextures.Clear();
    }
}
