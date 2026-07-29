using UnityEngine;

public class CheckpointAnim : MonoBehaviour
{
    [SerializeField] public Animator anim;
    [SerializeField] private AudioSource audioSource;
    private bool isActivated = false;

    private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                anim.SetTrigger("Activate");

            if (isActivated == false)
            {
                audioSource.Play();
            }

            isActivated = true;                                    
            }
    }
}
