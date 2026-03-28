// ARModeToggleButton.cs
// Attach to the same GameObject as (or reference) a UnityEngine.UI.Button.
// Wire the Button's OnClick → ARModeToggleButton.OnClick in the Inspector.
// The label will show "Mode: PLACE" or "Mode: MOVE".

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ARModeToggleButton : MonoBehaviour
{
    [Header("References")]
    public ARTouchRouter router;

    [Tooltip("Optional TMP label on the button to show current mode")]
    public TMP_Text modeLabel;

    void Start()
    {
        RefreshLabel();
    }

    // Wire this to your Button's OnClick event in the Inspector
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
            ? "Mode: PLACE"
            : "Mode: MOVE";
    }
}