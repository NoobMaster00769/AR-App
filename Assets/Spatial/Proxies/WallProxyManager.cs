using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Spatial.Proxies;
using UnityEngine.XR.ARSubsystems;

namespace Spatial.Proxies
{
    [RequireComponent(typeof(ARPlaneManager))]
    public class WallProxyManager : MonoBehaviour
    {
        ARPlaneManager planeManager;

        readonly Dictionary<TrackableId, WallProxy> wallProxies = new();

        [Header("Wall Proxy Settings")]
        public float wallHeight = 3f;
        public float wallThickness = 0.05f;
        public LayerMask wallLayer;

        void Awake()
        {
            planeManager = GetComponent<ARPlaneManager>();
        }

        void OnEnable()
        {
            planeManager.planesChanged += OnPlanesChanged;
        }

        void OnDisable()
        {
            planeManager.planesChanged -= OnPlanesChanged;
        }

        void OnPlanesChanged(ARPlanesChangedEventArgs args)
        {
            foreach (var plane in args.added)
                TryAddWallProxy(plane);

            foreach (var plane in args.updated)
                UpdateWallProxy(plane);

            foreach (var plane in args.removed)
                RemoveWallProxy(plane);
        }

        void TryAddWallProxy(ARPlane plane)
        {
            if (plane.alignment != PlaneAlignment.Vertical)
                return;

            if (wallProxies.ContainsKey(plane.trackableId))
                return;

            GameObject proxy = CreateWallProxyObject(plane);
            wallProxies.Add(
                plane.trackableId,
                new WallProxy(plane, proxy)
            );
        }

        void UpdateWallProxy(ARPlane plane)
        {
            if (!wallProxies.TryGetValue(plane.trackableId, out var proxy))
                return;

            proxy.UpdateFromPlane();
        }

        void RemoveWallProxy(ARPlane plane)
        {
            if (!wallProxies.TryGetValue(plane.trackableId, out var proxy))
                return;

            Destroy(proxy.proxyObject);
            wallProxies.Remove(plane.trackableId);
        }

        GameObject CreateWallProxyObject(ARPlane plane)
        {
            GameObject wall = new GameObject("WallProxy");

            wall.layer = wallLayer.value != 0
                ? Mathf.RoundToInt(Mathf.Log(wallLayer.value, 2))
                : 0;

            wall.transform.SetParent(transform);

            BoxCollider collider = wall.AddComponent<BoxCollider>();
            collider.size = new Vector3(
                plane.size.x,
                wallHeight,
                wallThickness
            );
            collider.center = new Vector3(0, wallHeight * 0.5f, 0);

            wall.transform.SetPositionAndRotation(
                plane.transform.position,
                plane.transform.rotation
            );

            return wall;
        }

        public IReadOnlyCollection<WallProxy> GetWalls()
        {
            return wallProxies.Values;
        }
    }
}
