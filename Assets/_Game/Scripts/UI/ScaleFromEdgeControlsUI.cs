using UnityEngine;

[DefaultExecutionOrder(100)]
public class ScaleFromEdgeControlsUI : MonoBehaviour
{
    [SerializeField] RectTransform controlsRoot;
    [SerializeField] CanvasGroup controlsCanvasGroup;

    LevelEditor levelEditor;
    RectTransform overlayRect;
    Canvas parentCanvas;
    bool hasSelectionFrame;
    LevelEditor.ScaleFromEdgeFrame selectionFrame;

    void Awake()
    {
        if (controlsRoot == null)
            controlsRoot = transform as RectTransform;
        if (controlsCanvasGroup == null)
            controlsCanvasGroup = GetComponent<CanvasGroup>();
        if (controlsCanvasGroup == null)
            controlsCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        overlayRect = transform.parent as RectTransform;
        parentCanvas = GetComponentInParent<Canvas>();
        SetVisible(false);
    }

    public void Initialize(LevelEditor editor)
    {
        levelEditor = editor;
    }

    public void SetSelectionFrame(bool hasFrame, LevelEditor.ScaleFromEdgeFrame newSelectionFrame)
    {
        hasSelectionFrame = hasFrame;
        selectionFrame = newSelectionFrame;
    }

    public void SetControlAvailability(bool canScaleHorizontally, bool canScaleVertically, bool canScaleUniformly)
    {
        SetHandleVisible(ScaleFromEdgeHandle.Left, canScaleHorizontally);
        SetHandleVisible(ScaleFromEdgeHandle.Right, canScaleHorizontally);
        SetHandleVisible(ScaleFromEdgeHandle.Up, canScaleVertically);
        SetHandleVisible(ScaleFromEdgeHandle.Down, canScaleVertically);

        SetHandleVisible(ScaleFromEdgeHandle.UpLeft, canScaleUniformly);
        SetHandleVisible(ScaleFromEdgeHandle.UpRight, canScaleUniformly);
        SetHandleVisible(ScaleFromEdgeHandle.DownLeft, canScaleUniformly);
        SetHandleVisible(ScaleFromEdgeHandle.DownRight, canScaleUniformly);
    }

    public void SetVisible(bool shouldShow)
    {
        if (gameObject.activeSelf != shouldShow)
            gameObject.SetActive(shouldShow);
    }

    public void SetControlsVisible(bool shouldShow)
    {
        controlsCanvasGroup.alpha = shouldShow ? 1f : 0f;
        controlsCanvasGroup.interactable = shouldShow;
        controlsCanvasGroup.blocksRaycasts = shouldShow;
    }

    public void BeginControl(ScaleFromEdgeHandle handle, int pointerId, Vector2 screenPosition)
    {
        if (levelEditor != null)
            levelEditor.BeginScaleFromEdgeControl(handle, pointerId, screenPosition);
    }

    public void EndControl(ScaleFromEdgeHandle handle, int pointerId)
    {
        if (levelEditor != null)
            levelEditor.EndScaleFromEdgeControl(handle, pointerId);
    }

    void LateUpdate()
    {
        if (!hasSelectionFrame || controlsRoot == null || overlayRect == null || parentCanvas == null)
            return;

        Camera activeCamera = Camera.main;
        if (activeCamera == null)
            return;

        Vector3 centerScreenPosition = activeCamera.WorldToScreenPoint(selectionFrame.Center);
        if (centerScreenPosition.z <= 0f)
        {
            SetVisible(false);
            return;
        }

        Camera eventCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect, centerScreenPosition, eventCamera, out Vector2 centerLocalPosition))
            return;

        controlsRoot.anchoredPosition = centerLocalPosition;
        foreach (ScaleFromEdgeHandle handle in System.Enum.GetValues(typeof(ScaleFromEdgeHandle)))
        {
            RectTransform handleTransform = FindHandle(handle);
            if (handleTransform == null)
                continue;

            Vector3 handleScreenPosition = activeCamera.WorldToScreenPoint(selectionFrame.GetHandlePosition(handle));
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect, handleScreenPosition, eventCamera, out Vector2 handleLocalPosition))
                handleTransform.anchoredPosition = handleLocalPosition - centerLocalPosition;
        }
    }

    void SetHandleVisible(ScaleFromEdgeHandle handle, bool shouldShow)
    {
        RectTransform handleTransform = FindHandle(handle);
        if (handleTransform != null && handleTransform.gameObject.activeSelf != shouldShow)
            handleTransform.gameObject.SetActive(shouldShow);
    }

    RectTransform FindHandle(ScaleFromEdgeHandle handle)
    {
        if (controlsRoot == null)
            return null;

        Transform handleTransform = controlsRoot.Find(handle.ToString());
        return handleTransform as RectTransform;
    }
}
