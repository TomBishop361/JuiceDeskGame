using UnityEngine;

public class DoorTrigger : MonoBehaviour
{
    [SerializeField] private AirlockDoorController door;
    [SerializeField] private string cameraTag = "MainCamera";

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(cameraTag))
        {
            door.OpenDoor();
        }
    }
}