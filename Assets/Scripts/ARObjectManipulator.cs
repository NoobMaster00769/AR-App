using UnityEngine;

public class ARObjectManipulator : MonoBehaviour
{
    Camera cam;
    bool isDragging = false;

    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        if (Input.touchCount == 1)
        {
            MoveObject(Input.GetTouch(0));
        }
        else if (Input.touchCount == 2)
        {
            ScaleAndRotate();
        }
    }

    void MoveObject(Touch touch)
    {
        Ray ray = cam.ScreenPointToRay(touch.position);

        if (touch.phase == TouchPhase.Began)
        {
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.transform == transform)
                    isDragging = true;
            }
        }

        if (touch.phase == TouchPhase.Moved && isDragging)
        {
            Vector3 pos =
                cam.transform.position +
                cam.transform.forward * 1.2f;

            transform.position = pos;
        }

        if (touch.phase == TouchPhase.Ended)
        {
            isDragging = false;
        }
    }

    void ScaleAndRotate()
    {
        Touch t1 = Input.GetTouch(0);
        Touch t2 = Input.GetTouch(1);

        // SCALE
        float prevDist = (t1.position - t1.deltaPosition -
                          (t2.position - t2.deltaPosition)).magnitude;

        float currDist = (t1.position - t2.position).magnitude;

        float scaleFactor = currDist / prevDist;

        transform.localScale *= scaleFactor;

        // ROTATE
        Vector2 prevDir = (t1.position - t1.deltaPosition) -
                          (t2.position - t2.deltaPosition);

        Vector2 currDir = t1.position - t2.position;

        float angle = Vector2.SignedAngle(prevDir, currDir);

        transform.Rotate(Vector3.up, angle);
    }
}