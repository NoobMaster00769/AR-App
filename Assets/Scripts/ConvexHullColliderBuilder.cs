
using UnityEngine;

public static class ConvexHullColliderBuilder
{
    static readonly PhysicMaterial _objMat = new("SpawnedObject")
    {
        bounciness      = 0.05f,
        dynamicFriction = 0.55f,
        staticFriction  = 0.55f,
        frictionCombine = PhysicMaterialCombine.Average,
        bounceCombine   = PhysicMaterialCombine.Minimum
    };

    public static void Build(GameObject root)
    {
        var existingColliders = root.GetComponentsInChildren<Collider>();
        bool hasPhysicsCollider = false;

        foreach (var col in existingColliders)
        {
            if (col.isTrigger) continue; 

            col.material = _objMat;
            hasPhysicsCollider = true;
        }

        if (hasPhysicsCollider)
        {
            EnsureRootTrigger(root);
            Debug.Log($"✅ Using existing colliders on {root.name}");
            return;
        }

        FallbackBoxCollider(root);
    }

    static void EnsureRootTrigger(GameObject root)
    {
        foreach (var col in root.GetComponents<BoxCollider>())
            if (col.isTrigger) return; 

        var rends = root.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return;

        Bounds worldBounds = rends[0].bounds;
        foreach (var r in rends) worldBounds.Encapsulate(r.bounds);

        Vector3 localCenter = root.transform.InverseTransformPoint(worldBounds.center);
        Vector3 ls          = root.transform.lossyScale;
        Vector3 localSize   = new Vector3(
            ls.x > 0f ? worldBounds.size.x / ls.x : worldBounds.size.x,
            ls.y > 0f ? worldBounds.size.y / ls.y : worldBounds.size.y,
            ls.z > 0f ? worldBounds.size.z / ls.z : worldBounds.size.z);

        var trigger       = root.AddComponent<BoxCollider>();
        trigger.center    = localCenter;
        trigger.size      = localSize;
        trigger.isTrigger = true;
    }

    static void FallbackBoxCollider(GameObject root)
    {
        var rends = root.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0)
        {
            var fb = root.AddComponent<BoxCollider>();
            fb.size = Vector3.one * 0.1f;
            return;
        }

        Bounds worldBounds = rends[0].bounds;
        foreach (var r in rends) worldBounds.Encapsulate(r.bounds);

        Vector3 localCenter = root.transform.InverseTransformPoint(worldBounds.center);
        Vector3 ls          = root.transform.lossyScale;
        Vector3 localSize   = new Vector3(
            ls.x > 0f ? worldBounds.size.x / ls.x : worldBounds.size.x,
            ls.y > 0f ? worldBounds.size.y / ls.y : worldBounds.size.y,
            ls.z > 0f ? worldBounds.size.z / ls.z : worldBounds.size.z);

        var mainBox      = root.AddComponent<BoxCollider>();
        mainBox.center   = localCenter;
        mainBox.size     = localSize;
        mainBox.material = _objMat;

        var trigger       = root.AddComponent<BoxCollider>();
        trigger.center    = localCenter;
        trigger.size      = localSize;
        trigger.isTrigger = true;

        Debug.Log($"📦 Fallback BoxCollider on {root.name}");
    }
}