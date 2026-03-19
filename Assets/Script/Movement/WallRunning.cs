using System;
using System.Security.Cryptography;
using UnityEngine;

public class WallRunning : MonoBehaviour
{
    [Header("Referneces")]
    public LayerMask wallLayer;
    public LayerMask groundLayer;
    public float wallRunForce;
    public Transform orientation;
    [SerializeField] InputController controller;
    Vector2 moveDir;
    bool jump;
    [SerializeField] Rigidbody rb;

    [Header("Wall Run")]
    public float wallCheckDist =0.7f;
    public float minJumpHeight;
    public float wallRunTime;
    float wallRunTimer;
    private RaycastHit leftWallCheck;
    private RaycastHit rightWallCheck;
    public float SameWallTime =5;
    float sameWallTimer;
    public Transform LastWall;
    private bool wallLeft;
    private bool wallRight;

    [Header("Wall Jumping")]
    public float wallJumpUpForce;
    public float wallJumpSideForce;
    public float exitWallTime;
    float exitWallTimer;
    bool exitingWall;  
   

    public event Action<bool> OnWallRunStart;
    public event Action OnWallRunEnd;
    
    public ForceMode forceMode;
    private void OnEnable()
    {
       controller.InputManager.OnMoveReceived += MoveInput;
        controller.InputManager.OnJumpReceived += JumpInput;

    }

    private void OnDisable()
    {
        controller.InputManager.OnMoveReceived -= MoveInput;
        controller.InputManager.OnJumpReceived -= JumpInput;
    }

    void MoveInput(Vector2 input)
    {
        moveDir = input;
    }

    void JumpInput(bool input)
    {
        if (input)
        {
            WallJump();
        }
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
        if ((wallLeft || wallRight) && moveDir.y > 0 && AboveGround() && !exitingWall)
        {
            //Start Wall Run
            if (!controller.wallRunning) startWallRun();

            if (wallRunTimer > 0) wallRunTimer -= Time.deltaTime;

            if(wallRunTimer <=0 && controller.wallRunning)
            {
                exitingWall = true;
                exitWallTimer = exitWallTime;
            }

            

        }
        else if (exitingWall)
        {
            if (controller.wallRunning)
            {
                StopWallRun();
            }
            if(exitWallTimer > 0)
            {
                exitWallTimer -= +Time.deltaTime;
            }
            if(exitWallTimer <= 0)
            {
                exitingWall = false;
                
            }
        }
        else
        {
            if (controller.wallRunning) StopWallRun();
        }
    }
    void lastWallTimeCounter()
    {
        if(sameWallTimer <= 0 || !AboveGround())
        {
            LastWall = null;
        }
        else
        {
            sameWallTimer -= Time.deltaTime;    
        }
    }

    void startWallRun()
    {
        Transform wall = wallRight ? rightWallCheck.transform : leftWallCheck.transform;
        if (wall != LastWall)
        {
            wallRunTimer = wallRunTime;
        }        
        LastWall = wall;
        sameWallTimer = SameWallTime;
        controller.wallRunning = true;
        OnWallRunStart?.Invoke(wallRight);
            
        
    }

    void StopWallRun()
    {
        controller.wallRunning = false;
        OnWallRunEnd?.Invoke();
        //OnWallRunStart?.Invoke(false);
    }

    void wallRunMove()
    {
        if (controller.activeGrapple) return;
        rb.useGravity = false;
        rb.linearVelocity = new Vector3(rb.linearVelocity.x,0,rb.linearVelocity.z);

        Vector3 wallNoral = wallRight ? rightWallCheck.normal : leftWallCheck.normal;
        
        Vector3 wallForward = Vector3.Cross(wallNoral, transform.up);

        if((orientation.forward - wallForward).magnitude > (orientation.forward - -wallForward).magnitude)
        {
            wallForward =-wallForward;
        }

        //Rotate Orientation to meet wall
        orientation.transform.rotation = Quaternion.LookRotation(wallForward);

        rb.AddForce(wallForward * wallRunForce, ForceMode.Force);

        //Push to wall
        if (!(wallLeft && moveDir.x > 0) && !(wallRight && moveDir.x < 0))
        {
            rb.AddForce(-wallNoral * 200, ForceMode.Force);
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
        lastWallTimeCounter();
    }

    private void WallJump()
    {
        if (controller.wallRunning == false) return;
        exitingWall = true;
        exitWallTimer = exitWallTime;

        Vector3 wallNormal = wallRight ? rightWallCheck.normal : leftWallCheck.normal;

        Vector3 forceToApply = transform.up * wallJumpUpForce + wallNormal * wallJumpSideForce;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

        rb.AddForce(forceToApply, forceMode);

        
    }
}
