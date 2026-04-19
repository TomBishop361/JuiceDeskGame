using UnityEngine;
using static InputController;

[RequireComponent(typeof(InputController))]
public class PlayerAnimationHandler : MonoBehaviour
{
    [SerializeField] Animator animator;
    [SerializeField] InputController _controller;
    
    private void Reset()
    {
        animator = GetComponent<Animator>();
        _controller = GetComponent<InputController>();
    }

    private void Update()
    {
        UpdateAnimator();
    }

    private void UpdateAnimator()
    {
        if (!animator) return;

        //animator.SetBool("Grounded", isGrounded);
        animator.SetBool("Walking", _controller.state == MovementState.walking);
        animator.SetBool("Idle", _controller.state == MovementState.idle);
        animator.SetBool("Sprinting", _controller.state == MovementState.sprinting);
        //animator.SetBool("Crouching", state == MovementState.crouching);
        animator.SetBool("Sliding", _controller.state == MovementState.sliding);
        animator.SetBool("WallRunRight", _controller.state == MovementState.wallRunning && _controller.WallRunRight);
        animator.SetBool("WallRunLeft", _controller.state == MovementState.wallRunning && _controller.WallRunLeft);
        animator.SetBool("Air", _controller.state == MovementState.air);
        //animator.SetBool("RailGrinding", isRailGrinding);
        //animator.SetBool("Grappling", activeGrapple);

        //animator.SetInteger("State", (int)state);
    }
}
