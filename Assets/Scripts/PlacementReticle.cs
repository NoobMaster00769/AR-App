using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

/// <summary>
/// Attach this to an empty GameObject called "PlacementReticle".
/// It follows AR plane hits and shows a visual ring indicator.
/// When no plane is found it hides itself.
/// 
/// SETUP:
///   1. Create an empty GameObject → name it "PlacementReticle"
///   2. Attach this script
///   3. Assign the ARRaycastManager from XR Origin in the Inspector
///   4. Assign a Reticle Visual (see note below)
///
/// RETICLE VISUAL:
///   - Create a Quad child object under PlacementReticle
///   - Scale it to (0.3, 0.3, 0.3)
///   - Apply the built-in "AR Feathered Plane" material or any ring texture
///   - Assign that child GameObject to reticleVisual below
///   OR: Import the AR Foundation Samples and use their reticle prefab
/// </summary>
public class PlacementReticle : MonoBehaviour
{
    [Header("References")]
    public ARRaycastManager raycastManager;

    [Tooltip("The child GameObject that holds the visual ring mesh/sprite")]
    public GameObject reticleVisual;

    [Header("Settings")]
    [Tooltip("How fast the reticle rotates to match the surface normal")]
    public float rotationSpeed = 10f;

    // Whether a valid surface is currently under the screen center
    public bool IsOnValidSurface { get; private set; } = false;

    // The world-space pose of the current valid surface hit
    public Pose CurrentPose { get; private set; }

    static readonly List<ARRaycastHit> _hits = new();

    Camera _cam;

    void Start()
    {
        _cam = Camera.main;

        // Hide until we find a surface
        SetVisible(false);
    }

    void Update()
    {
        UpdateReticle();
    }

    void UpdateReticle()
    {
        // Cast a ray from screen center
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        bool hit = raycastManager.Raycast(
            screenCenter,
            _hits,
            TrackableType.PlaneWithinPolygon
        );

        IsOnValidSurface = hit;

        if (hit)
        {
            Pose pose = _hits[0].pose;
            CurrentPose = pose;

            // Snap position
            transform.position = pose.position;

            // Smoothly rotate to match plane normal (Y-up alignment)
            Quaternion targetRot = pose.rotation;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                Time.deltaTime * rotationSpeed
            );

            SetVisible(true);
        }
        else
        {
            SetVisible(false);
        }
    }

    void SetVisible(bool visible)
    {
        if (reticleVisual != null)
            reticleVisual.SetActive(visible);
    }
}