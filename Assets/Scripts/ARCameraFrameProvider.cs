using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.Collections;

[RequireComponent(typeof(ARCameraManager))]
public class ARCameraFrameProvider : MonoBehaviour
{
    ARCameraManager cameraManager;
    public Texture2D cameraTexture;

    public int modelInputSize = 640;

    void Awake()
    {
        cameraManager = GetComponentInChildren<ARCameraManager>();
    }

    void OnEnable()
    {
        cameraManager.frameReceived += OnFrameReceived;
    }

    void OnDisable()
    {
        cameraManager.frameReceived -= OnFrameReceived;
    }

    void OnFrameReceived(ARCameraFrameEventArgs args)
    {
        if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
            return;

        var conversionParams = new XRCpuImage.ConversionParams
        {
            inputRect = new RectInt(0, 0, image.width, image.height),
            outputDimensions = new Vector2Int(modelInputSize, modelInputSize),
            outputFormat = TextureFormat.RGB24,
            transformation = XRCpuImage.Transformation.MirrorY
        };

        if (cameraTexture == null)
            cameraTexture = new Texture2D(
                modelInputSize,
                modelInputSize,
                TextureFormat.RGB24,
                false);

        var rawData = cameraTexture.GetRawTextureData<byte>();
        image.Convert(conversionParams, rawData);
        cameraTexture.Apply();

        image.Dispose();
    }
}