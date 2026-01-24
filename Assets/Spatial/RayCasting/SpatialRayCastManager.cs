using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Spatial.Core;

namespace Spatial.Raycasting
{
    [RequireComponent(typeof(ARRaycastManager))]
    [RequireComponent(typeof(ARPlaneManager))]
    public class SpatialRaycastManager : MonoBehaviour
    {
        ARRaycastManager raycastManager;
        ARPlaneManager planeManager;

        static List<ARRaycastHit> hits = new();

        [Header("Confidence thresholds")]
        public float minPlaneArea = 0.2f;
        public float maxDepthDistance = 5f;

        [Header("Wall proxy")]
        public LayerMask wallProxyLayer;

        Camera cam;

        void Awake()
        {
            raycastManager = GetComponent<ARRaycastManager>();
            planeManager = GetComponent<ARPlaneManager>();
            cam = Camera.main;
        }

        public bool Raycast(
            Vector2 screenPoint,
            out SpatialRaycastHit bestHit)
        {
            bestHit = default;

            if (!raycastManager || cam == null)
                return false;

            // ------------------------------------------------------------------
            // 1️⃣ WALL PROXY PHYSICS RAYCAST (HIGHEST PRIORITY)
            // ------------------------------------------------------------------
            Ray ray = cam.ScreenPointToRay(screenPoint);

            if (Physics.Raycast(
                ray,
                out RaycastHit physicsHit,
                10f,
                wallProxyLayer))
            {
                bestHit = new SpatialRaycastHit(
                    new Pose(
                        physicsHit.point,
                        Quaternion.LookRotation(-physicsHit.normal)
                    ),
                    physicsHit.distance,
                    SpatialSurfaceType.Plane,
                    1f
                );

                return true;
            }

            // ------------------------------------------------------------------
            // 2️⃣ AR FOUNDATION RAYCAST (PLANES + DEPTH)
            // ------------------------------------------------------------------
            TrackableType mask =
                TrackableType.PlaneWithinPolygon |
                TrackableType.Depth;

            if (!raycastManager.Raycast(screenPoint, hits, mask))
                return false;

            float bestScore = 0f;

            foreach (var hit in hits)
            {
                SpatialRaycastHit spatialHit = ConvertHit(hit);
                if (!spatialHit.IsValid)
                    continue;

                if (spatialHit.confidence > bestScore)
                {
                    bestScore = spatialHit.confidence;
                    bestHit = spatialHit;
                }
            }

            return bestScore > 0f;
        }

        SpatialRaycastHit ConvertHit(ARRaycastHit hit)
        {
            // -------------------------------------------------
            // DEPTH HIT
            // -------------------------------------------------
            if (hit.hitType == TrackableType.Depth)
            {
                if (hit.distance > maxDepthDistance)
                    return default;

                float confidence =
                    Mathf.Clamp01(1f - hit.distance / maxDepthDistance);

                return new SpatialRaycastHit(
                    hit.pose,
                    hit.distance,
                    SpatialSurfaceType.Depth,
                    confidence
                );
            }

            // -------------------------------------------------
            // PLANE HIT
            // -------------------------------------------------
            if ((hit.hitType & TrackableType.PlaneWithinPolygon) != 0)
            {
                var plane = planeManager.GetPlane(hit.trackableId);
                if (!plane)
                    return default;

                float area = plane.size.x * plane.size.y;
                if (area < minPlaneArea)
                    return default;

                float confidence = Mathf.Clamp01(area);

                return new SpatialRaycastHit(
                    hit.pose,
                    hit.distance,
                    SpatialSurfaceType.Plane,
                    confidence,
                    plane
                );
            }

            return default;
        }
    }
}
