using UnityEngine;

public class DemoTarget : MonoBehaviour, IDamageable
{
    public void adjustHealth(int damage)
    {
       Destroy(gameObject);
    }

    
}
