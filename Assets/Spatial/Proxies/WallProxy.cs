using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Spatial.Proxies
{
    public class WallProxy
    {
        public ARPlane sourcePlane;
        public GameObject proxyObject;
        public Vector3 normal;
        public Pose pose;

        public WallProxy(ARPlane plane, GameObject proxy)
        {
            sourcePlane = plane;
            proxyObject = proxy;
            UpdateFromPlane();
        }

        public void UpdateFromPlane()
        {
            pose = new Pose(
                sourcePlane.transform.position,
                sourcePlane.transform.rotation
            );

            // Plane normal (forward for vertical planes)
            normal = sourcePlane.transform.up;

            proxyObject.transform.SetPositionAndRotation(
                pose.position,
                pose.rotation
            );
        }
    }
}
