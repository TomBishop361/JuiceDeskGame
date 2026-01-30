using UnityEngine;

public class CameraTargetTracking : MonoBehaviour
{
    [SerializeField] Transform playerBody;


    private void Update()
    {
        transform.position = playerBody.position;
    }
}
