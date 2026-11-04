using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelSelectionMenu : MonoBehaviour
{
    [Header("Level Source")]
    [SerializeField] LevelSource source;
    [Header("Preview References")]
    [SerializeField] GameObject levelPreviewPrefab;
    [SerializeField] Transform levelsPreviewPanel;
    [SerializeField] TMP_Text noLevelsFoundText;

    sealed class Entry
    {
        public ContentRecord record;
        public Sprite sprite;
        public bool previewLoaded;
    }
    readonly List<Entry> entries = new List<Entry>();
    ContentEntryList<Entry, Transform> views;
    int catalogVersion;
    int displayedCatalogVersion = -1;
    bool isRefreshing;

    public LevelSource Source => source;
    public bool IsOpen => gameObject.activeSelf;

    void Awake()
    {
        views = new ContentEntryList<Entry, Transform>(
            () => Instantiate(levelPreviewPrefab, levelsPreviewPanel, false).transform,
            entry => entry.record.displayName ?? string.Empty, BindPreview);
    }

    public void Show() { gameObject.SetActive(true); RefreshIfNeeded(); }
    public void Hide() { gameObject.SetActive(false); }
    void OnDisable() { isRefreshing = false; }
    void OnDestroy() { views?.Clear(); }
    public void MarkDirty()
    {
        catalogVersion++;
        if (gameObject.activeInHierarchy) RefreshIfNeeded();
    }
    void RefreshIfNeeded()
    {
        if (!isRefreshing && displayedCatalogVersion != catalogVersion)
            StartCoroutine(RefreshLevelPreviews());
    }
    IEnumerator RefreshLevelPreviews()
    {
        if (levelPreviewPrefab == null || levelsPreviewPanel == null || noLevelsFoundText == null)
        {
            Debug.LogError("LevelSelectionMenu is missing one or more preview references.", this);
            yield break;
        }
        isRefreshing = true;
        do
        {
            int version = catalogVersion;
            List<ContentRecord> records = null;
            yield return LevelManager.Instance.LoadLevelCatalog(source, loaded => records = loaded);
            if (records == null)
            {
                noLevelsFoundText.text = "Could not load levels.";
                isRefreshing = false;
                yield break;
            }
            var previous = new Dictionary<string, Entry>();
            foreach (Entry entry in entries)
                if (!previous.ContainsKey(entry.record.id)) previous.Add(entry.record.id, entry);
            var next = new List<Entry>();
            foreach (ContentRecord record in records)
            {
                if (record == null || string.IsNullOrEmpty(record.id)) continue;
                if (!previous.TryGetValue(record.id, out Entry entry)) entry = new Entry();
                entry.record = record;
                next.Add(entry);
            }
            entries.Clear();
            entries.AddRange(next);
            views.Refresh(entries, string.Empty, null);
            noLevelsFoundText.text = entries.Count == 0 ? "No levels found" : string.Empty;
            foreach (Entry entry in entries)
            {
                if (entry.previewLoaded) continue;
                yield return LevelManager.Instance.LoadLevelPreview(source, entry.record, loaded => entry.sprite = loaded);
                entry.previewLoaded = true;
                views.Update(entry, null);
            }
            displayedCatalogVersion = version;
        }
        while (gameObject.activeInHierarchy && displayedCatalogVersion != catalogVersion);
        isRefreshing = false;
    }
    void BindPreview(Entry entry, Transform preview, bool selected)
    {
        Transform content = preview.GetChild(0);
        content.Find("LevelName").GetComponent<TMP_Text>().text = entry.record.displayName;
        content.Find("LevelAuthor").GetComponent<TMP_Text>().text = entry.record.author;
        ButtonEventCaller caller = preview.GetComponentInChildren<ButtonEventCaller>(true);
        if (caller != null) caller.ConfigureLevelPreview(source, entry.record.id);
        else Debug.LogError("Level preview prefab is missing ButtonEventCaller.", preview);
        if (!entry.previewLoaded) return;
        Transform imageTransform = content.Find("Image");
        Image image = imageTransform.GetComponent<Image>();
        if (entry.sprite == null)
        {
            imageTransform.GetChild(0).GetComponent<TMP_Text>().text = "Image not found";
            image.color = Color.grey;
        }
        else image.sprite = entry.sprite;
    }
}
