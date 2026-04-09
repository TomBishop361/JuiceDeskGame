using UnityEngine;

public class Door : MonoBehaviour
{
    public GameObject door;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            GetComponent<Animator>().SetTrigger("Open");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            GetComponent<Animator>().SetTrigger("Close");
        }
    }

}
