using UnityEngine;

public class Throwable : MonoBehaviour
{
    Rigidbody rb;
    Camera cam;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        cam = Camera.main;
    }

    void OnMouseDown()
    {
        Throw();
    }

    void Throw()
{
    if (rb == null) return;

    // 🔓 enable physics ONLY when throwing
    rb.isKinematic = false;
    rb.useGravity = true;

    Vector3 force =
        cam.transform.forward * 4f +
        cam.transform.up * 1.5f;

    rb.AddForce(force, ForceMode.Impulse);
}
}