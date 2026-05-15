using UnityEngine;

public class DemoTarget : MonoBehaviour, IDamageable
{
    [SerializeField] private GameObject particleEffect;

    public void adjustHealth(int damage)
    {
        GlassOnDestroy();
    }

    // Needed to implement interface
	public void TakeDamage(AttackData attackData) {
        // Configure Attack data for sword enemy

        GlassOnDestroy();


    }

    private void GlassOnDestroy()
    {
        if (particleEffect != null)
        {
            Instantiate(particleEffect, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}
