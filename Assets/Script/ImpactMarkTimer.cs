using UnityEngine;

public class ImpactMarkTimer : MonoBehaviour
{
    float lifetime = 0.5f;

    private void Update()
    {
        lifetime -= Time.deltaTime;
        if (lifetime < 0) Destroy(gameObject);
    }
}
