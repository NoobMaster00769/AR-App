using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public class ObjectSpawner : MonoBehaviour
{
    public ARRaycastManager raycastManager;

    public GameObject bottlePrefab;
    public GameObject chairPrefab;
    public GameObject laptopPrefab;
    public GameObject defaultPrefab;

    static List<ARRaycastHit> hits = new List<ARRaycastHit>();

    Dictionary<string, GameObject> spawnedObjects =
        new Dictionary<string, GameObject>();

    public void OnDetection(string message)
    {
        string[] parts = message.Split(':');

        if (parts.Length != 3)
            return;

        string label = parts[0];
        float x = float.Parse(parts[1]);
        float y = float.Parse(parts[2]);

        Vector2 screenPoint =
            new Vector2(
                x * Screen.width,
                y * Screen.height
            );

        SpawnOrMove(label, screenPoint);
    }

    void SpawnOrMove(string label, Vector2 screenPoint)
    {
        GameObject prefab = GetPrefab(label);

        Pose pose;
        bool hitPlane = raycastManager.Raycast(
            screenPoint,
            hits,
            TrackableType.PlaneWithinPolygon
        );

        if (hitPlane)
        {
            pose = hits[0].pose;
            Debug.Log("Plane hit → spawning on plane");
        }
        else
        {
            Debug.Log("Plane miss → spawning in front of camera");

            pose = new Pose(
                Camera.main.transform.position +
                Camera.main.transform.forward * 0.5f,
                Quaternion.identity
            );
        }

        if (!spawnedObjects.ContainsKey(label))
        {
            GameObject obj =
                Instantiate(prefab, pose.position, pose.rotation);

            Rigidbody rb = obj.GetComponent<Rigidbody>();

            if (rb == null)
                rb = obj.AddComponent<Rigidbody>();

            rb.useGravity = true;
            rb.mass = 1f;

            spawnedObjects[label] = obj;
        }
        else
        {
            spawnedObjects[label].transform.position =
                Vector3.Lerp(
                    spawnedObjects[label].transform.position,
                    pose.position,
                    0.3f
                );
        }
    }

    GameObject GetPrefab(string label)
    {
        switch (label)
        {
            case "bottle":
                return bottlePrefab;

            case "chair":
                return chairPrefab;

            case "laptop":
                return laptopPrefab;

            default:
                return defaultPrefab;
        }
    }
}