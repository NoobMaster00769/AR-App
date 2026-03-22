using UnityEngine;

public class BoundingBoxUI : MonoBehaviour
{
    public RectTransform box;

    public void DrawBox(float x, float y)
    {
        if (box == null)
        {
            Debug.Log("❌ BOX NOT ASSIGNED");
            return;
        }

        Debug.Log("✅ DRAWING BOX at: " + x + ", " + y);

        box.gameObject.SetActive(true);

        // convert normalized → screen
        float screenX = x * Screen.width;
        float screenY = y * Screen.height;

        // convert screen → canvas space
        box.anchoredPosition = new Vector2(
            screenX - Screen.width / 2f,
            screenY - Screen.height / 2f
        );

        // BIG so you cannot miss it
        box.sizeDelta = new Vector2(250, 250);
    }
}