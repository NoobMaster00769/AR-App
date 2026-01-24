using UnityEngine;

namespace Spatial.Proxies
{
    public class FurnitureProxy
    {
        public GameObject proxyObject;
        public Vector3 center;
        public float confidence;
        public float lastUpdated;

        public FurnitureProxy(GameObject obj, Vector3 center, float confidence)
        {
            this.proxyObject = obj;
            this.center = center;
            this.confidence = confidence;
            this.lastUpdated = Time.time;
        }

        public void Update(Vector3 newCenter, float newConfidence)
        {
            center = newCenter;
            confidence = Mathf.Max(confidence, newConfidence);
            lastUpdated = Time.time;

            proxyObject.transform.position = center;
        }
    }
}
