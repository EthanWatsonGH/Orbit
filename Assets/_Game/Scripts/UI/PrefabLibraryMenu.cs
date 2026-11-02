using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PrefabLibraryMenu : MonoBehaviour
{
    [SerializeField] TMP_InputField searchInput;
    [SerializeField] Button clearSearchButton;
    [SerializeField] ScrollRect scrollView;
    [SerializeField] RectTransform viewport;
    [SerializeField] GridLayoutGroup grid;
    [SerializeField, Min(1)] int columnCount = 5;
    [SerializeField] PrefabLibraryCard cardTemplate;
    [SerializeField] TMP_Text emptyText;
    [SerializeField] TMP_Text detailsTitle;
    [SerializeField] TMP_Text detailsType;
    [SerializeField] Image detailsPreview;
    [SerializeField] GameObject detailsPanel;
    [SerializeField] GameObject reorderPanel;
    [SerializeField] Button reorderButton;
    [SerializeField] TMP_Text reorderTitle;
    [SerializeField] Button moveToTopButton;
    [SerializeField] Button moveUpRowButton;
    [SerializeField] Button moveEarlierButton;
    [SerializeField] Button moveLaterButton;
    [SerializeField] Button moveDownRowButton;
    [SerializeField] Button moveToBottomButton;

    readonly List<GameObject> cards = new List<GameObject>();
    readonly List<Sprite> createdSprites = new List<Sprite>();
    readonly List<Texture2D> createdTextures = new List<Texture2D>();
    readonly List<Entry> entries = new List<Entry>();
    LevelEditor editor;
    PrefabBar bar;
    float lastWidth = -1;
    float cardHeightToWidth;
    float searchScrollBeforeReorder;
    string searchBeforeReorder;
    bool reordering;
    ColorBlock reorderButtonColors;
    readonly Vector3[] cardCorners = new Vector3[4];
    Entry selectedEntry;
    bool canReorder;

    sealed class Entry
    {
        public string name;
        public string key;
        public bool builtIn;
        public Sprite sprite;
    }

    void Awake()
    {
        cardHeightToWidth = grid.cellSize.x > 0 ? grid.cellSize.y / grid.cellSize.x : 1f;
        reorderButtonColors = reorderButton.colors;
    }

    public void Show(LevelEditor targetEditor, PrefabBar targetBar)
    {
        editor = targetEditor;
        bar = targetBar;
        gameObject.SetActive(true);
        searchInput.SetTextWithoutNotify(string.Empty);
        selectedEntry = null;
        reordering = false;
        RebuildEntries();
        RefreshCards();
        SetReorderMode(false);
        UpdateSelection();
    }

    public void Hide() => gameObject.SetActive(false);
    public void Close() => UIManager.Instance?.ShowLastActiveUiBeforeOpeningMainMenu();
    public void SearchChanged(string value) => RefreshCards();
    public void ClearSearch()
    {
        searchInput.SetTextWithoutNotify(string.Empty);
        RefreshCards();
        searchInput.Select();
        searchInput.ActivateInputField();
    }

    public void ToggleReorder()
    {
        if (reordering)
        {
            SetReorderMode(false);
            searchInput.SetTextWithoutNotify(searchBeforeReorder ?? string.Empty);
            RefreshCards();
            scrollView.verticalNormalizedPosition = searchScrollBeforeReorder;
        }
        else if (selectedEntry != null && canReorder)
        {
            searchBeforeReorder = searchInput.text;
            searchScrollBeforeReorder = scrollView.verticalNormalizedPosition;
            searchInput.DeactivateInputField();
            searchInput.SetTextWithoutNotify(string.Empty);
            SetReorderMode(true);
            RefreshCards();
        }
    }
    public void MoveToTop() => MoveSelected(0);
    public void MoveUpRow() => MoveSelected(entries.IndexOf(selectedEntry) - grid.constraintCount);
    public void MoveEarlier() => MoveSelected(entries.IndexOf(selectedEntry) - 1);
    public void MoveLater() => MoveSelected(entries.IndexOf(selectedEntry) + 1);
    public void MoveDownRow() => MoveSelected(entries.IndexOf(selectedEntry) + grid.constraintCount);
    public void MoveToBottom() => MoveSelected(entries.Count - 1);

    void LateUpdate()
    {
        if (viewport != null && grid != null && !Mathf.Approximately(lastWidth, viewport.rect.width))
            UpdateColumns();
    }

    void OnDestroy() => ClearEntries();

    void RebuildEntries()
    {
        ClearEntries();
        foreach (BaseObjectDefinition item in BaseObjectCatalog.All)
        {
            entries.Add(new Entry { key = PrefabOrder.BuiltInKey(item.Type), name = item.Name, builtIn = true, sprite = item.LoadIcon() });
        }
        List<SavedPrefabRecord> saved = editor.GetSavedPrefabs();
        if (saved != null)
        {
            foreach (SavedPrefabRecord record in saved)
            {
                if (record == null || string.IsNullOrEmpty(record.id))
                    continue;
                entries.Add(new Entry { key = PrefabOrder.PlayerKey(record.id), name = record.name ?? "Unnamed prefab", sprite = LoadPreview(record.id) });
            }
        }
        List<string> savedOrder = editor.GetPrefabOrder();
        canReorder = saved != null && savedOrder != null;
        var entriesByKey = new Dictionary<string, Entry>();
        var defaultOrder = new List<string>();
        foreach (Entry entry in entries)
            if (!entriesByKey.ContainsKey(entry.key))
            {
                entriesByKey.Add(entry.key, entry);
                defaultOrder.Add(entry.key);
            }
        entries.Clear();
        foreach (string key in PrefabOrder.Merge(defaultOrder, savedOrder))
            entries.Add(entriesByKey[key]);
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

    void RefreshCards(bool resetScroll = true)
    {
        float scrollPosition = scrollView.verticalNormalizedPosition;
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
            card.Configure(entry.name, entry.sprite, entry.builtIn, entry == selectedEntry, () => SelectEntry(entry));
            cards.Add(card.gameObject);
        }
        emptyText.gameObject.SetActive(cards.Count == 0);
        UpdateColumns();
        Canvas.ForceUpdateCanvases();
        scrollView.verticalNormalizedPosition = resetScroll ? 1 : scrollPosition;
        if (reordering) KeepSelectionVisible();
    }

    void UpdateColumns()
    {
        lastWidth = viewport.rect.width;
        int columns = Mathf.Max(1, columnCount);
        float width = lastWidth - grid.padding.horizontal - grid.spacing.x * (columns - 1);
        float cardWidth = Mathf.Max(1, width / columns);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.cellSize = new Vector2(cardWidth, cardWidth * cardHeightToWidth);
    }

    void SelectEntry(Entry entry)
    {
        selectedEntry = entry;
        RefreshCards(false);
        UpdateSelection();
    }

    void SetReorderMode(bool active)
    {
        reordering = active;
        detailsPanel.SetActive(!active);
        reorderPanel.SetActive(active);
        searchInput.interactable = !active;
        clearSearchButton.interactable = !active;
        ColorBlock colors = reorderButtonColors;
        if (active)
        {
            colors.normalColor = new Color(.55f, .75f, 1f, 1f);
            colors.highlightedColor = colors.normalColor;
            colors.selectedColor = colors.normalColor;
        }
        reorderButton.colors = colors;
    }

    void KeepSelectionVisible()
    {
        int index = entries.IndexOf(selectedEntry);
        if (index < 0 || index >= cards.Count) return;
        var card = cards[index].transform as RectTransform;
        if (card == null) return;
        card.GetWorldCorners(cardCorners);
        float bottom = viewport.InverseTransformPoint(cardCorners[0]).y;
        float top = viewport.InverseTransformPoint(cardCorners[1]).y;
        float adjustment = bottom < viewport.rect.yMin ? viewport.rect.yMin - bottom :
            top > viewport.rect.yMax ? viewport.rect.yMax - top : 0f;
        if (!Mathf.Approximately(adjustment, 0f))
            scrollView.content.anchoredPosition += new Vector2(0f, adjustment);
    }

    void UpdateSelection()
    {
        int index = entries.IndexOf(selectedEntry);
        bool selected = index >= 0;
        reorderButton.interactable = selected && canReorder;
        detailsTitle.text = selected ? selectedEntry.name : "Select a prefab";
        detailsType.text = selected ? (selectedEntry.builtIn ? "Game prefab" : "Your prefab") : string.Empty;
        detailsPreview.sprite = selected ? selectedEntry.sprite : null;
        detailsPreview.enabled = selected && selectedEntry.sprite != null;
        reorderTitle.text = selected ? selectedEntry.name : string.Empty;
        moveToTopButton.interactable = moveUpRowButton.interactable = moveEarlierButton.interactable = selected && index > 0;
        moveLaterButton.interactable = moveDownRowButton.interactable = moveToBottomButton.interactable = selected && index < entries.Count - 1;
    }

    void MoveSelected(int destination)
    {
        int index = entries.IndexOf(selectedEntry);
        if (!canReorder || index < 0) return;
        destination = Mathf.Clamp(destination, 0, entries.Count - 1);
        if (index == destination) return;
        entries.RemoveAt(index);
        entries.Insert(destination, selectedEntry);
        var keys = new List<string>();
        foreach (Entry entry in entries) keys.Add(entry.key);
        if (!editor.SavePrefabOrder(keys))
        {
            entries.RemoveAt(destination);
            entries.Insert(index, selectedEntry);
            return;
        }
        bar.MarkDirty();
        RefreshCards(false);
        UpdateSelection();
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
