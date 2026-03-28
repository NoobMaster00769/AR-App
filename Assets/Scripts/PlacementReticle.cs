using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using System.Collections.Generic;

public class PlacementReticle : MonoBehaviour
{
    [Header("References")]
    public ARRaycastManager raycastManager;

    public GameObject reticleVisual;

    [Header("Settings")]
    public float rotationSpeed = 10f;

    public bool IsOnValidSurface { get; private set; } = false;
    public Pose CurrentPose { get; private set; }

    static readonly List<ARRaycastHit> _hits = new();

    Camera _cam;

    void Start()
    {
        _cam = Camera.main;
        Show(false); 
    }

    void Update()
    {
        UpdateReticle();
    }

    void UpdateReticle()
    {
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

            transform.position = pose.position;

            Quaternion targetRot = pose.rotation;
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRot,
                Time.deltaTime * rotationSpeed
            );

            Show(true);
        }
        else
        {
            Show(false);
        }
    }

   
    public void Show(bool visible)
    {
        if (reticleVisual != null)
            reticleVisual.SetActive(visible);
    }
}