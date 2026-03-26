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

        box.gameObject.SetActive(true);

        float screenX = x * Screen.width;
        float screenY = y * Screen.height;

        box.anchoredPosition = new Vector2(
            screenX - Screen.width / 2f,
            screenY - Screen.height / 2f
        );

        box.sizeDelta = new Vector2(200, 200);
    }
}