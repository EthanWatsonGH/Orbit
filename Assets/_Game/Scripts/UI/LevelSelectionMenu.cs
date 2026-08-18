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

    int catalogVersion;
    int displayedCatalogVersion = -1;
    bool isRefreshing;

    public LevelSource Source => source;
    public bool IsOpen => gameObject.activeSelf;

    public void Show()
    {
        gameObject.SetActive(true);
        RefreshIfNeeded();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void MarkDirty()
    {
        catalogVersion++;

        if (gameObject.activeInHierarchy)
            RefreshIfNeeded();
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
            Debug.LogError("ERROR: LevelSelectionMenu is missing one or more preview references.", this);
            yield break;
        }

        isRefreshing = true;

        do
        {
            int versionBeingDisplayed = catalogVersion;
            ResetLevelPreviews();

            List<LevelCatalogRecord> records = null;
            yield return LevelManager.Instance.LoadLevelCatalog(source, loadedRecords => records = loadedRecords);

            int previewCount = 0;
            foreach (LevelCatalogRecord record in records ?? new List<LevelCatalogRecord>())
            {
                GameObject levelPreview = CreateLevelPreview(record);
                Texture2D previewTexture = null;
                yield return LevelManager.Instance.LoadLevelPreview(source, record, loadedTexture => previewTexture = loadedTexture);

                if (previewTexture == null)
                    ShowPreviewImageError(levelPreview, "Image not found");
                else
                    SetPreviewImage(levelPreview, previewTexture);

                previewCount++;
            }

            if (previewCount == 0)
                ShowNoLevelsFound();

            displayedCatalogVersion = versionBeingDisplayed;
        }
        while (gameObject.activeInHierarchy && displayedCatalogVersion != catalogVersion);

        isRefreshing = false;
    }

    void ResetLevelPreviews()
    {
        foreach (Transform levelPreview in levelsPreviewPanel)
            Destroy(levelPreview.gameObject);

        noLevelsFoundText.text = string.Empty;
    }

    void ShowNoLevelsFound()
    {
        noLevelsFoundText.text = "No levels found";
        noLevelsFoundText.color = Color.white;
    }

    GameObject CreateLevelPreview(LevelCatalogRecord record)
    {
        GameObject levelPreview = Instantiate(levelPreviewPrefab, levelsPreviewPanel);
        Transform previewContent = levelPreview.transform.GetChild(0);

        previewContent.Find("LevelName").GetComponent<TMP_Text>().text = record.displayName;
        previewContent.Find("LevelAuthor").GetComponent<TMP_Text>().text = record.author;

        ButtonEventCaller buttonEventCaller = levelPreview.GetComponentInChildren<ButtonEventCaller>(true);
        if (buttonEventCaller == null)
            Debug.LogError("ERROR: Level preview prefab is missing ButtonEventCaller.");
        else
            buttonEventCaller.ConfigureLevelPreview(source, record.id);

        return levelPreview;
    }

    void ShowPreviewImageError(GameObject levelPreview, string message)
    {
        Transform imageTransform = levelPreview.transform.GetChild(0).Find("Image");
        imageTransform.GetChild(0).GetComponent<TMP_Text>().text = message;
        imageTransform.GetComponent<Image>().color = Color.grey;
    }

    void SetPreviewImage(GameObject levelPreview, Texture2D imageTexture)
    {
        Sprite imageSprite = Sprite.Create(imageTexture, new Rect(0, 0, imageTexture.width, imageTexture.height), new Vector2(0.5f, 0.5f));
        levelPreview.transform.GetChild(0).Find("Image").GetComponent<Image>().sprite = imageSprite;
    }
}
