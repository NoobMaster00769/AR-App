using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;
using System;
using System.Runtime.InteropServices;

public class ARCameraMLForwarder : MonoBehaviour
{
    ARCameraManager cameraManager;

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ProcessVisionFrame(
        IntPtr data,
        int width,
        int height
    );

    [DllImport("__Internal")]
    private static extern void InitVision();
#endif

    void Awake()
    {
        cameraManager = GetComponent<ARCameraManager>();
    }

    void Start()
    {
#if UNITY_IOS && !UNITY_EDITOR
        InitVision();
#endif
    }

    void OnEnable()
    {
        cameraManager.frameReceived += OnCameraFrame;
    }

    void OnDisable()
    {
        cameraManager.frameReceived -= OnCameraFrame;
    }

    void OnCameraFrame(ARCameraFrameEventArgs args)
    {

#if UNITY_IOS && !UNITY_EDITOR

        if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
            return;

        var conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(0,0,image.width,image.height),
            outputDimensions = new Vector2Int(image.width,image.height),
            outputFormat = TextureFormat.RGBA32,
            transformation = XRCpuImage.Transformation.None
        };

        int size = image.GetConvertedDataSize(conversionParams);

        var buffer = new NativeArray<byte>(size, Allocator.Temp);

        image.Convert(conversionParams, buffer);

        byte[] managed = buffer.ToArray();

        buffer.Dispose();
        image.Dispose();

        GCHandle handle = GCHandle.Alloc(managed, GCHandleType.Pinned);

        ProcessVisionFrame(
            handle.AddrOfPinnedObject(),
            conversionParams.outputDimensions.x,
            conversionParams.outputDimensions.y
        );

        handle.Free();

#endif
    }
}