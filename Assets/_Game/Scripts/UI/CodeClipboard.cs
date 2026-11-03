using System;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

// Clipboard access for code panels; browser operations complete asynchronously.
public class CodeClipboard : MonoBehaviour
{
    int readRequest;
    int writeRequest;
    Action<string> onRead;
    Action onReadFailed;
    Action onWrite;
    Action onWriteFailed;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void ReadCodeClipboard(string target, int request);
    [DllImport("__Internal")] static extern void WriteCodeClipboard(string target, int request, string text);
    [DllImport("__Internal")] static extern void CancelCodeClipboard(string target);

    void Awake() => gameObject.name += "_" + Guid.NewGuid().ToString("N");
#endif

    public void Read(Action<string> success, Action failure)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        onRead = success;
        onReadFailed = failure;
        try { ReadCodeClipboard(gameObject.name, ++readRequest); }
        catch (Exception exception)
        {
            Debug.LogWarning("Clipboard could not be read: " + exception.Message);
            onRead = null;
            onReadFailed = null;
            failure?.Invoke();
        }
#else
        string text;
        try { text = GUIUtility.systemCopyBuffer; }
        catch (Exception exception)
        {
            Debug.LogWarning("Clipboard could not be read: " + exception.Message);
            failure?.Invoke();
            return;
        }
        success?.Invoke(text);
#endif
    }

    public void Write(string text, Action success, Action failure)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        onWrite = success;
        onWriteFailed = failure;
        try { WriteCodeClipboard(gameObject.name, ++writeRequest, text); }
        catch (Exception exception)
        {
            Debug.LogWarning("Code could not be copied: " + exception.Message);
            onWrite = null;
            onWriteFailed = null;
            failure?.Invoke();
        }
#else
        try { GUIUtility.systemCopyBuffer = text; }
        catch (Exception exception)
        {
            Debug.LogWarning("Code could not be copied: " + exception.Message);
            failure?.Invoke();
            return;
        }
        success?.Invoke();
#endif
    }

    public void ReceiveCodeClipboardRead(string result)
    {
        if (!TryGetResult(result, readRequest, out string value)) return;
        Action<string> success = onRead;
        Action failure = onReadFailed;
        onRead = null;
        onReadFailed = null;
        if (value.StartsWith("ok:", StringComparison.Ordinal)) success?.Invoke(value.Substring(3));
        else failure?.Invoke();
    }

    public void ReceiveCodeClipboardWrite(string result)
    {
        if (!TryGetResult(result, writeRequest, out string value)) return;
        Action success = onWrite;
        Action failure = onWriteFailed;
        onWrite = null;
        onWriteFailed = null;
        if (value == "ok") success?.Invoke();
        else failure?.Invoke();
    }

    static bool TryGetResult(string result, int expectedRequest, out string value)
    {
        value = null;
        if (result == null) return false;
        int separator = result.IndexOf('\n');
        if (separator < 0 || !int.TryParse(result.Substring(0, separator), out int request) || request != expectedRequest)
            return false;
        value = result.Substring(separator + 1);
        return true;
    }

    void OnDisable()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        CancelCodeClipboard(gameObject.name);
#endif
        ++readRequest;
        ++writeRequest;
        onRead = null;
        onReadFailed = null;
        onWrite = null;
        onWriteFailed = null;
    }
}
