using UnityEngine;

public class DemoTarget : MonoBehaviour, IDamageable
{
    public void adjustHealth(int damage)
    {
       Destroy(gameObject);
    }

    // Needed to implement interface
	public void TakeDamage(AttackData attackData) {
		// Configure Attack data for sword enemy
		
        Destroy(gameObject);
    }
}
