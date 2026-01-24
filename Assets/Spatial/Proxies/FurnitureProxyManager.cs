using System.Collections.Generic;
using UnityEngine;

namespace Spatial.Proxies
{
    public class FurnitureProxyManager : MonoBehaviour
    {
        [Header("Promotion thresholds")]
        public float promotionRadius = 0.3f;
        public int framesRequired = 6;
        public float decayTime = 8f;

        [Header("Proxy settings")]
        public Vector3 proxySize = new Vector3(0.6f, 0.8f, 0.6f);
        public LayerMask furnitureLayer;

        readonly List<Vector3> recentCenters = new();
        readonly Dictionary<int, FurnitureProxy> furnitureProxies = new();

        int frameCounter;

        void Update()
        {
            frameCounter++;

            CleanupOldFurniture();
        }

        /// <summary>
        /// Called by DepthOcclusionProxyManager when it finds a stable cluster
        /// </summary>
        public void SubmitCluster(Vector3 center)
        {
            recentCenters.Add(center);

            if (recentCenters.Count < framesRequired)
                return;

            Vector3 avg = Average(recentCenters);
            int hash = Hash(avg);

            if (furnitureProxies.TryGetValue(hash, out var proxy))
            {
                proxy.Update(avg, 1f);
            }
            else
            {
                CreateFurnitureProxy(avg, hash);
            }

            recentCenters.Clear();
        }

        void CreateFurnitureProxy(Vector3 center, int hash)
        {
            GameObject obj = new GameObject("FurnitureProxy");
            obj.layer = Mathf.RoundToInt(Mathf.Log(furnitureLayer.value, 2));

            BoxCollider collider = obj.AddComponent<BoxCollider>();
            collider.size = proxySize;

            obj.transform.position = center;

            furnitureProxies.Add(
                hash,
                new FurnitureProxy(obj, center, 1f)
            );
        }

        void CleanupOldFurniture()
        {
            float now = Time.time;

            var toRemove = new List<int>();

            foreach (var kv in furnitureProxies)
            {
                if (now - kv.Value.lastUpdated > decayTime)
                    toRemove.Add(kv.Key);
            }

            foreach (var key in toRemove)
            {
                Destroy(furnitureProxies[key].proxyObject);
                furnitureProxies.Remove(key);
            }
        }

        Vector3 Average(List<Vector3> points)
        {
            Vector3 sum = Vector3.zero;
            foreach (var p in points)
                sum += p;
            return sum / points.Count;
        }

        int Hash(Vector3 v)
        {
            return Mathf.RoundToInt(v.x * 10f) ^
                   Mathf.RoundToInt(v.y * 10f) << 2 ^
                   Mathf.RoundToInt(v.z * 10f) >> 2;
        }
    }
}
