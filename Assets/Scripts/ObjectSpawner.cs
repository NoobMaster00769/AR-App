using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public class ObjectSpawner : MonoBehaviour
{
    public ARRaycastManager raycastManager;
    public ARAnchorManager anchorManager;

    public GameObject bottlePrefab;
    public GameObject chairPrefab;
    public GameObject laptopPrefab;
    public GameObject defaultPrefab;

    static List<ARRaycastHit> hits = new List<ARRaycastHit>();

    // ✅ MULTI instances
    Dictionary<string, List<GameObject>> spawnedObjects =
        new Dictionary<string, List<GameObject>>();

    float minDistanceBetweenObjects = 0.2f;

    public void OnDetection(string message)
    {
        string[] parts = message.Split(':');
        if (parts.Length != 3) return;

        string label = parts[0];
        float x = float.Parse(parts[1]);
        float y = float.Parse(parts[2]);

        Vector2 screenPoint = new Vector2(
            x * Screen.width,
            (y * 0.9f) * Screen.height
        );

        Spawn(label, screenPoint);
    }

    void Spawn(string label, Vector2 screenPoint)
    {
        // ❌ DO NOT SPAWN if no plane
        if (!raycastManager.Raycast(
            screenPoint,
            hits,
            TrackableType.PlaneEstimated | TrackableType.PlaneWithinPolygon))
        {
            return;
        }

        var hit = hits[0];
        Pose pose = hit.pose;

        // ✅ init list
        if (!spawnedObjects.ContainsKey(label))
            spawnedObjects[label] = new List<GameObject>();

        // ✅ FIX: renamed loop variable (no conflict)
        foreach (var existingObj in spawnedObjects[label])
        {
            if (Vector3.Distance(existingObj.transform.position, pose.position)
                < minDistanceBetweenObjects)
            {
                return; // too close → skip
            }
        }

        GameObject prefab = GetPrefab(label);

        ARPlane plane = hit.trackable as ARPlane;
        if (plane == null) return;

        ARAnchor anchor = anchorManager.AttachAnchor(plane, pose);
        if (anchor == null) return;

        GameObject newObj = Instantiate(prefab, anchor.transform);

        // ✅ PERFECT LOCK
        newObj.transform.localPosition = Vector3.zero;
        newObj.transform.localRotation = Quaternion.identity;

        // ✅ SMALL SIZE
        newObj.transform.localScale = Vector3.one * 0.1f;

        // ❌ NO PHYSICS DRIFT
        Rigidbody rb = newObj.GetComponent<Rigidbody>();
        if (rb == null) rb = newObj.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.isKinematic = true;

        spawnedObjects[label].Add(newObj);

        Debug.Log("🔥 Spawned: " + label);
    }

    GameObject GetPrefab(string label)
    {
        switch (label)
        {
            case "bottle": return bottlePrefab;
            case "chair": return chairPrefab;
            case "laptop": return laptopPrefab;
            default: return defaultPrefab; // ✅ unknown classes
        }
    }
}