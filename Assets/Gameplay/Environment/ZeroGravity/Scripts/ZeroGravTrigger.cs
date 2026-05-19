using Unity.Cinemachine;
using UnityEngine;

public class ZeroGravTrigger : MonoBehaviour
{

    float startGravityY;
    [Range(-10,0)]
    public float NewGrav = -5.11f;

    private void Start()
    {
        startGravityY = Physics.gravity.y;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) 
            Physics.gravity = Vector3.up * NewGrav;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            Physics.gravity = Vector3.up*startGravityY;
    }


}

