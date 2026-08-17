using System.Collections;
using UnityEngine;

public class ButtonEventCaller : MonoBehaviour
{
    LevelSource previewLevelSource;
    string previewLevelId;

    public void RecenterCamera()
    {
        EventManager.Instance.RecenterCamera();
    }

    public void ShowPlayerLevelSelectionMenu()
    {
        UIManager.Instance.ShowPlayerLevelSelectionMenu();
    }

    public void ShowGameLevelSelectionMenu()
    {
        UIManager.Instance.ShowGameLevelSelectionMenu();
    }

    public void ShowLastActiveUiBeforeOpeningMainMenu()
    {
        UIManager.Instance.ShowLastActiveUiBeforeOpeningMainMenu();
    }

    public void LoadLevelFromPreviewPanel()
    {
        StartCoroutine(LoadLevelFromPreview(previewLevelSource, previewLevelId));
    }

    public void ConfigureLevelPreview(LevelSource source, string contentId)
    {
        previewLevelSource = source;
        previewLevelId = contentId;
    }

    IEnumerator LoadLevelFromPreview(LevelSource source, string contentId)
    {
        bool didLoad = false;
        yield return LevelManager.Instance.LoadLevelFromPreview(source, contentId, loaded => didLoad = loaded);

        if (didLoad)
            UIManager.Instance.ShowLastActiveUiBeforeOpeningMainMenu();
    }

    public void LoadLevelFromLevelCodeInput()
    {
        LevelManager.Instance.LoadLevelFromLevelCodeInput();
    }

    public void StartScalingFromEdge()
    {
        
    }
}
