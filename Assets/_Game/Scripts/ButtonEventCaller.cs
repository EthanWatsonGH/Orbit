using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ButtonEventCaller : MonoBehaviour
{
    public void RecenterCamera()
    {
        EventManager.Instance.RecenterCamera();
    }

    public void LoadLevelFromPreviewPanel()
    {
        LevelManager.Instance.GetLevelJsonFromFile(gameObject.transform.Find("LevelPath").transform.GetComponent<TMP_Text>().text);
        LevelManager.Instance.LoadLevel();
        UIManager.Instance.ShowLastActiveUiBeforeOpeningMainMenu();
    }
}
