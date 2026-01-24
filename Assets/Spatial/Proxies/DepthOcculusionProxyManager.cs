using System.Collections.Generic;
using UnityEngine;
using Spatial.Core;

namespace Spatial.Proxies
{
    public class DepthOcclusionProxyManager : MonoBehaviour
    {
        [Header("Promotion")]
        public FurnitureProxyManager furnitureManager;

        [Header("Sampling")]
        public int samplesPerSecond = 20;
        public float maxSampleDistance = 5f;
        public float sphereRadius = 0.15f;

        [Header("Clustering")]
        public float clusterRadius = 0.4f;
        public int minSamplesPerCluster = 6;

        [Header("Proxy Volumes")]
        public Vector3 proxySize = new Vector3(0.4f, 0.6f, 0.4f);
        public LayerMask occlusionLayer;

        [Header("Lifetime")]
        public float sampleLifetime = 2f;
        public float proxyUpdateInterval = 0.5f;

        [Header("Debug (Works in Build)")]
        public bool showDebug = true;
        public Color sampleColor = Color.yellow;
        public Color clusterColor = Color.cyan;

        readonly List<DepthSample> samples = new();
        readonly List<GameObject> proxies = new();
        readonly List<GameObject> debugPoints = new();

        Camera cam;
        float sampleTimer;
        float proxyTimer;

        void Start()
        {
            cam = Camera.main;
        }

        void Update()
        {
            if (!cam) return;

            SampleDepth();
            CleanupOldSamples();

            proxyTimer += Time.deltaTime;
            if (proxyTimer >= proxyUpdateInterval)
            {
                proxyTimer = 0f;
                RebuildProxies();
            }
        }

        // ---------------------------------------------------------
        // 🔥 SPHERECAST‑BASED DEPTH SAMPLING (KEY UPGRADE)
        // ---------------------------------------------------------
        void SampleDepth()
        {
            sampleTimer += Time.deltaTime;
            if (sampleTimer < 1f / samplesPerSecond)
                return;

            sampleTimer = 0f;

            Vector3[] offsets =
            {
                Vector3.zero,
                cam.transform.right * 0.2f,
                -cam.transform.right * 0.2f,
                cam.transform.up * 0.15f,
                -cam.transform.up * 0.25f // downward bias for tables
            };

            foreach (var offset in offsets)
            {
                Vector3 origin = cam.transform.position + offset;
                Vector3 dir = cam.transform.forward;

                if (Physics.SphereCast(
                    origin,
                    sphereRadius,
                    dir,
                    out RaycastHit hit,
                    maxSampleDistance))
                {
                    samples.Add(new DepthSample(hit.point, Time.time));

                    // OPTIONAL UPGRADE: thicken volume with OverlapSphere
                    Collider[] overlaps = Physics.OverlapSphere(
                        hit.point,
                        sphereRadius * 1.5f,
                        occlusionLayer);

                    foreach (var c in overlaps)
                    {
                        samples.Add(
                            new DepthSample(
                                c.ClosestPoint(hit.point),
                                Time.time));
                    }

                    // Runtime debug (visible in build)
                    if (showDebug)
                        SpawnDebugPoint(hit.point, sampleColor);
                }
            }
        }

        void CleanupOldSamples()
        {
            float now = Time.time;
            samples.RemoveAll(s => now - s.timestamp > sampleLifetime);
        }

        // ---------------------------------------------------------
        // 🧱 CLUSTER → PROXY → FURNITURE PROMOTION
        // ---------------------------------------------------------
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

                if (furnitureManager != null)
                    furnitureManager.SubmitCluster(center);

                if (showDebug)
                    SpawnDebugPoint(center, clusterColor);
            }
        }

        List<List<Vector3>> ClusterSamples()
        {
            List<List<Vector3>> clusters = new();
            List<Vector3> unassigned = new();

            foreach (var s in samples)
                unassigned.Add(s.position);

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
            GameObject proxy = new GameObject("DepthOcclusionProxy");
            proxy.layer = Mathf.RoundToInt(Mathf.Log(occlusionLayer.value, 2));

            BoxCollider collider = proxy.AddComponent<BoxCollider>();
            collider.size = proxySize;

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

        // ---------------------------------------------------------
        // 👁️ DEBUG VISUALIZATION (VISIBLE IN BUILD)
        // ---------------------------------------------------------
        void SpawnDebugPoint(Vector3 pos, Color color)
        {
            GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dot.transform.position = pos;
            dot.transform.localScale = Vector3.one * 0.04f;

            var renderer = dot.GetComponent<MeshRenderer>();
            renderer.material = new Material(Shader.Find("Unlit/Color"));
            renderer.material.color = color;

            Destroy(dot.GetComponent<Collider>());
            Destroy(dot, 0.5f);

            debugPoints.Add(dot);
        }
    }
}
