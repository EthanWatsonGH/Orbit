using UnityEngine;

[DefaultExecutionOrder(100)]
public class SelectionControlsUI : MonoBehaviour
{
    [SerializeField] RectTransform controlsRoot;
    [SerializeField] CanvasGroup controlsCanvasGroup;
    [SerializeField] GameObject duplicateControl;
    [SerializeField] GameObject rotateControl;

    LevelEditor levelEditor;
    Transform selectedTransform;
    RectTransform overlayRect;
    Canvas parentCanvas;

    void Awake()
    {
        if (controlsRoot == null)
            controlsRoot = transform as RectTransform;
        if (controlsCanvasGroup == null)
            controlsCanvasGroup = GetComponent<CanvasGroup>();

        overlayRect = transform.parent as RectTransform;
        parentCanvas = GetComponentInParent<Canvas>();
        SetVisible(false);
    }

    public void Initialize(LevelEditor editor)
    {
        levelEditor = editor;
    }

    public void SetSelectedTransform(Transform newSelectedTransform)
    {
        selectedTransform = newSelectedTransform;
    }

    public void SetControlAvailability(
        bool canDuplicate,
        bool canRotate)
    {
        if (duplicateControl != null)
            duplicateControl.SetActive(canDuplicate);
        if (rotateControl != null)
            rotateControl.SetActive(canRotate);
    }

    public void SetVisible(bool shouldShow)
    {
        if (gameObject.activeSelf != shouldShow)
            gameObject.SetActive(shouldShow);
    }

    public void SetControlsVisible(bool shouldShow)
    {
        if (controlsCanvasGroup == null)
            return;

        controlsCanvasGroup.alpha = shouldShow ? 1f : 0f;
        controlsCanvasGroup.interactable = shouldShow;
        controlsCanvasGroup.blocksRaycasts = shouldShow;
    }

    public void BeginControl(ObjectTransformControl control, int pointerId, Vector2 screenPosition)
    {
        if (levelEditor != null)
            levelEditor.BeginObjectTransformControl(control, pointerId, screenPosition);
    }

    public void EndControl(ObjectTransformControl control, int pointerId)
    {
        if (levelEditor != null)
            levelEditor.EndObjectTransformControl(control, pointerId);
    }

    void LateUpdate()
    {
        if (selectedTransform == null)
        {
            SetVisible(false);
            return;
        }

        if (controlsRoot == null || overlayRect == null || parentCanvas == null)
            return;

        Camera activeCamera = Camera.main;
        if (activeCamera == null)
            return;

        Vector3 screenPosition = activeCamera.WorldToScreenPoint(selectedTransform.position);
        if (screenPosition.z <= 0f)
        {
            SetVisible(false);
            return;
        }

        Camera eventCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect, screenPosition, eventCamera, out Vector2 localPosition))
            controlsRoot.anchoredPosition = localPosition;
    }
}
