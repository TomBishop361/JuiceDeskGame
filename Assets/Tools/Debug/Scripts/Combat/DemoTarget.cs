using UnityEngine;

public class DemoTarget : MonoBehaviour, IDamageable
{
    [SerializeField] private ParticleSystem particleEffect;
    [SerializeField] private AudioSource glassSound;

    public void TakeDamage(AttackData attackData)
    {
        GlassOnDestroy();
    }

    private void GlassOnDestroy()
    {
        if (particleEffect != null)
        {
            particleEffect.Play();
        }

        if (glassSound != null)
        {
            glassSound.PlayOneShot(glassSound.clip);
        }

        Destroy(gameObject);
    }
}