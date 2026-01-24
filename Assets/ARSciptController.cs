using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARPlacementController : MonoBehaviour
{
    [Header("Managers")]
    public ARRaycastManager raycastManager;
    public ARPlaneManager planeManager;
    public ARAnchorManager anchorManager;

    [Header("Placement")]
    public GameObject placeablePrefab;

    [Header("Stability")]
    public float placementDelay = 2f;

    [Header("Physics Layers (IMPORTANT)")]
    public LayerMask physicsPlacementLayers;
    // WallProxy | FurnitureProxy | DepthOcclusion

    static List<ARRaycastHit> hits = new();

    float startTime;
    bool objectPlaced = false;

    void Start()
    {
        startTime = Time.time;
    }

    void Update()
    {
        if (objectPlaced) return;
        if (Time.time - startTime < placementDelay) return;
        if (Input.touchCount == 0) return;

        Touch touch = Input.GetTouch(0);
        if (touch.phase != TouchPhase.Began) return;

        Ray ray = Camera.main.ScreenPointToRay(touch.position);

        // =================================================
        // 1️⃣ PHYSICS FIRST (DEPTH + FURNITURE + WALLS)
        // =================================================
        if (Physics.Raycast(
                ray,
                out RaycastHit physicsHit,
                10f,
                physicsPlacementLayers))
        {
            PlaceAt(
                physicsHit.point,
                Quaternion.LookRotation(physicsHit.normal));
            return;
        }

        // =================================================
        // 2️⃣ DEPTH DOMINANCE ZONE (NO PLANE FALLBACK)
        // =================================================
        float camY = Camera.main.transform.position.y;
        float expectedFurnitureMin = camY - 1.4f;
        float expectedFurnitureMax = camY - 0.4f;

        // If user is tapping in furniture height band,
        // do NOT allow planes to interfere
        if (expectedFurnitureMin > 0.4f)
            return;

        // =================================================
        // 3️⃣ FALLBACK TO FLOOR PLANES ONLY
        // =================================================
        if (!raycastManager.Raycast(
                touch.position,
                hits,
                TrackableType.PlaneWithinPolygon))
            return;

        var hit = hits[0];
        ARPlane plane = planeManager.GetPlane(hit.trackableId);
        if (plane == null) return;

        // ❌ NO TABLE PLANES
        if (plane.alignment != PlaneAlignment.HorizontalUp)
            return;

        // ❌ ONLY FLOOR (IMPORTANT)
        float planeY = plane.transform.position.y;
        if (planeY > 0.25f)
            return;

        // =================================================
        // 4️⃣ CLAMP PLANE EXTENTS (ANTI OVER‑EXTENSION)
        // =================================================
        Vector3 local = plane.transform.InverseTransformPoint(hit.pose.position);
        Vector2 extents = plane.extents;

        local.x = Mathf.Clamp(local.x, -extents.x * 0.6f, extents.x * 0.6f);
        local.z = Mathf.Clamp(local.z, -extents.y * 0.6f, extents.y * 0.6f);

        Vector3 clampedWorld =
            plane.transform.TransformPoint(local);

        PlaceAt(clampedWorld, hit.pose.rotation);
    }

    void PlaceAt(Vector3 position, Quaternion rotation)
    {
        GameObject anchorObj = new GameObject("Anchor");
        ARAnchor anchor = anchorObj.AddComponent<ARAnchor>();

        anchor.transform.SetPositionAndRotation(position, rotation);

        Instantiate(
            placeablePrefab,
            position,
            rotation,
            anchor.transform);

        objectPlaced = true;

        // 🔒 Lock planes after placement
        foreach (var p in planeManager.trackables)
            p.gameObject.SetActive(false);

        planeManager.enabled = false;
    }
}
