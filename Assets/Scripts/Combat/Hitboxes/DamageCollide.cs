using UnityEngine;

public class DamageCollide : MonoBehaviour
{
    public AttackData AttackData;

    private void OnTriggerEnter(Collider other)
    {
        IDamageable damageable;
        if (other.gameObject.TryGetComponent<IDamageable>(out damageable))
        {
            damageable.TakeDamage(AttackData);
        }
    }

   
}
