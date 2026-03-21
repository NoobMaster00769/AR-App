using UnityEngine;

public class BoundingBoxUI : MonoBehaviour
{
    public RectTransform boxPrefab;
    public Canvas canvas;

    public void DrawBox(float x, float y)
    {
        RectTransform box = Instantiate(boxPrefab, canvas.transform);

        box.anchoredPosition = new Vector2(
            x * Screen.width,
            y * Screen.height
        );

        box.sizeDelta = new Vector2(100, 100); // simple box size
    }
}