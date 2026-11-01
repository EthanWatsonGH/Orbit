using System.Collections.Generic;
using UnityEngine;

// Positions all selection controls together in screen space.
[DefaultExecutionOrder(100)]
public class SelectionControlsUI : MonoBehaviour
{
    [SerializeField] RectTransform controlsRoot;
    [SerializeField] CanvasGroup controlsCanvasGroup;
    [SerializeField] GameObject duplicateControl;
    [SerializeField] GameObject rotateControl;
    [SerializeField, Min(0f)] float minimumHandleSpacingPixels = 72f;
    [SerializeField, Min(0f)] float controlPadding = 12f;

    LevelEditor levelEditor;
    Transform selectedTransform;
    RectTransform overlayRect;
    Canvas parentCanvas;
    RectTransform moveTransform;
    RectTransform rotateTransform;
    RectTransform duplicateTransform;
    bool hasSelectionFrame;
    LevelEditor.ScaleFromEdgeFrame selectionFrame;
    readonly Dictionary<ScaleFromEdgeHandle, HandleView> handleViews = new Dictionary<ScaleFromEdgeHandle, HandleView>();
    readonly List<HandleMeasurements> handleMeasurements = new List<HandleMeasurements>();
    readonly Vector3[] corners = new Vector3[4];

    struct HandleView
    {
        public RectTransform transform;
        public Quaternion baseLocalRotation;
    }

    struct HandleMeasurements
    {
        public ScaleFromEdgeHandle handle;
        public Vector2 gridPosition;
        public float radius;
    }

    void Awake() => EnsureReferences();

    public void Initialize(LevelEditor editor)
    {
        levelEditor = editor;
        EnsureReferences();
    }

    void EnsureReferences()
    {
        if (controlsRoot == null)
            controlsRoot = transform as RectTransform;
        if (controlsCanvasGroup == null)
            controlsCanvasGroup = GetComponent<CanvasGroup>();
        if (overlayRect == null)
            overlayRect = transform.parent as RectTransform;
        if (parentCanvas == null)
            parentCanvas = GetComponentInParent<Canvas>(true);
        if (controlsRoot == null)
            return;

        if (moveTransform == null)
            moveTransform = controlsRoot.Find("MoveBoth") as RectTransform;
        if (rotateTransform == null && rotateControl != null)
            rotateTransform = rotateControl.transform as RectTransform;
        if (duplicateTransform == null && duplicateControl != null)
            duplicateTransform = duplicateControl.transform as RectTransform;

        if (handleViews.Count == 0)
        {
            foreach (ScaleFromEdgeHandle handle in System.Enum.GetValues(typeof(ScaleFromEdgeHandle)))
            {
                if (controlsRoot.Find(handle.ToString()) is RectTransform handleTransform)
                    handleViews[handle] = new HandleView { transform = handleTransform, baseLocalRotation = handleTransform.localRotation };
            }
        }
    }

    public void SetSelectedTransform(Transform selected) => selectedTransform = selected;

    public void SetSelectionFrame(bool hasFrame, LevelEditor.ScaleFromEdgeFrame frame)
    {
        hasSelectionFrame = hasFrame;
        selectionFrame = frame;
    }

    public void SetControlAvailability(bool canDuplicate, bool canRotate, bool canScaleHorizontally, bool canScaleVertically, bool canScaleUniformly)
    {
        EnsureReferences();
        if (duplicateControl != null)
            duplicateControl.SetActive(canDuplicate);
        if (rotateControl != null)
            rotateControl.SetActive(canRotate);
        SetHandleVisible(ScaleFromEdgeHandle.Left, canScaleHorizontally);
        SetHandleVisible(ScaleFromEdgeHandle.Right, canScaleHorizontally);
        SetHandleVisible(ScaleFromEdgeHandle.Up, canScaleVertically);
        SetHandleVisible(ScaleFromEdgeHandle.Down, canScaleVertically);
        SetHandleVisible(ScaleFromEdgeHandle.UpLeft, canScaleUniformly);
        SetHandleVisible(ScaleFromEdgeHandle.UpRight, canScaleUniformly);
        SetHandleVisible(ScaleFromEdgeHandle.DownLeft, canScaleUniformly);
        SetHandleVisible(ScaleFromEdgeHandle.DownRight, canScaleUniformly);
    }

    void SetHandleVisible(ScaleFromEdgeHandle handle, bool visible)
    {
        if (handleViews.TryGetValue(handle, out HandleView view) && view.transform.gameObject.activeSelf != visible)
            view.transform.gameObject.SetActive(visible);
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }

    public void SetControlsVisible(bool visible)
    {
        EnsureReferences();
        if (controlsCanvasGroup == null)
            return;
        // Keep the object active so a drag still receives its matching pointer-up.
        controlsCanvasGroup.alpha = visible ? 1f : 0f;
        controlsCanvasGroup.interactable = visible;
        controlsCanvasGroup.blocksRaycasts = visible;
    }

    public void BeginControl(ObjectTransformControl control, int pointerId, Vector2 screenPosition) =>
        levelEditor?.BeginObjectTransformControl(control, pointerId, screenPosition);
    public void EndControl(ObjectTransformControl control, int pointerId) => levelEditor?.EndObjectTransformControl(control, pointerId);
    public void BeginControl(ScaleFromEdgeHandle handle, int pointerId, Vector2 screenPosition) =>
        levelEditor?.BeginScaleFromEdgeControl(handle, pointerId, screenPosition);
    public void EndControl(ScaleFromEdgeHandle handle, int pointerId) => levelEditor?.EndScaleFromEdgeControl(handle, pointerId);

    void LateUpdate()
    {
        EnsureReferences();
        if (selectedTransform == null || controlsRoot == null || overlayRect == null || parentCanvas == null)
            return;
        Camera camera = Camera.main;
        if (camera == null)
            return;

        Vector3 centerWorld = hasSelectionFrame ? selectionFrame.Center : selectedTransform.position;
        Vector3 centerScreen = camera.WorldToScreenPoint(centerWorld);
        if (centerScreen.z <= 0f)
            return;
        Camera eventCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect, centerScreen, eventCamera, out Vector2 centerLocal))
            return;
        controlsRoot.anchoredPosition = centerLocal;

        float rightmost = centerScreen.x;
        float padding = controlPadding * parentCanvas.scaleFactor;
        if (hasSelectionFrame)
        {
            Vector2 center = centerScreen;
            Vector2 rightOffset = (Vector2)camera.WorldToScreenPoint(selectionFrame.GetHandlePosition(ScaleFromEdgeHandle.Right)) - center;
            Vector2 upOffset = (Vector2)camera.WorldToScreenPoint(selectionFrame.GetHandlePosition(ScaleFromEdgeHandle.Up)) - center;
            Vector2 rightDirection = GetScreenDirection(rightOffset, selectionFrame.right);
            Vector2 upDirection = GetScreenDirection(upOffset, selectionFrame.up);
            Quaternion rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(selectionFrame.right.y, selectionFrame.right.x) * Mathf.Rad2Deg);
            handleMeasurements.Clear();
            foreach (KeyValuePair<ScaleFromEdgeHandle, HandleView> entry in handleViews)
            {
                HandleView view = entry.Value;
                view.transform.localRotation = rotation * view.baseLocalRotation;
                if (view.transform.gameObject.activeSelf)
                {
                    handleMeasurements.Add(new HandleMeasurements
                    {
                        handle = entry.Key,
                        gridPosition = GetHandleScreenPosition(entry.Key, Vector2.zero, Vector2.right, Vector2.up, 1f, 1f),
                        radius = GetCircleScreenRadius(view.transform, eventCamera)
                    });
                }
            }
            float halfWidth = Mathf.Max(rightOffset.magnitude, minimumHandleSpacingPixels);
            float halfHeight = Mathf.Max(upOffset.magnitude, minimumHandleSpacingPixels);
            for (int i = 0; i < handleMeasurements.Count; i++)
            {
                HandleMeasurements a = handleMeasurements[i];
                for (int j = i + 1; j < handleMeasurements.Count; j++)
                {
                    HandleMeasurements b = handleMeasurements[j];
                    float dx = Mathf.Abs(a.gridPosition.x - b.gridPosition.x);
                    float dy = Mathf.Abs(a.gridPosition.y - b.gridPosition.y);
                    if (dx > 0f)
                        halfWidth = Mathf.Max(halfWidth, (a.radius + b.radius + padding) / dx);
                    if (dy > 0f)
                        halfHeight = Mathf.Max(halfHeight, (a.radius + b.radius + padding) / dy);
                }
            }
            rightmost = center.x + Mathf.Abs(rightOffset.x) + Mathf.Abs(upOffset.x);

            foreach (KeyValuePair<ScaleFromEdgeHandle, HandleView> entry in handleViews)
            {
                Vector2 position = GetHandleScreenPosition(entry.Key, center, rightDirection, upDirection, halfWidth, halfHeight);
                SetScreenPosition(entry.Value.transform, position, centerLocal, eventCamera);
            }
            foreach (HandleMeasurements handle in handleMeasurements)
            {
                Vector2 position = GetHandleScreenPosition(handle.handle, center, rightDirection, upDirection, halfWidth, halfHeight);
                rightmost = Mathf.Max(rightmost, position.x + handle.radius);
            }
        }

        // Keep fixed slots in a screen-upright column, always to the right of the handles.
        Vector2 moveSize = GetScreenHalfSize(moveTransform, eventCamera);
        Vector2 rotateSize = GetScreenHalfSize(rotateTransform, eventCamera);
        Vector2 duplicateSize = GetScreenHalfSize(duplicateTransform, eventCamera);
        float actionHalfWidth = Mathf.Max(moveSize.x, Mathf.Max(rotateSize.x, duplicateSize.x));
        float actionHalfHeight = Mathf.Max(moveSize.y, Mathf.Max(rotateSize.y, duplicateSize.y));
        float columnX = rightmost + actionHalfWidth + padding;
        float step = 2f * actionHalfHeight + padding;
        SetScreenPosition(rotateTransform, new Vector2(columnX, centerScreen.y + step), centerLocal, eventCamera);
        SetScreenPosition(moveTransform, new Vector2(columnX, centerScreen.y), centerLocal, eventCamera);
        SetScreenPosition(duplicateTransform, new Vector2(columnX, centerScreen.y - step), centerLocal, eventCamera);
    }

    float GetCircleScreenRadius(RectTransform target, Camera eventCamera)
    {
        if (target == null)
            return 0f;
        target.GetWorldCorners(corners);
        // The icons are circular, so rotating their square RectTransforms must not
        // increase the space reserved around the visible circle.
        Vector2 a = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]);
        Vector2 b = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[1]);
        Vector2 c = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]);
        return Mathf.Max(Vector2.Distance(a, b), Vector2.Distance(b, c)) * 0.5f;
    }

    Vector2 GetScreenHalfSize(RectTransform target, Camera eventCamera)
    {
        if (target == null)
            return Vector2.zero;
        target.GetWorldCorners(corners);
        Vector2 center = RectTransformUtility.WorldToScreenPoint(eventCamera, target.TransformPoint(target.rect.center));
        Vector2 halfSize = Vector2.zero;
        foreach (Vector3 corner in corners)
        {
            Vector2 delta = RectTransformUtility.WorldToScreenPoint(eventCamera, corner) - center;
            halfSize.x = Mathf.Max(halfSize.x, Mathf.Abs(delta.x));
            halfSize.y = Mathf.Max(halfSize.y, Mathf.Abs(delta.y));
        }
        return halfSize;
    }

    void SetScreenPosition(RectTransform target, Vector2 screenPosition, Vector2 centerLocal, Camera eventCamera)
    {
        if (target != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect, screenPosition, eventCamera, out Vector2 local))
            target.anchoredPosition = local - centerLocal;
    }

    static Vector2 GetScreenDirection(Vector2 screenOffset, Vector3 worldFallback)
    {
        if (screenOffset.sqrMagnitude > Mathf.Epsilon)
            return screenOffset.normalized;
        Vector2 fallback = new Vector2(worldFallback.x, worldFallback.y);
        return fallback.sqrMagnitude > Mathf.Epsilon ? fallback.normalized : Vector2.right;
    }

    static Vector2 GetHandleScreenPosition(ScaleFromEdgeHandle handle, Vector2 center, Vector2 right, Vector2 up, float width, float height)
    {
        return handle switch
        {
            ScaleFromEdgeHandle.Left => center - right * width,
            ScaleFromEdgeHandle.Right => center + right * width,
            ScaleFromEdgeHandle.Up => center + up * height,
            ScaleFromEdgeHandle.Down => center - up * height,
            ScaleFromEdgeHandle.UpLeft => center - right * width + up * height,
            ScaleFromEdgeHandle.UpRight => center + right * width + up * height,
            ScaleFromEdgeHandle.DownLeft => center - right * width - up * height,
            ScaleFromEdgeHandle.DownRight => center + right * width - up * height,
            _ => center
        };
    }
}
