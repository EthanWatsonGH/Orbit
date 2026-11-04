using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Player-level browsing is separate from the play-mode level selection menus.
public class LevelManagementMenu : MonoBehaviour
{
    [SerializeField] TMP_InputField searchInput;
    [SerializeField] Button clearSearchButton;
    [SerializeField] ScrollRect scrollView;
    [SerializeField] GridLayoutGroup grid;
    [SerializeField] ResponsiveCardGrid cardGrid;
    [SerializeField] PrefabLibraryCard cardTemplate;
    [SerializeField] TMP_Text emptyText;
    [SerializeField] TMP_Text detailsTitle;
    [SerializeField] TMP_Text detailsStatus;
    [SerializeField] Button loadButton;
    [SerializeField] GameObject detailsPanel;
    [SerializeField] GameObject savePanel;
    [SerializeField] TMP_InputField saveNameInput;
    [SerializeField] CodeImportPanel importPanelPrefab;
    [SerializeField] CodeExportPanel exportPanelPrefab;
    [SerializeField] GameObject codePanelContainer;
    [SerializeField] RectTransform codePanelContent;
    [SerializeField] RectTransform exportPanelContent;

    readonly List<Entry> entries = new List<Entry>();
    Entry selected;
    bool loading;
    bool saving;
    bool saveViewOpen;
    int displayedRevision = -1;
    CodeImportPanel importPanel;
    CodeExportPanel exportPanel;
    ContentCardList<Entry> cards;

    sealed class Entry
    {
        public ContentRecord record;
        public Sprite sprite;
        public bool previewLoaded;
    }

    void Awake()
    {
        cards = new ContentCardList<Entry>(cardTemplate, grid.transform, entry => entry.record.displayName ?? "Unnamed level",
            entry => entry.sprite, entry => false, SelectEntry);
        importPanel = Instantiate(importPanelPrefab, codePanelContent, false);
        exportPanel = Instantiate(exportPanelPrefab, exportPanelContent, false);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        searchInput.SetTextWithoutNotify(string.Empty);
        selected = null;
        loading = false;
        saving = false;
        saveViewOpen = false;
        importPanel.Hide();
        exportPanel.Hide();
        codePanelContainer.SetActive(false);
        detailsPanel.SetActive(true);
        savePanel.SetActive(false);
        emptyText.gameObject.SetActive(false);
        clearSearchButton.gameObject.SetActive(false);
        UpdateSelection();
        ShowCurrentLevelCode();
        if (displayedRevision != LevelManager.Instance.Content.Revision(ContentCollection.PlayerLevels))
            StartCoroutine(LoadEntries());
        else
        {
            ApplyFilter(true);
            StartCoroutine(LoadPreviews());
        }
    }

    public void Hide()
    {
        StopAllCoroutines();
        importPanel.Hide();
        exportPanel.Hide();
        gameObject.SetActive(false);
    }

    public void Close()
    {
        if (!saving) UIManager.Instance?.ShowLastActiveUiBeforeOpeningMainMenu();
    }

    public void OpenImport()
    {
        if (saving) return;
        DeselectEntry();
        saveViewOpen = false;
        savePanel.SetActive(false);
        exportPanel.Hide();
        detailsPanel.SetActive(false);
        codePanelContainer.SetActive(true);
        importPanel.Show(ImportLevelCode);
        UpdateSelection();
    }

    public void CloseCodePanels()
    {
        importPanel.Hide();
        codePanelContainer.SetActive(false);
        detailsPanel.SetActive(true);
        UpdateSelection();
        ShowCurrentLevelCode();
    }

    public void ShowCurrent()
    {
        if (saving) return;
        DeselectEntry();
        importPanel.Hide();
        codePanelContainer.SetActive(false);
        detailsPanel.SetActive(true);
        saveViewOpen = false;
        savePanel.SetActive(false);
        UpdateSelection();
        ShowCurrentLevelCode();
    }

    string ImportLevelCode(string code)
    {
        if (!LevelManager.Instance.TryLoadLevelCode(code))
            return "Could not load this level. Check the code and try again.";
        Close();
        return null;
    }

    public void OpenSave()
    {
        if (saving) return;
        if (importPanel.gameObject.activeSelf) importPanel.Hide();
        codePanelContainer.SetActive(false);
        detailsPanel.SetActive(true);
        DeselectEntry();
        saveViewOpen = true;
        savePanel.SetActive(true);
        exportPanel.Hide();
        saveNameInput.SetTextWithoutNotify(string.Empty);
        UpdateSelection();
        saveNameInput.Select();
        saveNameInput.ActivateInputField();
    }

    public void SubmitSaveName(string value) => ConfirmSave();

    public void ConfirmSave()
    {
        if (!saveViewOpen || saving) return;
        saving = true;
        UpdateSelection();
        if (!LevelManager.Instance.SaveLevel(saveNameInput.text, OnSaveCompleted))
        {
            saving = false;
            UpdateSelection();
            detailsStatus.text = "Could not save this level.";
        }
    }

    void OnSaveCompleted(bool success)
    {
        saving = false;
        if (!isActiveAndEnabled) return;
        if (!success)
        {
            UpdateSelection();
            detailsStatus.text = "Could not save this level.";
            return;
        }
        saveViewOpen = false;
        savePanel.SetActive(false);
        UpdateSelection();
        ShowCurrentLevelCode();
        detailsStatus.text = "Level saved.";
        StopAllCoroutines();
        StartCoroutine(LoadEntries());
    }

    public void CancelSave()
    {
        if (saving) return;
        saveViewOpen = false;
        savePanel.SetActive(false);
        UpdateSelection();
        ShowCurrentLevelCode();
    }

    void ShowCurrentLevelCode()
    {
        if (selected == null && !saveViewOpen && !importPanel.gameObject.activeSelf)
        {
            if (LevelManager.Instance.TryGetCurrentLevelCode(out string code)) exportPanel.Show(code);
            else { exportPanel.Hide(); detailsStatus.text = "Could not generate the current level code."; }
        }
    }

    public void SearchChanged(string value) => ApplyFilter(true);

    public void ClearSearch()
    {
        searchInput.SetTextWithoutNotify(string.Empty);
        ApplyFilter(true);
        searchInput.Select();
        searchInput.ActivateInputField();
    }

    IEnumerator LoadEntries()
    {
        List<ContentRecord> records = null;
        yield return LevelManager.Instance.LoadLevelCatalog(LevelSource.PlayerLevels, result => records = result, true);
        if (records == null)
        {
            emptyText.text = "Could not load levels.";
            emptyText.gameObject.SetActive(true);
            displayedRevision = -1;
            yield break;
        }
        emptyText.text = "No levels found";
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
        ApplyFilter(true);

        displayedRevision = LevelManager.Instance.Content.Revision(ContentCollection.PlayerLevels);
        yield return LoadPreviews();
    }

    IEnumerator LoadPreviews()
    {
        foreach (Entry entry in entries)
        {
            if (entry.previewLoaded) continue;
            yield return LevelManager.Instance.LoadLevelPreview(LevelSource.PlayerLevels, entry.record, result => entry.sprite = result);
            entry.previewLoaded = true;
            ConfigureCard(entry);
        }
    }

    void ConfigureCard(Entry entry)
    {
        cards.Update(entry, selected);
    }

    void SelectEntry(Entry entry)
    {
        if (saving) return;
        if (importPanel.gameObject.activeSelf) importPanel.Hide();
        codePanelContainer.SetActive(false);
        detailsPanel.SetActive(true);
        saveViewOpen = false;
        savePanel.SetActive(false);
        exportPanel.Hide();
        Entry previous = selected;
        selected = entry;
        if (previous != null) ConfigureCard(previous);
        ConfigureCard(selected);
        UpdateSelection();
        StartCoroutine(LoadSelectedCode(entry));
    }

    void DeselectEntry()
    {
        Entry previous = selected;
        selected = null;
        if (previous != null) ConfigureCard(previous);
    }

    IEnumerator LoadSelectedCode(Entry entry)
    {
        string code = null;
        yield return LevelManager.Instance.LoadLevelCode(LevelSource.PlayerLevels, entry.record, result => code = result);
        if (entry != selected || saveViewOpen || importPanel.gameObject.activeSelf) yield break;
        if (string.IsNullOrWhiteSpace(code)) detailsStatus.text = "Could not read this level's code.";
        else exportPanel.Show(code);
    }

    void ApplyFilter(bool resetScroll)
    {
        string query = searchInput.text.Trim();
        int visibleCount = cards.Refresh(entries, query, selected);
        clearSearchButton.gameObject.SetActive(query.Length > 0);
        emptyText.gameObject.SetActive(visibleCount == 0);
        cardGrid.Refresh();
        Canvas.ForceUpdateCanvases();
        if (resetScroll) scrollView.verticalNormalizedPosition = 1f;
    }

    void UpdateSelection()
    {
        detailsTitle.text = saveViewOpen ? "Save current level" : selected == null ? "Current level" : selected.record.displayName;
        detailsStatus.text = saving ? "Saving..." : selected == null ? string.Empty : selected.record.author ?? string.Empty;
        loadButton.interactable = selected != null && !loading && !saving && !importPanel.gameObject.activeSelf;
    }

    public void LoadSelected()
    {
        if (selected != null && !loading && !saving) StartCoroutine(LoadSelectedLevel(selected.record.id));
    }

    IEnumerator LoadSelectedLevel(string id)
    {
        loading = true;
        UpdateSelection();
        bool success = false;
        yield return LevelManager.Instance.LoadLevelFromPreview(LevelSource.PlayerLevels, id, result => success = result);
        loading = false;
        if (success) Close();
        else
        {
            UpdateSelection();
            detailsStatus.text = "Could not load this level.";
        }
    }

    void ClearEntries()
    {
        cards?.Clear();
        entries.Clear();
    }

    void OnDestroy() => ClearEntries();
}
