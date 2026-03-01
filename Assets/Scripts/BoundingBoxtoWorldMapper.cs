using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

[RequireComponent(typeof(ARRaycastManager))]
public class BoundingBoxToWorldMapper : MonoBehaviour
{
    ARRaycastManager raycastManager;
    static List<ARRaycastHit> hits = new List<ARRaycastHit>();

    GameObject currentObject;

    [Header("Settings")]
    public float objectSize = 0.2f;
    public float followSpeed = 0.2f;
    public bool alignWithPlane = true;

    void Awake()
    {
        raycastManager = GetComponent<ARRaycastManager>();
    }

    void Update()
    {
        if (raycastManager == null)
            return;

        // Simulate detection at screen center
        Vector2 simulatedDetectionCenter =
            new Vector2(Screen.width / 2f, Screen.height / 2f);

        TryMapToWorld(simulatedDetectionCenter);
    }

    void TryMapToWorld(Vector2 screenPoint)
    {
        if (raycastManager.Raycast(
            screenPoint,
            hits,
            TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;

            if (currentObject == null)
            {
                CreateObject(hitPose);
            }
            else
            {
                UpdateObject(hitPose);
            }
        }
    }

    void CreateObject(Pose pose)
    {
        currentObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        currentObject.transform.localScale = Vector3.one * objectSize;

        Rigidbody rb = currentObject.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true; // important for stable tracking

        ApplyPose(pose);
    }

    void UpdateObject(Pose pose)
    {
        Vector3 targetPosition = pose.position;

        // Smooth movement
        currentObject.transform.position =
            Vector3.Lerp(
                currentObject.transform.position,
                targetPosition,
                followSpeed);

        if (alignWithPlane)
        {
            currentObject.transform.rotation =
                Quaternion.Lerp(
                    currentObject.transform.rotation,
                    pose.rotation,
                    followSpeed);
        }
    }

    void ApplyPose(Pose pose)
    {
        currentObject.transform.position = pose.position;

        if (alignWithPlane)
            currentObject.transform.rotation = pose.rotation;
    }
}