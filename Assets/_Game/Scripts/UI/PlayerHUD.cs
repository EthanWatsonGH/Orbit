using UnityEngine;

public class PlayerHUD : MonoBehaviour
{
    [SerializeField] Player player;

    public Player Player => player;

    public void Bind(Player targetPlayer)
    {
        player = targetPlayer;
    }

    public void PressedLaunch()
    {
        player?.PressedLaunch();
    }

    public void PressedRetry()
    {
        player?.PressedRetry();
    }

    public void SetQuickRetryEnabled(bool isEnabled)
    {
        player?.SetQuickRetryEnabled(isEnabled);
    }

    public void SetQuickLaunchEnabled(bool isEnabled)
    {
        player?.SetQuickLaunchEnabled(isEnabled);
    }

    public void SetFollowPlayerEnabled(bool isEnabled)
    {
        player?.SetFollowPlayerEnabled(isEnabled);
    }

    public void SwitchToLevelEditor()
    {
        player?.SwitchToLevelEditor();
    }

    public void SetTouchPointIsOverButtonTrue()
    {
        GameManager.Instance.SetTouchPointIsOverButtonTrue();
    }

    public void SetTouchPointIsOverButtonFalse()
    {
        GameManager.Instance.SetTouchPointIsOverButtonFalse();
    }
}
