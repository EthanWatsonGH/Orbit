using UnityEngine;
using UnityEngine.EventSystems;

public class LaunchDirectionControlUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] RectTransform controlRoot;
    [SerializeField] CanvasGroup canvasGroup;

    PlayerHUD playerHud;
    RectTransform overlayRect;
    Canvas parentCanvas;
    int activePointerId = int.MinValue;
    bool isVisible;

    void Awake()
    {
        if (controlRoot == null)
            controlRoot = transform as RectTransform;
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        playerHud = GetComponentInParent<PlayerHUD>();
        overlayRect = transform.parent as RectTransform;
        parentCanvas = GetComponentInParent<Canvas>();
        isVisible = true;
        SetVisible(false);
    }

    void OnEnable()
    {
        if (PointerInput.Instance != null)
            PointerInput.Instance.HeldPointerUpdated += UpdateActiveDrag;
    }

    void OnDisable()
    {
        if (PointerInput.Instance != null)
            PointerInput.Instance.HeldPointerUpdated -= UpdateActiveDrag;

        EndActiveDrag();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Player player = playerHud != null ? playerHud.Player : null;
        if (player == null || activePointerId != int.MinValue || !player.BeginLaunchDirectionTargetDrag(eventData.position))
            return;

        activePointerId = eventData.pointerId;
    }

    void UpdateActiveDrag(Vector2 screenPosition)
    {
        if (activePointerId == int.MinValue)
            return;

        Player player = playerHud != null ? playerHud.Player : null;
        player?.UpdateLaunchDirectionTargetDrag(screenPosition);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == activePointerId)
            EndActiveDrag();
    }

    void LateUpdate()
    {
        Player player = playerHud != null ? playerHud.Player : null;
        if (player == null || !player.IsAiming || !TryUpdateScreenPosition(player, out Camera activeCamera))
        {
            SetVisible(false);
            return;
        }

        Vector2 direction = (Vector2)activeCamera.WorldToScreenPoint(player.LaunchDirectionTargetPosition)
            - (Vector2)activeCamera.WorldToScreenPoint(player.transform.position);
        if (direction.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            controlRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        SetVisible(true);
    }

    void EndActiveDrag()
    {
        if (activePointerId == int.MinValue)
            return;

        Player player = playerHud != null ? playerHud.Player : null;
        player?.EndLaunchDirectionTargetDrag();
        activePointerId = int.MinValue;
    }

    bool TryUpdateScreenPosition(Player player, out Camera activeCamera)
    {
        activeCamera = null;

        if (controlRoot == null || overlayRect == null || parentCanvas == null ||
            CameraViewManager.Instance == null ||
            !CameraViewManager.Instance.TryGetActiveWorldCamera(out activeCamera))
            return false;

        Vector3 screenPosition = activeCamera.WorldToScreenPoint(player.LaunchDirectionTargetPosition);
        if (screenPosition.z <= 0f)
            return false;

        Camera eventCamera = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect, screenPosition, eventCamera, out Vector2 localPosition))
            return false;

        controlRoot.anchoredPosition = localPosition;
        return true;
    }

    void SetVisible(bool shouldShow)
    {
        if (canvasGroup == null || isVisible == shouldShow)
            return;

        isVisible = shouldShow;
        canvasGroup.alpha = shouldShow ? 1f : 0f;
        canvasGroup.interactable = shouldShow;
        canvasGroup.blocksRaycasts = shouldShow;
    }
}
