using UnityEngine;

public class moveCamLate : MonoBehaviour
{
    [SerializeField] Transform Target;
    [SerializeField] int followSpeed = 1;
    [SerializeField] Vector3 offset;
    void LateUpdate()
    {
        Vector3 targetPosition = Target.position + offset;
        transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
    }
}
