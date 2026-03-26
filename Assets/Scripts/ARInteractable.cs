using UnityEngine;

public class ARInteractable : MonoBehaviour
{
    Rigidbody rb;
    Camera cam;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        cam = Camera.main;
    }

    void Update()
    {
        if (Input.touchCount == 0) return;

        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            Ray ray = cam.ScreenPointToRay(touch.position);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.transform == transform)
                {
                    Throw();
                }
            }
        }
    }

    void Throw()
    {
        if (rb == null) return;

        rb.isKinematic = false;
        rb.useGravity = true;

        Vector3 force =
            cam.transform.forward * 4f +
            cam.transform.up * 1.5f;

        rb.AddForce(force, ForceMode.Impulse);
    }
}