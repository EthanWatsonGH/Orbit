using System;
using UnityEngine;

// A shared parser for level codes, prefab codes, and stored content files.
public static class ContentSerialization
{
    [Serializable]
    struct Header { public byte loaderVersion; }

    public static bool TryRead(string json, ContentKind expectedKind, Func<string, bool> isSupportedType, out ContentData data)
    {
        data = null;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            Header header = JsonUtility.FromJson<Header>(json);
            switch (header.loaderVersion)
            {
                case 1: data = JsonUtility.FromJson<ContentData>(json); break;
                default: Debug.LogError("Content loader version " + header.loaderVersion + " is not supported."); return false;
            }
            if (data == null || data.kind != expectedKind || !Validate(data, isSupportedType)) { data = null; return false; }
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError("Content could not be read: " + exception.Message);
            data = null;
            return false;
        }
    }

    public static bool Validate(ContentData data, Func<string, bool> isSupportedType)
    {
        if (data == null || data.objects == null || isSupportedType == null ||
            !IsFinite(data.startX) || !IsFinite(data.startY)) return false;
        if (data.kind != ContentKind.Level && data.kind != ContentKind.Prefab) return false;
        if (data.kind == ContentKind.Prefab)
        {
            if (data.objects.Count == 0 || data.objects[0].p != -1) return false;
            bool group = data.objects[0].t == LevelManager.GroupObjectType;
            if ((group && data.objects.Count < 3) || (!group && data.objects.Count != 1)) return false;
        }
        int[] childCounts = new int[data.objects.Count];
        for (int index = 0; index < data.objects.Count; index++)
        {
            LevelObjectV1 item = data.objects[index];
            if (!isSupportedType(item.t) || item.p < -1 || item.p >= index ||
                (item.p >= 0 && data.objects[item.p].t != LevelManager.GroupObjectType) ||
                (item.p >= 0 && item.t == LevelManager.GroupObjectType) ||
                (data.kind == ContentKind.Prefab && index > 0 && (item.p != 0 || item.t == LevelManager.GroupObjectType)) ||
                !IsFinite(item.x) || !IsFinite(item.y) || !IsFinite(item.sx) || !IsFinite(item.sy) || !IsFinite(item.r)) return false;
            if (item.p >= 0) childCounts[item.p]++;
        }
        for (int index = 0; index < data.objects.Count; index++)
            if (data.objects[index].t == LevelManager.GroupObjectType && childCounts[index] < 2) return false;
        return true;
    }
    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
