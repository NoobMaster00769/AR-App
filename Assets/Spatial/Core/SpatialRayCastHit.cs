using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Spatial.Core
{
    public enum SpatialSurfaceType
    {
        Mesh,
        Plane,
        Depth,
        Unknown
    }

    public struct SpatialRaycastHit
    {
        public Pose pose;
        public float distance;
        public SpatialSurfaceType surfaceType;
        public float confidence;
        public ARTrackable trackable;

        public bool IsValid => confidence > 0.5f;

        public SpatialRaycastHit(
            Pose pose,
            float distance,
            SpatialSurfaceType surfaceType,
            float confidence,
            ARTrackable trackable = null)
        {
            this.pose = pose;
            this.distance = distance;
            this.surfaceType = surfaceType;
            this.confidence = confidence;
            this.trackable = trackable;
        }
    }
}
