using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class ScaleFromEdgeControlsUI : MonoBehaviour
{
    [SerializeField] RectTransform controlsRoot;
    [SerializeField] CanvasGroup controlsCanvasGroup;
    [SerializeField, Min(0f)] float minimumHandleSpacingPixels = 72f;

    LevelEditor levelEditor;
    RectTransform overlayRect;
    Canvas parentCanvas;
    bool hasSelectionFrame;
    LevelEditor.ScaleFromEdgeFrame selectionFrame;
    readonly Dictionary<ScaleFromEdgeHandle, Quaternion> handleBaseRotations = new Dictionary<ScaleFromEdgeHandle, Quaternion>();

    void Awake()
    {
        if (controlsRoot == null)
            controlsRoot = transform as RectTransform;
        if (controlsCanvasGroup == null)
            controlsCanvasGroup = GetComponent<CanvasGroup>();
        if (controlsCanvasGroup == null)
            controlsCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        foreach (ScaleFromEdgeHandle handle in System.Enum.GetValues(typeof(ScaleFromEdgeHandle)))
        {
            RectTransform handleTransform = FindHandle(handle);
            if (handleTransform != null)
                handleBaseRotations[handle] = handleTransform.localRotation;
        }

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
        float selectionRotationDegrees = Mathf.Atan2(selectionFrame.right.y, selectionFrame.right.x) * Mathf.Rad2Deg;
        Quaternion selectionRotation = Quaternion.Euler(0f, 0f, selectionRotationDegrees);
        Vector2 centerScreenPosition2D = centerScreenPosition;
        Vector2 rightScreenOffset = (Vector2)activeCamera.WorldToScreenPoint(selectionFrame.GetHandlePosition(ScaleFromEdgeHandle.Right)) - centerScreenPosition2D;
        Vector2 upScreenOffset = (Vector2)activeCamera.WorldToScreenPoint(selectionFrame.GetHandlePosition(ScaleFromEdgeHandle.Up)) - centerScreenPosition2D;
        Vector2 rightScreenDirection = GetScreenDirection(rightScreenOffset, selectionFrame.right);
        Vector2 upScreenDirection = GetScreenDirection(upScreenOffset, selectionFrame.up);
        float displayedHalfWidth = Mathf.Max(rightScreenOffset.magnitude, minimumHandleSpacingPixels);
        float displayedHalfHeight = Mathf.Max(upScreenOffset.magnitude, minimumHandleSpacingPixels);

        foreach (ScaleFromEdgeHandle handle in System.Enum.GetValues(typeof(ScaleFromEdgeHandle)))
        {
            RectTransform handleTransform = FindHandle(handle);
            if (handleTransform == null)
                continue;

            if (handleBaseRotations.TryGetValue(handle, out Quaternion baseRotation))
                handleTransform.localRotation = selectionRotation * baseRotation;

            Vector2 handleScreenPosition = GetDisplayedHandleScreenPosition(
                handle,
                centerScreenPosition2D,
                rightScreenDirection,
                upScreenDirection,
                displayedHalfWidth,
                displayedHalfHeight);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect, handleScreenPosition, eventCamera, out Vector2 handleLocalPosition))
                handleTransform.anchoredPosition = handleLocalPosition - centerLocalPosition;
        }
    }

    static Vector2 GetScreenDirection(Vector2 screenOffset, Vector3 worldFallbackDirection)
    {
        if (screenOffset.sqrMagnitude > Mathf.Epsilon)
            return screenOffset.normalized;

        Vector2 fallbackDirection = new Vector2(worldFallbackDirection.x, worldFallbackDirection.y);
        return fallbackDirection.sqrMagnitude > Mathf.Epsilon ? fallbackDirection.normalized : Vector2.right;
    }

    static Vector2 GetDisplayedHandleScreenPosition(
        ScaleFromEdgeHandle handle,
        Vector2 centerScreenPosition,
        Vector2 rightScreenDirection,
        Vector2 upScreenDirection,
        float halfWidth,
        float halfHeight)
    {
        return handle switch
        {
            ScaleFromEdgeHandle.Left => centerScreenPosition - rightScreenDirection * halfWidth,
            ScaleFromEdgeHandle.Right => centerScreenPosition + rightScreenDirection * halfWidth,
            ScaleFromEdgeHandle.Up => centerScreenPosition + upScreenDirection * halfHeight,
            ScaleFromEdgeHandle.Down => centerScreenPosition - upScreenDirection * halfHeight,
            ScaleFromEdgeHandle.UpLeft => centerScreenPosition - rightScreenDirection * halfWidth + upScreenDirection * halfHeight,
            ScaleFromEdgeHandle.UpRight => centerScreenPosition + rightScreenDirection * halfWidth + upScreenDirection * halfHeight,
            ScaleFromEdgeHandle.DownLeft => centerScreenPosition - rightScreenDirection * halfWidth - upScreenDirection * halfHeight,
            ScaleFromEdgeHandle.DownRight => centerScreenPosition + rightScreenDirection * halfWidth - upScreenDirection * halfHeight,
            _ => centerScreenPosition
        };
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
