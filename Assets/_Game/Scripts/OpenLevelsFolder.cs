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
            string directory = LevelManager.Instance.playerLevelsDirectory;
            try
            {
                System.IO.Directory.CreateDirectory(directory);
                System.Diagnostics.Process.Start("explorer.exe", directory.Replace("/", "\\"));
            }
            catch (System.Exception exception)
            {
                Debug.LogError("Could not open the levels folder: " + exception.Message);
            }
        }
    }
}
