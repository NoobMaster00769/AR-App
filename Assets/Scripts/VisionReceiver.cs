using UnityEngine;

public class VisionReceiver : MonoBehaviour
{
    public BoundingBoxUI bboxUI;
    public ObjectSpawner spawner;

    public void OnDetection(string message)
    {
        Debug.Log("DETECTION MESSAGE: " + message);

        string[] parts = message.Split(':');
        if (parts.Length != 3) return;

        string label = parts[0];
        float x = float.Parse(parts[1]);
        float y = float.Parse(parts[2]);

        // ✅ FIX: now x,y exist
        if (bboxUI != null)
            bboxUI.DrawBox(0.5f, 0.5f);

        if (spawner != null)
            spawner.OnDetection(message);
    }
}