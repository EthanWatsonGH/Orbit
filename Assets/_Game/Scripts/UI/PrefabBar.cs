using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Owns the shared row and the runtime-created player-prefab previews.
public class PrefabBar : MonoBehaviour
{
    [SerializeField] ScrollRect scrollView;
    [SerializeField] RectTransform viewport;
    [SerializeField] RectTransform content;
    [SerializeField] PrefabBarEntry entryTemplate;
    [SerializeField, Min(0f)] float dragThresholdPixels = 20f;
    [SerializeField, Min(1f)] float entryWidth = 100f;

    readonly List<PrefabBarEntry> builtInEntries = new List<PrefabBarEntry>();
    readonly List<GameObject> savedEntries = new List<GameObject>();
    readonly List<Sprite> savedSprites = new List<Sprite>();
    readonly List<Texture2D> savedTextures = new List<Texture2D>();
    readonly Vector3[] barCorners = new Vector3[4];
    LevelEditor editor;
    bool initialized;
    bool dirty = true;
    int activePointerId = int.MinValue;
    int visibleEntryCount;
    float lastViewportWidth = -1f;

    public ScrollRect ScrollView => scrollView;
    public float DragThresholdPixels => dragThresholdPixels;

    public IEnumerable<PrefabBarEntry> BuiltInEntries => builtInEntries;

    public void Bind(LevelEditor targetEditor)
    {
        if (editor != targetEditor)
        {
            editor = targetEditor;
            dirty = true;
        }
        if (!initialized && content != null)
        {
            builtInEntries.AddRange(content.GetComponentsInChildren<PrefabBarEntry>(true));
            foreach (PrefabBarEntry entry in builtInEntries)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.BuiltInType))
                    entry.SetBuiltInIcon(Resources.Load<Sprite>(PrefabBarEntry.BuiltInIconResourceFolder + "/" + entry.BuiltInType));
            }
            initialized = true;
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

    void OnDestroy() => ClearSavedEntries();

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
        if (!dirty || !initialized || !gameObject.activeInHierarchy || activePointerId != int.MinValue ||
            editor == null || content == null || entryTemplate == null || viewport == null)
            return;

        List<SavedPrefabRecord> records = editor.GetSavedPrefabs();
        if (records == null)
            return;

        ClearSavedEntries();
        visibleEntryCount = 0;
        var entriesByKey = new Dictionary<string, PrefabBarEntry>();
        var defaultOrder = new List<string>();
        foreach (PrefabBarEntry entry in builtInEntries)
        {
            if (entry == null || !entry.gameObject.activeSelf || string.IsNullOrEmpty(entry.BuiltInType))
                continue;
            string key = PrefabOrder.BuiltInKey(entry.BuiltInType);
            if (!entriesByKey.ContainsKey(key))
            {
                entriesByKey.Add(key, entry);
                defaultOrder.Add(key);
            }
        }

        foreach (SavedPrefabRecord record in records)
        {
            if (record == null || string.IsNullOrEmpty(record.id))
                continue;
            PrefabBarEntry entry = Instantiate(entryTemplate, content, false);
            entry.name = "Saved Prefab " + record.name;
            entry.ConfigureSaved(record.id);
            TMP_Text label = entry.GetComponentInChildren<TMP_Text>(true);
            Image image = entry.GetComponent<Image>();
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
            if (image != null && texture != null)
            {
                Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                savedSprites.Add(sprite);
                savedTextures.Add(texture);
                if (label != null) label.text = string.Empty;
            }
            else
            {
                if (texture != null) Destroy(texture);
                if (label != null) label.text = record.name;
            }
            savedEntries.Add(entry.gameObject);
            string key = PrefabOrder.PlayerKey(record.id);
            if (!entriesByKey.ContainsKey(key))
            {
                entriesByKey.Add(key, entry);
                defaultOrder.Add(key);
            }
            else
                entry.gameObject.SetActive(false);
        }

        foreach (string key in PrefabOrder.Merge(defaultOrder, editor.GetPrefabOrder()))
            PositionEntry(entriesByKey[key].transform as RectTransform, visibleEntryCount++);

        dirty = false;
        UpdateContentWidth();
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

    void ClearSavedEntries()
    {
        foreach (GameObject entry in savedEntries)
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
        savedEntries.Clear();
        savedSprites.Clear();
        savedTextures.Clear();
    }
}
