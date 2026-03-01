using System.Collections.Generic;
using UnityEngine;

public class SpatialDepthReconstruction : MonoBehaviour
{
    [Header("Sampling")]
    public float sampleRate = 25f;
    public float maxDistance = 5f;
    public float sphereRadius = 0.12f;

    [Header("Clustering")]
    public float clusterRadius = 0.35f;
    public int minSamplesPerCluster = 8;

    [Header("Proxy Volume")]
    public Vector3 proxySize = new Vector3(0.6f, 0.6f, 0.3f);
    public LayerMask occlusionLayer;

    [Header("Lifetime")]
    public float sampleLifetime = 2f;
    public float rebuildInterval = 0.4f;

    Camera cam;

    float sampleTimer;
    float rebuildTimer;

    class DepthSample
    {
        public Vector3 pos;
        public float time;
        public DepthSample(Vector3 p, float t)
        {
            pos = p;
            time = t;
        }
    }

    List<DepthSample> samples = new();
    List<GameObject> proxies = new();

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        if (!cam) return;

        SampleDepth();
        CleanupSamples();

        rebuildTimer += Time.deltaTime;
        if (rebuildTimer >= rebuildInterval)
        {
            rebuildTimer = 0f;
            RebuildProxies();
        }
    }

    void SampleDepth()
    {
        sampleTimer += Time.deltaTime;
        if (sampleTimer < 1f / sampleRate) return;
        sampleTimer = 0f;

        Vector3[] offsets =
        {
            Vector3.zero,
            cam.transform.right * 0.2f,
            -cam.transform.right * 0.2f,
            cam.transform.up * 0.15f,
            -cam.transform.up * 0.25f
        };

        foreach (var offset in offsets)
        {
            Vector3 origin = cam.transform.position + offset;
            Vector3 dir = cam.transform.forward;

            if (Physics.SphereCast(origin, sphereRadius, dir,
                out RaycastHit hit, maxDistance))
            {
                samples.Add(new DepthSample(hit.point, Time.time));
            }
        }
    }

    void CleanupSamples()
    {
        float now = Time.time;
        samples.RemoveAll(s => now - s.time > sampleLifetime);
    }

    void RebuildProxies()
    {
        foreach (var p in proxies)
            Destroy(p);

        proxies.Clear();

        List<List<Vector3>> clusters = ClusterSamples();

        foreach (var cluster in clusters)
        {
            if (cluster.Count < minSamplesPerCluster)
                continue;

            Vector3 center = Average(cluster);
            CreateProxy(center);
        }
    }

    List<List<Vector3>> ClusterSamples()
    {
        List<List<Vector3>> clusters = new();
        List<Vector3> unassigned = new();

        foreach (var s in samples)
            unassigned.Add(s.pos);

        while (unassigned.Count > 0)
        {
            Vector3 seed = unassigned[0];
            unassigned.RemoveAt(0);

            List<Vector3> cluster = new() { seed };

            for (int i = unassigned.Count - 1; i >= 0; i--)
            {
                if (Vector3.Distance(seed, unassigned[i]) <= clusterRadius)
                {
                    cluster.Add(unassigned[i]);
                    unassigned.RemoveAt(i);
                }
            }

            clusters.Add(cluster);
        }

        return clusters;
    }

    void CreateProxy(Vector3 center)
    {
        GameObject proxy = new GameObject("DepthProxy");
        proxy.layer = Mathf.RoundToInt(Mathf.Log(occlusionLayer.value, 2));

        BoxCollider box = proxy.AddComponent<BoxCollider>();
        box.size = proxySize;

        proxy.transform.position = center;

        proxies.Add(proxy);
    }

    Vector3 Average(List<Vector3> points)
    {
        Vector3 sum = Vector3.zero;
        foreach (var p in points)
            sum += p;
        return sum / points.Count;
    }
}