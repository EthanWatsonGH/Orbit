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
    [SerializeField] ResponsiveCardGrid cardGrid;
    [SerializeField] PrefabLibraryCard cardTemplate;
    [SerializeField] TMP_Text emptyText;
    [SerializeField] TMP_Text detailsTitle;
    [SerializeField] TMP_Text detailsStatus;
    [SerializeField] GameObject detailsPanel;
    [SerializeField] GameObject playerPrefabActions;
    [SerializeField] RectTransform exportPanelContent;
    [SerializeField] GameObject renamePanel;
    [SerializeField] TMP_InputField renameInput;
    [SerializeField] GameObject deleteConfirmationPanel;
    [SerializeField] CodeImportPanel importPanelPrefab;
    [SerializeField] CodeExportPanel exportPanelPrefab;
    [SerializeField] GameObject codePanelContainer;
    [SerializeField] RectTransform codePanelContent;
    [SerializeField] TMP_Text codePanelTitle;
    [SerializeField] GameObject reorderPanel;
    [SerializeField] Button reorderButton;
    [SerializeField] TMP_Text reorderTitle;
    [SerializeField] Button moveToTopButton;
    [SerializeField] Button moveUpRowButton;
    [SerializeField] Button moveEarlierButton;
    [SerializeField] Button moveLaterButton;
    [SerializeField] Button moveDownRowButton;
    [SerializeField] Button moveToBottomButton;

    readonly List<Entry> entries = new List<Entry>();
    LevelEditor editor;
    PrefabBar bar;
    CodeImportPanel importPanel;
    CodeExportPanel exportPanel;
    ContentCardList<Entry> cards;
    float searchScrollBeforeReorder;
    string searchBeforeReorder;
    bool reordering;
    ColorBlock reorderButtonColors;
    readonly Vector3[] cardCorners = new Vector3[4];
    Entry selectedEntry;
    bool canReorder;
    int displayedRevision = -1;
    sealed class Entry
    {
        public string name;
        public string key;
        public string id;
        public bool builtIn;
        public Sprite sprite;
    }

    void Awake()
    {
        cards = new ContentCardList<Entry>(cardTemplate, grid.transform, entry => entry.name,
            entry => entry.sprite, entry => entry.builtIn, SelectEntry);
        reorderButtonColors = reorderButton.colors;
        importPanel = Instantiate(importPanelPrefab, codePanelContent, false);
        exportPanel = Instantiate(exportPanelPrefab, exportPanelContent, false);
    }

    public void Show(LevelEditor targetEditor, PrefabBar targetBar)
    {
        editor = targetEditor;
        bar = targetBar;
        gameObject.SetActive(true);
        searchInput.SetTextWithoutNotify(string.Empty);
        selectedEntry = null;
        reordering = false;
        importPanel.Hide();
        exportPanel.Hide();
        codePanelContainer.SetActive(false);
        if (displayedRevision != LevelManager.Instance.Content.Revision(ContentCollection.PlayerPrefabs)) RebuildEntries();
        RefreshCards();
        SetReorderMode(false);
        UpdateSelection();
    }

    public void Hide()
    {
        importPanel.Hide();
        exportPanel.Hide();
        codePanelContainer.SetActive(false);
        gameObject.SetActive(false);
    }
    public void Close() => UIManager.Instance?.ShowLastActiveUiBeforeOpeningMainMenu();
    public void SearchChanged(string value) => RefreshCards();
    public void ClearSearch()
    {
        searchInput.SetTextWithoutNotify(string.Empty);
        RefreshCards();
        searchInput.Select();
        searchInput.ActivateInputField();
    }

    public void StartRename()
    {
        if (selectedEntry == null || selectedEntry.builtIn) return;
        detailsStatus.text = string.Empty;
        renameInput.SetTextWithoutNotify(selectedEntry.name);
        SetDetailsActionView(1);
        renameInput.Select();
        renameInput.ActivateInputField();
    }

    public void SubmitRename(string value) => SaveRename();

    public void SaveRename()
    {
        if (selectedEntry == null || selectedEntry.builtIn) return;
        string name = renameInput.text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            detailsStatus.text = "Enter a name for this prefab.";
            return;
        }
        if (!editor.RenameSavedPrefab(selectedEntry.id, name))
        {
            detailsStatus.text = "Could not rename this prefab.";
            return;
        }
        selectedEntry.name = name;
        bar.MarkDirty();
        displayedRevision = LevelManager.Instance.Content.Revision(ContentCollection.PlayerPrefabs);
        RefreshCards(false);
        SetDetailsActionView(0);
        UpdateSelection();
    }

    public void StartDelete()
    {
        detailsStatus.text = string.Empty;
        if (selectedEntry != null && !selectedEntry.builtIn) SetDetailsActionView(2);
    }

    public void ConfirmDelete()
    {
        if (selectedEntry == null || selectedEntry.builtIn) return;
        if (!editor.DeleteSavedPrefab(selectedEntry.id))
        {
            detailsStatus.text = "Could not delete this prefab.";
            return;
        }
        cards.Remove(selectedEntry);
        entries.Remove(selectedEntry);
        selectedEntry = null;
        bar.MarkDirty();
        displayedRevision = LevelManager.Instance.Content.Revision(ContentCollection.PlayerPrefabs);
        RefreshCards(false);
        SetDetailsActionView(0);
        UpdateSelection();
    }

    public void CancelPrefabAction()
    {
        SetDetailsActionView(0);
        UpdateSelection();
    }

    public void OpenImport()
    {
        if (importPanel.gameObject.activeSelf) return;
        if (reordering) ToggleReorder();
        selectedEntry = null;
        SetDetailsActionView(0);
        RefreshCards(false);
        UpdateSelection();
        detailsPanel.SetActive(false);
        codePanelTitle.text = "Import Code";
        codePanelContainer.SetActive(true);
        importPanel.Show(ImportPrefabCode);
    }

    public void CloseCodePanels()
    {
        importPanel.Hide();
        codePanelContainer.SetActive(false);
        detailsPanel.SetActive(true);
        UpdateSelection();
    }

    string ImportPrefabCode(string code)
    {
        if (!editor.TryImportPrefab(code, out string id))
            return "Could not import this prefab. Check the code and try again.";
        bar.MarkDirty();
        CloseCodePanels();
        searchInput.SetTextWithoutNotify(string.Empty);
        RebuildEntries();
        selectedEntry = entries.Find(entry => entry.id == id);
        SetDetailsActionView(0);
        RefreshCards();
        KeepSelectionVisible();
        UpdateSelection();
        if (selectedEntry != null) detailsStatus.text = "Prefab imported.";
        return null;
    }

    public void ToggleReorder()
    {
        if (importPanel.gameObject.activeSelf) CloseCodePanels();
        if (reordering)
        {
            SetReorderMode(false);
            searchInput.SetTextWithoutNotify(searchBeforeReorder ?? string.Empty);
            RefreshCards();
            scrollView.verticalNormalizedPosition = searchScrollBeforeReorder;
        }
        else if (selectedEntry != null && canReorder)
        {
            SetDetailsActionView(0);
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

    void OnDestroy() => ClearEntries();

    void RebuildEntries()
    {
        var previous = new Dictionary<string, Entry>();
        foreach (Entry entry in entries)
            if (!previous.ContainsKey(entry.key)) previous.Add(entry.key, entry);
        var entriesByKey = new Dictionary<string, Entry>();
        var defaultOrder = new List<string>();
        foreach (ContentRecord item in LevelManager.Instance.Content.LocalEntries(ContentCollection.GamePrefabs))
        {
            string key = PrefabOrder.BuiltInKey(item.id);
            if (!previous.TryGetValue(key, out Entry entry)) entry = new Entry();
            entry.key = key; entry.name = item.displayName; entry.builtIn = true;
            entry.sprite = LevelManager.Instance.Content.LocalPreview(ContentCollection.GamePrefabs, item);
            if (!entriesByKey.ContainsKey(key)) { entriesByKey.Add(key, entry); defaultOrder.Add(key); }
        }
        List<ContentRecord> saved = editor.GetSavedPrefabs();
        if (saved != null)
            foreach (ContentRecord record in saved)
            {
                if (record == null || string.IsNullOrEmpty(record.id)) continue;
                string key = PrefabOrder.PlayerKey(record.id);
                if (!previous.TryGetValue(key, out Entry entry)) entry = new Entry();
                entry.key = key; entry.id = record.id; entry.name = record.displayName ?? "Unnamed prefab";
                entry.builtIn = false; entry.sprite = editor.GetSavedPrefabPreview(record);
                if (!entriesByKey.ContainsKey(key)) { entriesByKey.Add(key, entry); defaultOrder.Add(key); }
            }
        List<string> savedOrder = editor.GetPrefabOrder();
        canReorder = saved != null && savedOrder != null;
        entries.Clear();
        foreach (string key in PrefabOrder.Merge(defaultOrder, savedOrder)) entries.Add(entriesByKey[key]);
        displayedRevision = LevelManager.Instance.Content.Revision(ContentCollection.PlayerPrefabs);
    }

    void RefreshCards(bool resetScroll = true)
    {
        float scrollPosition = scrollView.verticalNormalizedPosition;
        string query = searchInput.text.Trim();
        int visibleCount = cards.Refresh(entries, query, selectedEntry);
        emptyText.gameObject.SetActive(visibleCount == 0);
        cardGrid.Refresh();
        Canvas.ForceUpdateCanvases();
        scrollView.verticalNormalizedPosition = resetScroll ? 1 : scrollPosition;
        if (reordering) KeepSelectionVisible();
    }

    void SelectEntry(Entry entry)
    {
        if (importPanel.gameObject.activeSelf) CloseCodePanels();
        selectedEntry = entry;
        SetDetailsActionView(0);
        RefreshCards(false);
        UpdateSelection();
    }

    void SetDetailsActionView(int view)
    {
        bool playerPrefab = selectedEntry != null && !selectedEntry.builtIn;
        playerPrefabActions.SetActive(playerPrefab && view == 0);
        renamePanel.SetActive(playerPrefab && view == 1);
        deleteConfirmationPanel.SetActive(playerPrefab && view == 2);
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
        PrefabLibraryCard selectedCard = cards.Get(selectedEntry);
        if (selectedCard == null || !selectedCard.gameObject.activeSelf) return;
        var card = selectedCard.transform as RectTransform;
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
        if (!selected) SetDetailsActionView(0);
        reorderButton.interactable = selected && canReorder;
        detailsTitle.text = selected ? selectedEntry.name : "Select a prefab";
        detailsStatus.text = string.Empty;
        if (selected && !selectedEntry.builtIn)
        {
            string code = editor.GetSavedPrefabCode(selectedEntry.id);
            if (string.IsNullOrWhiteSpace(code))
            {
                exportPanel.Hide();
                detailsStatus.text = "Could not read this prefab's code.";
            }
            else
                exportPanel.Show(code);
        }
        else
            exportPanel.Hide();
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
        displayedRevision = LevelManager.Instance.Content.Revision(ContentCollection.PlayerPrefabs);
        RefreshCards(false);
        UpdateSelection();
    }

    void ClearEntries()
    {
        cards?.Clear();
        entries.Clear();
    }
}
