using UnityEngine;
using Spatial.Raycasting;
using Spatial.Core;

namespace Spatial.Core
{
    public class SpatialPlacementController : MonoBehaviour
    {
        public SpatialRaycastManager spatialRaycaster;
        public GameObject placeablePrefab;

        Camera cam;

        void Start()
        {
            cam = Camera.main;
        }

        void Update()
        {
            if (Input.touchCount == 0)
                return;

            Touch touch = Input.GetTouch(0);
            if (touch.phase != TouchPhase.Began)
                return;

            if (spatialRaycaster.Raycast(touch.position, out SpatialRaycastHit hit))
            {
                if (!hit.IsValid)
                    return;

                PlaceObject(hit);
            }
        }

        void PlaceObject(SpatialRaycastHit hit)
        {
            GameObject obj = Instantiate(
                placeablePrefab,
                hit.pose.position,
                hit.pose.rotation
            );

            Debug.Log($"Placed on {hit.surfaceType} with confidence {hit.confidence}");
        }
    }
}
