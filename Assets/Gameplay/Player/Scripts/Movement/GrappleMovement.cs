using System.ComponentModel;
using UnityEngine;
[RequireComponent(typeof(Grapple),typeof(GrappleAnchorSelector))]
public class GrappleMovement : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private InputController mainController;
     Rigidbody rb { get => mainController._rb; set {} } 

    [Header("Grappling Settings")]
    [SerializeField] private float grappleSpeedBoost = 1.5f;
    [SerializeField] private float grapplePullForce = 20f;
    [SerializeField] private float anchorLaunchAmount = 25f;

    private Vector3 grappleTargetPos;
    private Vector3 velocityToSet;

    public bool IsActive { 
        get => mainController.activeGrapple;
        set => mainController.activeGrapple = value;
    }
    private bool enableMoveOnNextTouch
    {
        get => mainController.enableMoveOnNextTouch;
        set => mainController.enableMoveOnNextTouch = value;
    }

    public void JumpToPosition(Vector3 targetPos, float trajectoryHeight)
    {
        IsActive = true;
        rb.linearDamping = 0;
        grappleTargetPos = targetPos;

        Vector3 pullDir = (grappleTargetPos - transform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(pullDir - (Vector3.up * pullDir.y), Vector3.up);
        transform.rotation = targetRotation;

        
        velocityToSet = JumpVelocityCalc.CalculateJumpVelocity(transform.position, targetPos, trajectoryHeight);

        Invoke(nameof(SetVelocity), 0.05f);
    }

    private void SetVelocity()
    {
        rb.linearVelocity = velocityToSet;
        enableMoveOnNextTouch = true;

        // Hand off the new target speed back to the main controller safely
        mainController.OverrideMoveSpeed(mainController.sprintSpeed * grappleSpeedBoost);
    }

    public void HandleGrappleMovement()
    {
        if (!IsActive) return;

        Vector3 dirToTarget = (grappleTargetPos - transform.position).normalized;
        dirToTarget.y = 0;
        if (dirToTarget != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dirToTarget), 15 * Time.deltaTime);
        }
    }

    public void AnchorLaunch(Transform cameraTransform)
    {
        Vector3 launchDir = cameraTransform.forward;
        launchDir.y = 0f;
        rb.AddForce(launchDir.normalized * anchorLaunchAmount, ForceMode.VelocityChange);

        mainController.OverrideMoveSpeed(mainController.sprintSpeed);
    }

    private void FixedUpdate()
    {
        HandleGrappleMovement();
    }

    public void ProcessCollision()
    {
        if (enableMoveOnNextTouch)
        {
            enableMoveOnNextTouch = false;
            StopGrapple();
        }
    }

    public void StopGrapple()
    {
        IsActive = false;
        rb.linearDamping = 0.75f;
    }
}
