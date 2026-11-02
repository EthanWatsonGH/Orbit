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
    [SerializeField] GameObject detailsPanel;
    [SerializeField] GameObject reorderPanel;
    [SerializeField] Button reorderButton;
    [SerializeField] TMP_Text reorderTitle;
    [SerializeField] Button moveToTopButton;
    [SerializeField] Button moveUpFiveButton;
    [SerializeField] Button moveUpButton;
    [SerializeField] Button moveDownButton;
    [SerializeField] Button moveDownFiveButton;
    [SerializeField] Button moveToBottomButton;

    readonly List<GameObject> cards = new List<GameObject>();
    readonly List<Sprite> createdSprites = new List<Sprite>();
    readonly List<Texture2D> createdTextures = new List<Texture2D>();
    readonly List<Entry> entries = new List<Entry>();
    LevelEditor editor;
    PrefabBar bar;
    float lastWidth = -1;
    Entry selectedEntry;
    bool canReorder;

    sealed class Entry
    {
        public string name;
        public string key;
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
        selectedEntry = null;
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

    public void OpenReorder() { if (selectedEntry != null && canReorder) SetReorderMode(true); }
    public void CloseReorder() => SetReorderMode(false);
    public void MoveToTop() => MoveSelected(0);
    public void MoveUpFive() => MoveSelected(entries.IndexOf(selectedEntry) - 5);
    public void MoveUp() => MoveSelected(entries.IndexOf(selectedEntry) - 1);
    public void MoveDown() => MoveSelected(entries.IndexOf(selectedEntry) + 1);
    public void MoveDownFive() => MoveSelected(entries.IndexOf(selectedEntry) + 5);
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
    }

    void UpdateColumns()
    {
        lastWidth = viewport.rect.width;
        float width = lastWidth - grid.padding.horizontal;
        float columnWidth = grid.cellSize.x + grid.spacing.x;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, Mathf.FloorToInt((width + grid.spacing.x) / columnWidth));
    }

    void SelectEntry(Entry entry)
    {
        selectedEntry = entry;
        RefreshCards(false);
        UpdateSelection();
    }

    void SetReorderMode(bool active)
    {
        detailsPanel.SetActive(!active);
        reorderPanel.SetActive(active);
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
        moveToTopButton.interactable = moveUpFiveButton.interactable = moveUpButton.interactable = selected && index > 0;
        moveDownButton.interactable = moveDownFiveButton.interactable = moveToBottomButton.interactable = selected && index < entries.Count - 1;
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
