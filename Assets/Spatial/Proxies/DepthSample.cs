using UnityEngine;

namespace Spatial.Proxies
{
    public struct DepthSample
    {
        public Vector3 position;
        public float timestamp;

        public DepthSample(Vector3 pos, float time)
        {
            position = pos;
            timestamp = time;
        }
    }
}
