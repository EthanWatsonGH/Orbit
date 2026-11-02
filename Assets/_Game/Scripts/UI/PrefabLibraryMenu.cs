using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PrefabLibraryMenu : MonoBehaviour
{
    [SerializeField] TMP_InputField searchInput;
    [SerializeField] ScrollRect scrollView;
    [SerializeField] RectTransform viewport;
    [SerializeField] GridLayoutGroup grid;
    [SerializeField] PrefabLibraryCard cardTemplate;
    [SerializeField] TMP_Text emptyText;
    [SerializeField] TMP_Text detailsTitle;
    [SerializeField] TMP_Text detailsType;
    [SerializeField] Image detailsPreview;

    readonly List<GameObject> cards = new List<GameObject>();
    readonly List<Sprite> createdSprites = new List<Sprite>();
    readonly List<Texture2D> createdTextures = new List<Texture2D>();
    readonly List<Entry> entries = new List<Entry>();
    LevelEditor editor;
    PrefabBar bar;
    float lastWidth = -1;

    sealed class Entry
    {
        public string name;
        public bool builtIn;
        public Sprite sprite;
    }

    public void Show(LevelEditor targetEditor, PrefabBar targetBar)
    {
        editor = targetEditor;
        bar = targetBar;
        gameObject.SetActive(true);
        searchInput.SetTextWithoutNotify(string.Empty);
        RebuildEntries();
        RefreshCards();
        ClearDetails();
    }

    public void Hide() => gameObject.SetActive(false);
    public void Close() => UIManager.Instance?.ShowLastActiveUiBeforeOpeningMainMenu();
    public void SearchChanged(string value) => RefreshCards();

    void LateUpdate()
    {
        if (viewport != null && grid != null && !Mathf.Approximately(lastWidth, viewport.rect.width))
            UpdateColumns();
    }

    void OnDestroy() => ClearEntries();

    void RebuildEntries()
    {
        ClearEntries();
        foreach (PrefabBarEntry item in bar.BuiltInEntries)
        {
            if (item == null || !item.gameObject.activeSelf || string.IsNullOrEmpty(item.BuiltInType))
                continue;
            TMP_Text label = item.GetComponentInChildren<TMP_Text>(true);
            string name = label != null && !string.IsNullOrWhiteSpace(label.text) ? label.text : item.BuiltInType;
            entries.Add(new Entry { name = name, builtIn = true, sprite = item.BuiltInIcon });
        }
        List<SavedPrefabRecord> saved = editor.GetSavedPrefabs();
        if (saved == null)
            return;
        foreach (SavedPrefabRecord record in saved)
        {
            if (record == null || string.IsNullOrEmpty(record.id))
                continue;
            entries.Add(new Entry { name = record.name ?? "Unnamed prefab", sprite = LoadPreview(record.id) });
        }
    }

    Sprite LoadPreview(string id)
    {
        byte[] bytes = editor.GetSavedPrefabPreview(id);
        if (bytes == null || bytes.Length == 0)
            return null;
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (texture.LoadImage(bytes))
            {
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
                createdTextures.Add(texture);
                createdSprites.Add(sprite);
                return sprite;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Prefab preview could not be displayed: " + exception.Message);
        }
        Destroy(texture);
        return null;
    }

    void RefreshCards()
    {
        foreach (GameObject card in cards)
        {
            if (card != null)
            {
                card.SetActive(false);
                Destroy(card);
            }
        }
        cards.Clear();
        string query = searchInput.text.Trim();
        foreach (Entry entry in entries)
        {
            if (entry.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            PrefabLibraryCard card = Instantiate(cardTemplate, grid.transform, false);
            card.gameObject.SetActive(true);
            card.Configure(entry.name, entry.sprite, entry.builtIn, () => ShowDetails(entry));
            cards.Add(card.gameObject);
        }
        emptyText.gameObject.SetActive(cards.Count == 0);
        scrollView.verticalNormalizedPosition = 1;
        UpdateColumns();
    }

    void UpdateColumns()
    {
        lastWidth = viewport.rect.width;
        float width = lastWidth - grid.padding.horizontal;
        float columnWidth = grid.cellSize.x + grid.spacing.x;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, Mathf.FloorToInt((width + grid.spacing.x) / columnWidth));
    }

    void ShowDetails(Entry entry)
    {
        detailsTitle.text = entry.name;
        detailsType.text = entry.builtIn ? "Game prefab" : "Your prefab";
        detailsPreview.sprite = entry.sprite;
        detailsPreview.enabled = entry.sprite != null;
    }

    void ClearDetails()
    {
        detailsTitle.text = "Select a prefab";
        detailsType.text = string.Empty;
        detailsPreview.sprite = null;
        detailsPreview.enabled = false;
    }

    void ClearEntries()
    {
        foreach (Sprite sprite in createdSprites) if (sprite != null) Destroy(sprite);
        foreach (Texture2D texture in createdTextures) if (texture != null) Destroy(texture);
        createdSprites.Clear();
        createdTextures.Clear();
        entries.Clear();
    }
}
