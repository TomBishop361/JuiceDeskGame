using UnityEngine;

public class DemoTarget : MonoBehaviour, IDamageable
{
    [SerializeField] private GameObject particleEffect;

    public void adjustHealth(int damage)
    {
        OnDestroy();
    }

    // Needed to implement interface
	public void TakeDamage(AttackData attackData) {
        // Configure Attack data for sword enemy

        OnDestroy();


    }

    private void OnDestroy() 
    {
        if (particleEffect != null)
        {
            Instantiate(particleEffect, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}
