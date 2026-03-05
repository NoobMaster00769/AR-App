using UnityEngine;

public class VisionReceiver : MonoBehaviour
{
    public ObjectSpawner spawner;

    public void OnDetection(string message)
    {
        Debug.Log("DETECTION MESSAGE: " + message);

        string[] parts = message.Split(':');

        string label = parts[0];
        float x = float.Parse(parts[1]);
        float y = float.Parse(parts[2]);

        Debug.Log("Parsed detection -> label:" + label +
                  " x:" + x +
                  " y:" + y);

        if (spawner != null)
        {
            spawner.OnDetection(message);
        }
    }
}