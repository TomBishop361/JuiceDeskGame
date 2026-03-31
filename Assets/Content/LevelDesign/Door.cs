using UnityEngine;

public class Door : MonoBehaviour
{
    public GameObject door;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            door.SetActive(false);
        }
    }

}
