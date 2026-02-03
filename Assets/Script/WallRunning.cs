using System;
using System.Security.Cryptography;
using UnityEngine;

public class WallRunning : MonoBehaviour
{
    [Header("Referneces")]
    public LayerMask wallLayer;
    public LayerMask groundLayer;
    public float wallRunForce;
    public float wallRunTime;
    public float wallRunTimer;   

    [Header("Detection")]
    public float wallCheckDist =0.7f;
    public float minJumpHeight;
    private RaycastHit leftWallCheck;
    private RaycastHit rightWallCheck;
    private bool wallLeft;
    private bool wallRight;

    [Header("References")]
    public Transform orientation;
    [SerializeField] InputController controller;
    Vector2 moveDir;
    [SerializeField] Rigidbody rb;

    public event Action<bool> OnWallRunStart;
    

    private void OnEnable()
    {
       controller.InputManager.OnMoveReceived += MoveInput;
    }

    private void OnDisable()
    {
        controller.InputManager.OnMoveReceived -= MoveInput;
    }

    void MoveInput(Vector2 input)
    {
        moveDir = input;
    }


    void CheckForWall()
    {
        wallRight = Physics.Raycast(transform.position, orientation.right, out rightWallCheck, wallCheckDist, wallLayer);
        wallLeft = Physics.Raycast(transform.position, -orientation.right, out leftWallCheck, wallCheckDist, wallLayer);
        
    }

    private bool AboveGround()
    {
        return !Physics.Raycast(transform.position, Vector3.down, minJumpHeight, groundLayer);
    }


    private void StateMachine()
    {
        if ((wallLeft || wallRight) && moveDir.y > 0 && AboveGround())
        {
            //Start Wall Run
            if (!controller.wallRunning)
            {
                startWallRun();
            }
        }
        else
        {
            if (controller.wallRunning) StopWallRun();
        }
    }

    void startWallRun()
    {
        controller.wallRunning = true;
        OnWallRunStart?.Invoke(wallRight);
        
    }

    void StopWallRun()
    {
        controller.wallRunning = false;
        OnWallRunStart?.Invoke(false);
    }

    void wallRunMove()
    {
        rb.useGravity = false;
        rb.linearVelocity = new Vector3(rb.linearVelocity.x,0,rb.linearVelocity.z);

        Vector3 wallNoral = wallRight ? rightWallCheck.normal : leftWallCheck.normal;

        Vector3 wallForward = Vector3.Cross(wallNoral, transform.up);

        if((orientation.forward - wallForward).magnitude > (orientation.forward - -wallForward).magnitude)
        {
            wallForward =-wallForward;
        }

        rb.AddForce(wallForward * wallRunForce, ForceMode.Force);

        //Push to wall
        if (!(wallLeft && moveDir.x > 0) && !(wallRight && moveDir.x < 0))
        {
            rb.AddForce(-wallNoral * 100, ForceMode.Force);
        }
    }

    private void FixedUpdate()
    {
        if (controller.wallRunning) wallRunMove();
    }

    // Update is called once per frame
    void Update()
    {
     CheckForWall();
        StateMachine();
    }
}
