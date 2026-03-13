using UnityEngine;

public class DamageCollide : MonoBehaviour
{
    public AttackData AttackData;

    private void OnCollisionEnter(Collision collision)
    {
        collision.gameObject.GetComponent<IDamageable>().TakeDamage(AttackData);
    }
}
