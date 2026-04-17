using UnityEngine;

[RequireComponent(typeof(InputController))]
public class PlayerAnimationHandler : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] InputController controller;
    
    private void Reset()
    {
        animator = GetComponent<Animator>();
        controller = GetComponent<InputController>();
    }

    private void OnEnable()
    {
       // controller.OnStateChange += ChangeAnimationState;
    }

    void ChangeAnimationState(string state)
    {
        Debug.Log($"Animation state : {state}");
        animator.SetTrigger(state);
    }
}
