using UnityEngine;

public class FallBox : MonoBehaviour
{
    public Vector3 TeleportPos;

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("FALLBOX");
        if(other.gameObject.tag == "Player")
        {
            other.transform.position = TeleportPos;
        }
    }
}
