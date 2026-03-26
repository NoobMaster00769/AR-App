using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public class ARPlacementSystem : MonoBehaviour
{
    public ARRaycastManager raycastManager;

    static List<ARRaycastHit> hits = new List<ARRaycastHit>();

    void Update()
    {
        if (Input.touchCount == 0) return;

        Touch touch = Input.GetTouch(0);

        if (touch.phase != TouchPhase.Began) return;

        if (PrefabSelector.selectedPrefab == null)
        {
            Debug.Log("❌ No prefab selected");
            return;
        }

        TryPlace(touch.position);
    }

    void TryPlace(Vector2 screenPosition)
    {
        bool hitPlane = raycastManager.Raycast(
            screenPosition,
            hits,
            TrackableType.PlaneWithinPolygon
        );

        GameObject obj;

        if (hitPlane)
        {
            Pose pose = hits[0].pose;

            obj = Instantiate(
                PrefabSelector.selectedPrefab,
                pose.position,
                pose.rotation
            );

            SetupPhysics(obj, true);
        }
        else
        {
            // place in air
            Vector3 pos =
                Camera.main.transform.position +
                Camera.main.transform.forward * 1.2f;

            obj = Instantiate(
                PrefabSelector.selectedPrefab,
                pos,
                Quaternion.identity
            );

            SetupPhysics(obj, false);
        }

        obj.transform.localScale = Vector3.one * 0.1f;

        if (obj.GetComponent<ARInteractable>() == null)
            obj.AddComponent<ARInteractable>();
    }

    void SetupPhysics(GameObject obj, bool onPlane)
    {
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null) rb = obj.AddComponent<Rigidbody>();

        Collider col = obj.GetComponent<Collider>();
        if (col == null) obj.AddComponent<BoxCollider>();

        if (onPlane)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        else
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        rb.mass = 0.5f;
        rb.drag = 0.4f;
        rb.angularDrag = 0.4f;
    }
}