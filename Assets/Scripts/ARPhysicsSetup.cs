
using UnityEngine;

public class ARPhysicsSetup : MonoBehaviour
{
    void Awake()
    {
        Physics.gravity                         = new Vector3(0f, -9.81f, 0f);
        Physics.defaultSolverIterations         = 12;
        Physics.defaultSolverVelocityIterations = 6;
        Physics.bounceThreshold                 = 0.5f;
        Physics.sleepThreshold                  = 0.005f;
    }
}