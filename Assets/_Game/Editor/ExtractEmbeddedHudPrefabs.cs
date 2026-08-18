using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ExtractEmbeddedHudPrefabs
{
    const string PlayerPrefabPath = "Assets/_Game/Prefabs/OnlyOnePerLevel/Player.prefab";
    const string LevelEditorPrefabPath = "Assets/_Game/Prefabs/OnlyOnePerLevel/LevelEditor.prefab";
    const string PlayerHudPrefabPath = "Assets/_Game/Prefabs/UI/PlayerHUD.prefab";
    const string EditorHudPrefabPath = "Assets/_Game/Prefabs/UI/EditorHUD.prefab";

    sealed class ReferenceBinding
    {
        public string propertyPath;
        public string relativeTransformPath;
        public Type componentType;
        public int componentIndex;
    }

    [MenuItem("Orbit/UI/Extract Embedded HUD Prefabs")]
    static void Extract()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("HUD extraction unavailable", "Exit Play Mode before extracting the HUD prefabs.", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.path.EndsWith("/Main.unity", StringComparison.Ordinal))
        {
            EditorUtility.DisplayDialog("Open Main first", "Open the Main scene before extracting the HUD prefabs.", "OK");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerHudPrefabPath) != null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(EditorHudPrefabPath) != null)
        {
            EditorUtility.DisplayDialog("HUD prefabs already exist", "PlayerHUD.prefab or EditorHUD.prefab already exists. This one-time migration will not overwrite either file.", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog(
                "Extract HUD prefabs",
                "This creates PlayerHUD and EditorHUD prefabs, moves their scene instances under UIRoot, and removes the embedded Canvas children from Player and LevelEditor. Save or commit your work first.",
                "Extract",
                "Cancel"))
            return;

        try
        {
            EnsureHudPrefabFolder();

            Player player = UnityEngine.Object.FindFirstObjectByType<Player>(FindObjectsInactive.Include);
            LevelEditor levelEditor = UnityEngine.Object.FindFirstObjectByType<LevelEditor>(FindObjectsInactive.Include);
            UIManager uiManager = UnityEngine.Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
            GameObject uiRoot = GameObject.Find("UIRoot");

            if (player == null || levelEditor == null || uiManager == null || uiRoot == null)
                throw new InvalidOperationException("Main needs Player, LevelEditor, UIManager, and UIRoot before the HUDs can be extracted.");

            Transform playerCanvas = player.transform.Find("Canvas");
            Transform editorCanvas = levelEditor.transform.Find("Canvas");
            if (playerCanvas == null || editorCanvas == null)
                throw new InvalidOperationException("Player and LevelEditor must each still contain their embedded Canvas child.");

            List<ReferenceBinding> playerBindings = CaptureBindings(player, playerCanvas);
            List<ReferenceBinding> editorBindings = CaptureBindings(levelEditor, editorCanvas);

            ExtractHudPrefab<PlayerHUD>(PlayerPrefabPath, PlayerHudPrefabPath, "PlayerHUD", typeof(Player), typeof(GameManager));
            ExtractHudPrefab<EditorHUD>(LevelEditorPrefabPath, EditorHudPrefabPath, "EditorHUD", typeof(LevelEditor), typeof(GameManager));

            GameObject playerHud = InstantiateHud(PlayerHudPrefabPath, uiRoot.transform, scene);
            GameObject editorHud = InstantiateHud(EditorHudPrefabPath, uiRoot.transform, scene);

            RestoreBindings(player, playerBindings, playerHud.transform);
            RestoreBindings(levelEditor, editorBindings, editorHud.transform);
            playerHud.GetComponent<PlayerHUD>().Bind(player);
            editorHud.GetComponent<EditorHUD>().Bind(levelEditor);

            SetReference(uiManager, "playerHUD", playerHud);
            SetReference(uiManager, "levelEditorHUD", editorHud);

            if (playerCanvas != null)
                UnityEngine.Object.DestroyImmediate(playerCanvas.gameObject);
            if (editorCanvas != null)
                UnityEngine.Object.DestroyImmediate(editorCanvas.gameObject);

            EditorUtility.SetDirty(playerHud);
            EditorUtility.SetDirty(editorHud);
            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(levelEditor);
            EditorUtility.SetDirty(uiManager);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);

            EditorUtility.DisplayDialog("HUD extraction complete", "PlayerHUD and EditorHUD are now separate prefabs under UIRoot. Run the game and test their buttons before making further UI changes.", "OK");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("HUD extraction stopped", "Nothing else was attempted after the error. Check the Console and use version control to restore any partial asset changes before trying again.", "OK");
        }
    }

    static void ExtractHudPrefab<THud>(string ownerPrefabPath, string hudPrefabPath, string hudName, params Type[] externalTargetTypes)
        where THud : Component
    {
        GameObject ownerRoot = PrefabUtility.LoadPrefabContents(ownerPrefabPath);
        try
        {
            Transform canvas = ownerRoot.transform.Find("Canvas");
            if (canvas == null)
                throw new InvalidOperationException(ownerPrefabPath + " does not contain a direct Canvas child.");

            canvas.SetParent(null, false);
            canvas.name = hudName;
            // The Player HUD used a zero-scale Canvas while it lived under the Player.
            // As a child of UIRoot it must use normal UI scale instead.
            canvas.localScale = Vector3.one;
            THud hud = canvas.gameObject.AddComponent<THud>();

            ReplaceExternalEventTargets(canvas, hud, externalTargetTypes);
            Component owner = ownerRoot.GetComponent(externalTargetTypes[0]);
            if (owner == null)
                throw new InvalidOperationException(ownerPrefabPath + " does not contain " + externalTargetTypes[0].Name + ".");

            ClearReferencesIntoHierarchy(owner, canvas);

            PrefabUtility.SaveAsPrefabAsset(canvas.gameObject, hudPrefabPath);
            PrefabUtility.SaveAsPrefabAsset(ownerRoot, ownerPrefabPath);
            UnityEngine.Object.DestroyImmediate(canvas.gameObject);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(ownerRoot);
        }
    }

    static GameObject InstantiateHud(string prefabPath, Transform parent, Scene scene)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.transform.SetParent(parent, false);
        return instance;
    }

    static void EnsureHudPrefabFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Game/Prefabs/UI"))
            AssetDatabase.CreateFolder("Assets/_Game/Prefabs", "UI");
    }

    static List<ReferenceBinding> CaptureBindings(Component owner, Transform oldHudRoot)
    {
        List<ReferenceBinding> bindings = new List<ReferenceBinding>();
        SerializedObject serializedOwner = new SerializedObject(owner);
        SerializedProperty property = serializedOwner.GetIterator();

        while (property.NextVisible(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference ||
                !TryGetReferencedGameObject(property.objectReferenceValue, out GameObject referencedGameObject) ||
                !IsInHierarchy(referencedGameObject.transform, oldHudRoot))
                continue;

            Component referencedComponent = property.objectReferenceValue as Component;
            int componentIndex = 0;
            Type componentType = null;
            if (referencedComponent != null)
            {
                componentType = referencedComponent.GetType();
                Component[] matchingComponents = referencedGameObject.GetComponents(componentType);
                for (int index = 0; index < matchingComponents.Length; index++)
                {
                    if (matchingComponents[index] == referencedComponent)
                    {
                        componentIndex = index;
                        break;
                    }
                }
            }

            bindings.Add(new ReferenceBinding
            {
                propertyPath = property.propertyPath,
                relativeTransformPath = referencedGameObject.transform == oldHudRoot
                    ? string.Empty
                    : AnimationUtility.CalculateTransformPath(referencedGameObject.transform, oldHudRoot),
                componentType = componentType,
                componentIndex = componentIndex
            });
        }

        return bindings;
    }

    static void RestoreBindings(Component owner, IEnumerable<ReferenceBinding> bindings, Transform newHudRoot)
    {
        SerializedObject serializedOwner = new SerializedObject(owner);
        foreach (ReferenceBinding binding in bindings)
        {
            Transform targetTransform = string.IsNullOrEmpty(binding.relativeTransformPath)
                ? newHudRoot
                : newHudRoot.Find(binding.relativeTransformPath);
            if (targetTransform == null)
                throw new InvalidOperationException("Could not find " + binding.relativeTransformPath + " in " + newHudRoot.name + ".");

            UnityEngine.Object target = targetTransform.gameObject;
            if (binding.componentType != null)
            {
                Component[] matchingComponents = targetTransform.GetComponents(binding.componentType);
                if (binding.componentIndex >= matchingComponents.Length)
                    throw new InvalidOperationException("Could not restore a " + binding.componentType.Name + " reference in " + newHudRoot.name + ".");

                target = matchingComponents[binding.componentIndex];
            }

            SerializedProperty property = serializedOwner.FindProperty(binding.propertyPath);
            if (property == null)
                throw new InvalidOperationException("Could not restore " + binding.propertyPath + " on " + owner.name + ".");

            property.objectReferenceValue = target;
        }

        serializedOwner.ApplyModifiedPropertiesWithoutUndo();
    }

    static void ClearReferencesIntoHierarchy(Component owner, Transform detachedHudRoot)
    {
        SerializedObject serializedOwner = new SerializedObject(owner);
        SerializedProperty property = serializedOwner.GetIterator();
        while (property.NextVisible(true))
        {
            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                TryGetReferencedGameObject(property.objectReferenceValue, out GameObject referencedGameObject) &&
                IsInHierarchy(referencedGameObject.transform, detachedHudRoot))
                property.objectReferenceValue = null;
        }

        serializedOwner.ApplyModifiedPropertiesWithoutUndo();
    }

    static void ReplaceExternalEventTargets(Transform hudRoot, Component hud, IReadOnlyCollection<Type> externalTargetTypes)
    {
        foreach (Component component in hudRoot.GetComponentsInChildren<Component>(true))
        {
            if (component == null)
                continue;

            SerializedObject serializedComponent = new SerializedObject(component);
            SerializedProperty property = serializedComponent.GetIterator();
            bool didChange = false;

            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference ||
                    !property.propertyPath.EndsWith("m_Target", StringComparison.Ordinal) ||
                    !(property.objectReferenceValue is Component targetComponent) ||
                    !IsExternalTargetType(targetComponent.GetType(), externalTargetTypes))
                    continue;

                property.objectReferenceValue = hud;
                int targetNameIndex = property.propertyPath.LastIndexOf("m_Target", StringComparison.Ordinal);
                SerializedProperty typeNameProperty = serializedComponent.FindProperty(
                    property.propertyPath.Substring(0, targetNameIndex) + "m_TargetAssemblyTypeName");
                if (typeNameProperty != null)
                    typeNameProperty.stringValue = hud.GetType().FullName + ", " + hud.GetType().Assembly.GetName().Name;

                didChange = true;
            }

            if (didChange)
                serializedComponent.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static bool IsExternalTargetType(Type targetType, IEnumerable<Type> externalTargetTypes)
    {
        foreach (Type externalTargetType in externalTargetTypes)
        {
            if (externalTargetType.IsAssignableFrom(targetType))
                return true;
        }

        return false;
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

    static bool TryGetReferencedGameObject(UnityEngine.Object reference, out GameObject gameObject)
    {
        switch (reference)
        {
            case GameObject referencedGameObject:
                gameObject = referencedGameObject;
                return true;
            case Component referencedComponent:
                gameObject = referencedComponent.gameObject;
                return true;
            default:
                gameObject = null;
                return false;
        }
    }

    static bool IsInHierarchy(Transform candidate, Transform root)
    {
        return candidate == root || candidate.IsChildOf(root);
    }
}
