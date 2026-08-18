using UnityEngine;

public class EditorHUD : MonoBehaviour
{
    [SerializeField] LevelEditor levelEditor;

    public void Bind(LevelEditor targetLevelEditor)
    {
        levelEditor = targetLevelEditor;
    }

    public void SetPointerIsOverObjectSelectionBarTrue() => levelEditor?.SetPointerIsOverObjectSelectionBarTrue();
    public void SetPointerIsOverObjectSelectionBarFalse() => levelEditor?.SetPointerIsOverObjectSelectionBarFalse();
    public void CopyLevelCodeToClipboard() => levelEditor?.CopyLevelCodeToClipboard();
    public void SwitchToWorldTransformMode() => levelEditor?.SwitchToWorldTransformMode();
    public void SwitchToLocalTransformMode() => levelEditor?.SwitchToLocalTransformMode();
    public void SaveLevel() => levelEditor?.SaveLevel();
    public void SnapSelectedObjectToLastHorizontal() => levelEditor?.SnapSelectedObjectToLastHorizontal();
    public void SnapSelectedObjectToLastVertical() => levelEditor?.SnapSelectedObjectToLastVertical();
    public void DeselectObject() => levelEditor?.DeselectObject();
    public void LoadLevelFromClipboard() => levelEditor?.LoadLevelFromClipboard();
    public void DeleteAllLevelObjects() => levelEditor?.DeleteAllLevelObjects();
    public void SwitchToPlayMode() => levelEditor?.SwitchToPlayMode();
    public void PlaceBooster() => levelEditor?.PlaceBooster();
    public void PlaceBouncyWall() => levelEditor?.PlaceBouncyWall();
    public void PlaceConstantPuller() => levelEditor?.PlaceConstantPuller();
    public void PlaceConstantPusher() => levelEditor?.PlaceConstantPusher();
    public void PlaceFinish() => levelEditor?.PlaceFinish();
    public void PlaceKillCircle() => levelEditor?.PlaceKillCircle();
    public void PlaceKillWall() => levelEditor?.PlaceKillWall();
    public void PlacePuller() => levelEditor?.PlacePuller();
    public void PlacePusher() => levelEditor?.PlacePusher();
    public void PlaceSlipperyWall() => levelEditor?.PlaceSlipperyWall();

    public void SetTouchPointIsOverButtonTrue()
    {
        GameManager.Instance.SetTouchPointIsOverButtonTrue();
    }

    public void SetTouchPointIsOverButtonFalse()
    {
        GameManager.Instance.SetTouchPointIsOverButtonFalse();
    }
}
