using UnityEngine;

public class CheckpointAnim : MonoBehaviour
{
    [SerializeField] public Animator anim;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                anim.SetTrigger("Activate");
            }
    }

}
