using UnityEngine;

public class GravityZone : MonoBehaviour
{
    public Vector3 gravity = new Vector3(0, -9.81f, 0); // default gravity
    public Vector3 newGravity = new Vector3(0, -2f, 0); // low gravity

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Physics.gravity = newGravity;
            Debug.Log("Gravity changed to: " + newGravity);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Physics.gravity = gravity;
            Debug.Log("Gravity reset to: " + gravity);
        }
    }
}