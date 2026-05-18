using UnityEngine;
using UnityEngine.UIElements;

public class GrappleIndicator : MonoBehaviour
{
    public Transform player;
    public Vector3 targetPosition;
    public RectTransform UIIndicator;


    void Update()
    {
        // 1. Get direction from player to target
        Vector3 targetDir = targetPosition - player.position;

        
        Vector3 localDir = player.InverseTransformDirection(targetDir);
        localDir.y = 0; // Ignore height differences

        // 3. Calculate the angle in degrees
        float angle = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;


        UIIndicator.localRotation = Quaternion.Euler(0, 0, -angle);
    }
}
