using UnityEngine;

public class DemoTarget : MonoBehaviour, IDamageable {
    [SerializeField] private GameObject particleEffect;

    public void TakeDamage(AttackData attackData) {
        GlassOnDestroy();
    }

    private void GlassOnDestroy() {
        if (particleEffect != null) {
            Instantiate(particleEffect, transform.position, Quaternion.identity);
        }
        Destroy(gameObject);
    }
}
