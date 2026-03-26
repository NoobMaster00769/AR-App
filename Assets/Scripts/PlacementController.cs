using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public class ARPlacementController : MonoBehaviour
{
    public ARRaycastManager raycastManager;
    public Camera arCamera;

    static List<ARRaycastHit> hits = new List<ARRaycastHit>();

    GameObject currentObject;

    void Update()
    {
        if (Input.touchCount == 0) return;

        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            PlaceObject(touch.position);
        }
    }

    void PlaceObject(Vector2 touchPos)
    {
        if (PrefabSelector.selectedPrefab == null)
        {
            Debug.Log("❌ Select prefab first");
            return;
        }

        Pose pose;

        if (raycastManager.Raycast(touchPos, hits, TrackableType.PlaneWithinPolygon))
        {
            pose = hits[0].pose;
        }
        else
        {
            pose = new Pose(
                arCamera.transform.position + arCamera.transform.forward * 1.5f,
                Quaternion.identity
            );
        }

        currentObject = Instantiate(
            PrefabSelector.selectedPrefab,
            pose.position,
            pose.rotation
        );

        currentObject.transform.localScale = Vector3.one * 0.1f;

        SetupPhysics(currentObject);
        AddManipulator(currentObject);
    }

    void SetupPhysics(GameObject obj)
    {
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (!rb) rb = obj.AddComponent<Rigidbody>();

        Collider col = obj.GetComponent<Collider>();
        if (!col) obj.AddComponent<BoxCollider>();

        rb.mass = 0.5f;
        rb.drag = 0.3f;
        rb.angularDrag = 0.3f;

        rb.useGravity = true;
        rb.isKinematic = false;
    }

    void AddManipulator(GameObject obj)
    {
        if (!obj.GetComponent<ARObjectManipulator>())
            obj.AddComponent<ARObjectManipulator>();
    }
}