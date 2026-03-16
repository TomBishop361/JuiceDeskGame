using UnityEngine;

public class FallingDeathTeleportScript : MonoBehaviour
{
    public GameObject Player;
    public GameObject TeleportSpot;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    private void OnTriggerEnter(Collider collision)
    {
        Player.transform.position = TeleportSpot.transform.position;
    }
}
