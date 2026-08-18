using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ConvertLevelSelectionMenuToUiPanel
{
    const string PanelPrefabPath = "Assets/_Game/Prefabs/UI/LevelSelectionMenuPanel.prefab";

    sealed class MenuEntry
    {
        public LevelSelectionMenu menu;
        public LevelSource source;
        public string name;
        public bool wasActive;
    }

    [MenuItem("Orbit/UI/Convert Level Selection Menu To UIRoot Panel")]
    static void Convert()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Menu conversion unavailable", "Exit Play Mode before converting the level selection menu.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.path.EndsWith("/Main.unity", StringComparison.Ordinal))
        {
            EditorUtility.DisplayDialog("Open Main first", "Open the Main scene before converting the level selection menu.", "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath) != null)
        {
            EditorUtility.DisplayDialog("Menu panel already exists", "LevelSelectionMenuPanel.prefab already exists. This one-time conversion will not overwrite it.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Convert level selection menu",
                "This creates a UI panel prefab from the current Game level menu, removes its standalone Canvas components, replaces all three menu instances under UIRoot/ModalMenus, and updates UIManager. Save or commit your work first.",
                "Convert",
                "Cancel"))
            return;

        try
        {
            UIManager uiManager = UnityEngine.Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
            Transform modalMenus = GameObject.Find("UIRoot")?.transform.Find("ModalMenus");
            if (uiManager == null || modalMenus == null)
                throw new InvalidOperationException("Main needs UIManager and UIRoot/ModalMenus before converting the level menus.");

            List<MenuEntry> entries = GetMenuEntries(scene);
            LevelSelectionMenu gameMenu = null;
            foreach (MenuEntry entry in entries)
            {
                if (entry.source == LevelSource.Game)
                {
                    gameMenu = entry.menu;
                    break;
                }
            }

            if (gameMenu == null)
                throw new InvalidOperationException("Main needs a Game level selection menu to use as the panel template.");

            GameObject panelPrefab = CreatePanelPrefab(gameMenu, modalMenus);

            foreach (MenuEntry entry in entries)
            {
                if (entry.menu != null)
                    UnityEngine.Object.DestroyImmediate(entry.menu.gameObject);
            }

            LevelSelectionMenu gameMenuInstance = null;
            LevelSelectionMenu playerMenuInstance = null;
            LevelSelectionMenu downloadedMenuInstance = null;
            foreach (MenuEntry entry in entries)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, scene);
                instance.name = entry.name;
                instance.transform.SetParent(modalMenus, false);
                ConfigurePanelTransform((RectTransform)instance.transform);

                LevelSelectionMenu menu = instance.GetComponent<LevelSelectionMenu>();
                SetSource(menu, entry.source);
                instance.SetActive(entry.wasActive);

                switch (entry.source)
                {
                    case LevelSource.Game:
                        gameMenuInstance = menu;
                        break;
                    case LevelSource.PlayerLevels:
                        playerMenuInstance = menu;
                        break;
                    case LevelSource.DownloadedLevels:
                        downloadedMenuInstance = menu;
                        break;
                }
            }

            SetReference(uiManager, "gameLevelSelectionMenu", gameMenuInstance);
            SetReference(uiManager, "playerLevelSelectionMenu", playerMenuInstance);
            SetReference(uiManager, "downloadedLevelSelectionMenu", downloadedMenuInstance);

            EditorUtility.SetDirty(uiManager);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);

            EditorUtility.DisplayDialog("Menu conversion complete", "The three level menus are now UI panels beneath UIRoot/ModalMenus and use UIRoot's Canvas Scaler. Test opening each menu and its Cancel button.", "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Menu conversion stopped", "Check the Console and use version control to restore any partial asset changes before trying again.", "OK");
        }
    }

    [MenuItem("Orbit/UI/Repair Level Selection Menu Panel")]
    static void RepairPanel()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Menu repair unavailable", "Exit Play Mode before repairing the level selection menu panel.", "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath) == null)
        {
            EditorUtility.DisplayDialog("Panel not found", "Run the level selection menu conversion first.", "OK");
            return;
        }

        try
        {
            GameObject panel = PrefabUtility.LoadPrefabContents(PanelPrefabPath);
            try
            {
                RemoveLegacyCanvasComponents(panel);
                ConfigurePanelTransform(panel.GetComponent<RectTransform>());
                PrefabUtility.SaveAsPrefabAsset(panel, PanelPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(panel);
            }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Menu panel repaired", "The menu panel now uses UIRoot's Canvas and Canvas Scaler only. Test opening each level selection menu.", "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Menu repair stopped", "Check the Console and use version control to restore any partial asset changes before trying again.", "OK");
        }
    }

    static List<MenuEntry> GetMenuEntries(Scene scene)
    {
        List<MenuEntry> entries = new List<MenuEntry>();
        foreach (LevelSelectionMenu menu in UnityEngine.Object.FindObjectsByType<LevelSelectionMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (menu.gameObject.scene != scene)
                continue;

            entries.Add(new MenuEntry
            {
                menu = menu,
                source = menu.Source,
                name = menu.name,
                wasActive = menu.gameObject.activeSelf
            });
        }

        if (entries.Count != 3)
            throw new InvalidOperationException("Expected exactly three LevelSelectionMenu objects in Main, but found " + entries.Count + ".");

        return entries;
    }

    static GameObject CreatePanelPrefab(LevelSelectionMenu gameMenu, Transform modalMenus)
    {
        PrefabUtility.UnpackPrefabInstance(gameMenu.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        Transform legacyCanvas = gameMenu.transform.Find("Canvas");
        if (legacyCanvas == null || !(legacyCanvas is RectTransform))
            throw new InvalidOperationException("The Game level selection menu needs a direct Canvas child.");

        GameObject panel = legacyCanvas.gameObject;
        LevelSelectionMenu panelMenu = panel.AddComponent<LevelSelectionMenu>();
        EditorUtility.CopySerialized(gameMenu, panelMenu);

        RemoveLegacyCanvasComponents(panel);

        panel.name = "LevelSelectionMenuPanel";
        panel.transform.SetParent(modalMenus, false);
        ConfigurePanelTransform((RectTransform)panel.transform);
        panel.SetActive(false);

        UnityEngine.Object.DestroyImmediate(gameMenu.gameObject);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(panel, PanelPrefabPath);
        UnityEngine.Object.DestroyImmediate(panel);
        return prefab;
    }

    static void ConfigurePanelTransform(RectTransform panel)
    {
        panel.anchorMin = Vector2.zero;
        panel.anchorMax = Vector2.one;
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.localScale = Vector3.one;
    }

    static void RemoveLegacyCanvasComponents(GameObject panel)
    {
        // Canvas must be removed last because these two UI components depend on it.
        GraphicRaycaster raycaster = panel.GetComponent<GraphicRaycaster>();
        if (raycaster != null)
            UnityEngine.Object.DestroyImmediate(raycaster);

        CanvasScaler scaler = panel.GetComponent<CanvasScaler>();
        if (scaler != null)
            UnityEngine.Object.DestroyImmediate(scaler);

        Canvas canvas = panel.GetComponent<Canvas>();
        if (canvas != null)
            UnityEngine.Object.DestroyImmediate(canvas);
    }

    static void SetSource(LevelSelectionMenu menu, LevelSource source)
    {
        SerializedObject serializedMenu = new SerializedObject(menu);
        SerializedProperty sourceProperty = serializedMenu.FindProperty("source");
        if (sourceProperty == null)
            throw new InvalidOperationException("LevelSelectionMenu does not contain its source field.");

        sourceProperty.enumValueIndex = (int)source;
        serializedMenu.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetReference(Component owner, string propertyPath, UnityEngine.Object target)
    {
        SerializedObject serializedOwner = new SerializedObject(owner);
        SerializedProperty property = serializedOwner.FindProperty(propertyPath);
        if (property == null)
            throw new InvalidOperationException(owner.GetType().Name + " does not contain " + propertyPath + ".");

        property.objectReferenceValue = target;
        serializedOwner.ApplyModifiedPropertiesWithoutUndo();
    }
}
