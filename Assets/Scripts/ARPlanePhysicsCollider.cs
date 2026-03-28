
using UnityEngine;
using UnityEngine.XR.ARFoundation;

[RequireComponent(typeof(ARPlaneManager))]
public class ARPlanePhysicsCollider : MonoBehaviour
{
    static PhysicMaterial _planeMat;

    ARPlaneManager _planeManager;

    void Awake()
    {
        _planeManager = GetComponent<ARPlaneManager>();

        if (_planeMat == null)
        {
            _planeMat = new PhysicMaterial("ARPlane")
            {
                bounciness      = 0f,
                dynamicFriction = 0.6f,
                staticFriction  = 0.6f,
                frictionCombine = PhysicMaterialCombine.Average,
                bounceCombine   = PhysicMaterialCombine.Minimum
            };
        }
    }

    void OnEnable()  => _planeManager.planesChanged += OnPlanesChanged;
    void OnDisable() => _planeManager.planesChanged -= OnPlanesChanged;

    void OnPlanesChanged(ARPlanesChangedEventArgs args)
    {
        foreach (var plane in args.added)   Sync(plane);
        foreach (var plane in args.updated) Sync(plane);
    }

    void Sync(ARPlane plane)
    {
        var mf = plane.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        var rb = plane.GetComponent<Rigidbody>();
        if (rb) Destroy(rb);

   
        foreach (var col in plane.GetComponents<Collider>())
            Destroy(col);

        var bounds = mf.sharedMesh.bounds;
        var bc     = plane.gameObject.AddComponent<BoxCollider>();
        bc.center  = bounds.center;
        bc.size    = new Vector3(
            bounds.size.x,
            Mathf.Max(bounds.size.y, 0.05f), 
            bounds.size.z);
        bc.material = _planeMat;
    }
}