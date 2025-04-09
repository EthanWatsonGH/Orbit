using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OpenLevelsFolder : MonoBehaviour
{
    private void Start()
    {
        if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
        {
            gameObject.SetActive(true);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    public void OpenLevelFolderInFileExplorer()
    {
        if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
        {
            System.Diagnostics.Process.Start("explorer.exe", LevelManager.Instance.playerLevelsDirectory.Replace("/", "\\"));
        }
    }
}
