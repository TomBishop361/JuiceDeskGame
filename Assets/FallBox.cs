using UnityEngine;

public class FallBox : MonoBehaviour
{
    public Vector3 TeleportPos;

    private void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.tag == "Player")
        {
            other.transform.position = TeleportPos;
        }
    }
}
