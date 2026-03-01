using UnityEngine;
using Unity.Barracuda;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

[RequireComponent(typeof(ARRaycastManager))]
public class YOLOv8Detector : MonoBehaviour
{
    public NNModel modelAsset;
    public ARCameraFrameProvider frameProvider;

    Model runtimeModel;
    IWorker worker;
    ARRaycastManager raycastManager;

    static List<ARRaycastHit> hits = new List<ARRaycastHit>();
    GameObject detectedObject;

    [Header("Detection")]
    public float confidenceThreshold = 0.15f;
    public float followSpeed = 0.2f;
    public float objectScale = 0.2f;

    [Header("Debug")]
    public bool enableDebugLogs = true;
    public int inferenceInterval = 5;   // run every N frames

    int frameCounter = 0;

    void Start()
    {
        runtimeModel = ModelLoader.Load(modelAsset);
        worker = WorkerFactory.CreateWorker(
            WorkerFactory.Type.Auto,
            runtimeModel);

        raycastManager = GetComponent<ARRaycastManager>();

        if (enableDebugLogs)
        {
            Debug.Log("YOLO model loaded.");
            Debug.Log("Output shape: " + runtimeModel.outputs[0]);
        }
    }

    void Update()
    {
        if (frameProvider == null || frameProvider.cameraTexture == null)
            return;

        frameCounter++;
        if (frameCounter % inferenceInterval != 0)
            return;

        Tensor input = new Tensor(frameProvider.cameraTexture, 3);
        worker.Execute(input);
        Tensor output = worker.PeekOutput();

        ParseOutput(output);

        input.Dispose();
        output.Dispose();
    }

    void ParseOutput(Tensor output)
    {
        int boxCount = output.shape[1];     
        int valuesPerBox = output.shape[2]; 

        float bestScore = 0f;
        int bestIndex = -1;

        for (int i = 0; i < boxCount; i++)
        {
            int baseIndex = i * valuesPerBox;

            float objectness = output[0, baseIndex + 4];

            float bestClassScore = 0f;

            for (int c = 5; c < valuesPerBox; c++)
            {
                float classScore = output[0, baseIndex + c];
                if (classScore > bestClassScore)
                    bestClassScore = classScore;
            }

            float finalScore = objectness * bestClassScore;

            if (finalScore > bestScore)
            {
                bestScore = finalScore;
                bestIndex = i;
            }
        }

        if (enableDebugLogs)
            Debug.Log("Best score this frame: " + bestScore);

        if (bestScore < confidenceThreshold)
            return;

        int bestBase = bestIndex * valuesPerBox;

        float x = output[0, bestBase + 0];
        float y = output[0, bestBase + 1];
        float w = output[0, bestBase + 2];
        float h = output[0, bestBase + 3];

        if (enableDebugLogs)
        {
            Debug.Log($"Detection -> Score:{bestScore} X:{x} Y:{y} W:{w} H:{h}");
        }

        // YOLOv8 outputs normalized center coords (0–1)
        Vector2 screenPoint = new Vector2(
            x * Screen.width,
            (1 - y) * Screen.height   // flip Y if needed
        );

        MapToWorld(screenPoint);
    }

    void MapToWorld(Vector2 screenPoint)
    {
        if (raycastManager.Raycast(
            screenPoint,
            hits,
            TrackableType.PlaneWithinPolygon))
        {
            Pose pose = hits[0].pose;

            if (detectedObject == null)
            {
                detectedObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                detectedObject.transform.localScale = Vector3.one * objectScale;

                Rigidbody rb = detectedObject.AddComponent<Rigidbody>();
                rb.useGravity = false;
                rb.isKinematic = true;

                if (enableDebugLogs)
                    Debug.Log("Cube created.");
            }

            detectedObject.transform.position =
                Vector3.Lerp(
                    detectedObject.transform.position,
                    pose.position,
                    followSpeed);
        }
    }

    void OnDestroy()
    {
        worker?.Dispose();
    }
}