using System;
using System.Collections.Generic;
using UnityEngine;

// Version 1 object records are shared by levels and saved object collections.
// Parents precede their children; -1 means the supplied root transform.
[Serializable]
public struct LevelObjectV1
{
    public string t; // Prefab name or "Group".
    public int p; // Parent index.
    public float x; // Local X position.
    public float y; // Local Y position.
    public float sx; // Local X scale.
    public float sy; // Local Y scale.
    public float r; // Local Z rotation.
}

public static class LevelObjectSerialization
{
    public static bool TryCaptureChildren(Transform parent, Func<string, bool> isSupportedType, out List<LevelObjectV1> objects)
    {
        objects = new List<LevelObjectV1>();
        if (AppendChildren(parent, -1, isSupportedType, objects))
            return true;

        objects = null;
        return false;
    }

    static bool AppendChildren(Transform parent, int parentIndex, Func<string, bool> isSupportedType, List<LevelObjectV1> objects)
    {
        foreach (Transform child in parent)
        {
            string type = GetObjectType(child);
            if (!isSupportedType(type))
            {
                Debug.LogError("Objects could not be saved because '" + child.name + "' is not a recognized level object or Group.", child);
                return false;
            }

            int index = objects.Count;
            objects.Add(new LevelObjectV1
            {
                t = type,
                p = parentIndex,
                x = child.localPosition.x,
                y = child.localPosition.y,
                sx = child.localScale.x,
                sy = child.localScale.y,
                r = child.localEulerAngles.z
            });

            if (!AppendChildren(child, index, isSupportedType, objects))
                return false;
        }

        return true;
    }

    static string GetObjectType(Transform objectTransform)
    {
        const string cloneSuffix = "(Clone)";
        string type = objectTransform.name.Trim();
        if (type.EndsWith(cloneSuffix, StringComparison.Ordinal))
            type = type.Substring(0, type.Length - cloneSuffix.Length).TrimEnd();
        return type;
    }

    // The returned list uses the same indices as the records, including nulls for
    // objects that could not be created, so their descendants cannot attach elsewhere.
    public static List<Transform> InstantiateObjects(IReadOnlyList<LevelObjectV1> objects, Transform root,
        Func<string, Transform, GameObject> createObject)
    {
        List<Transform> created = new List<Transform>();
        if (objects == null)
            return created;

        foreach (LevelObjectV1 data in objects)
        {
            Transform parent = root;
            if (data.p != -1)
            {
                if (data.p < 0 || data.p >= created.Count)
                {
                    Debug.LogError("Version 1 object '" + data.t + "' has an invalid parent index.");
                    created.Add(null);
                    continue;
                }

                parent = created[data.p];
                if (parent == null)
                {
                    Debug.LogError("Version 1 object '" + data.t + "' cannot load because its parent could not be created.");
                    created.Add(null);
                    continue;
                }
            }

            GameObject instance = createObject(data.t, parent);
            if (instance == null)
            {
                created.Add(null);
                continue;
            }

            instance.transform.localPosition = new Vector3(data.x, data.y, 0f);
            instance.transform.localRotation = Quaternion.Euler(0f, 0f, data.r);
            instance.transform.localScale = new Vector3(data.sx, data.sy, 1f);
            created.Add(instance.transform);
        }

        return created;
    }
}
