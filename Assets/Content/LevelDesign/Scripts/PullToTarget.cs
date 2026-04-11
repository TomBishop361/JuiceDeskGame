using UnityEngine;

public class PullToTarget : MonoBehaviour
{
    public Transform target;
    public float pullSpeed = 5f; 

    private bool isPulling = false;
    private Transform player;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPulling = true;
            player = other.transform;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPulling = false;
            player = null;
        }
    }

    void FixedUpdate()
    {
        if (isPulling && player != null)
        {
            player.position = Vector3.MoveTowards(player.position, target.position, pullSpeed * Time.fixedDeltaTime);
        }
    }
}