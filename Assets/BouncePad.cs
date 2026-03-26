using UnityEngine;

public class BouncePad : MonoBehaviour
{
    public float bounceDelay;
    float bounceDelayTimer;
    public float bounceAmount;

    private void OnTriggerEnter(Collider other)
    {
        //
        bounceDelayTimer = bounceDelay;

    }

    private void OnTriggerStay(Collider other)
    {
        Rigidbody rb;
        if (other.TryGetComponent<Rigidbody>(out rb)){            
            bounceDelayTimer -= Time.deltaTime;
            if (bounceDelayTimer <= 0)
            {                
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, bounceAmount, rb.linearVelocity.z);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        
    }


}
