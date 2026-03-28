
using UnityEngine;
using TMPro;

public class ARModeToggleButton : MonoBehaviour
{
    [Header("References")]
    public ARTouchRouter router;
    public TMP_Text      modeLabel;

    void Start() => RefreshLabel();

    public void OnClick()
    {
        if (router == null) return;
        router.SwitchMode();
        RefreshLabel();
    }

    void RefreshLabel()
    {
        if (modeLabel == null || router == null) return;
        modeLabel.text = router.CurrentMode == ARTouchRouter.InteractionMode.Place
            ? "Mode: PLACE" : "Mode: MOVE";
    }
}