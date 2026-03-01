using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public class ARPlacementSystem : MonoBehaviour
{
    public GameObject placementPrefab;

    ARRaycastManager raycastManager;
    ARAnchorManager anchorManager;

    static List<ARRaycastHit> hits = new List<ARRaycastHit>();

    void Awake()
    {
        raycastManager = GetComponent<ARRaycastManager>();
        anchorManager = GetComponent<ARAnchorManager>();
    }

    void Update()
    {
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            TryPlace(Input.GetTouch(0).position);
        }
    }

    void TryPlace(Vector2 screenPosition)
    {
        if (raycastManager.Raycast(screenPosition, hits,
            TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = hits[0].pose;

            // Align object to plane
            Quaternion planeRotation = Quaternion.LookRotation(
                Vector3.ProjectOnPlane(Camera.main.transform.forward, hitPose.up),
                hitPose.up
            );

            hitPose.rotation = planeRotation;

            ARAnchor anchor = anchorManager.AddAnchor(hitPose);

            if (anchor != null)
            {
                GameObject obj = Instantiate(placementPrefab, anchor.transform);

                // Scale to 10cm
                obj.transform.localScale = Vector3.one * 0.1f;

                // Fix floating
                float height = obj.GetComponent<Renderer>().bounds.size.y;
                obj.transform.localPosition += Vector3.up * (height / 2f);
            }
        }
    }
}