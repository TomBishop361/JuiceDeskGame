using System;
using System.Security.Cryptography;
using UnityEngine;
using Game.AI;

public class WallRunning : MonoBehaviour
{
    [Header("Referneces")]
    TimerManager _timerManager;
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
    int wallRunTimerID;


    private RaycastHit leftWallCheck;
    private RaycastHit rightWallCheck;

    public float SameWallTime =5;
    private int SameWallTimerID;

    public Transform LastWall;
    private bool wallLeft;
    private bool wallRight;
	[SerializeField] private float wallRunNoiseInterval = 0.4f;

	[Header("Wall Jumping")]
    public float wallJumpUpForce;
    public float wallJumpSideForce;

    private int wallRunExitTimerID;
    public float exitWallTime;
    
    bool exitingWall;  
   

    public event Action<bool> OnWallRunStart;
    public event Action OnWallRunEnd;
    
    public ForceMode forceMode;

	private PlayerNoiseEmitter noiseEmitter;
	private float nextWallRunNoiseTime;

	private void Awake() {
		noiseEmitter = GetComponent<PlayerNoiseEmitter>();
	}

	private void OnEnable()
    {
        _timerManager = TimerManager.instance;
        wallRunExitTimerID = _timerManager.NewTimer(exitWallTime, ExitTimerComplete, "Exit Wall Time");
        wallRunTimerID = _timerManager.NewTimer(wallRunTime, WallRunTimerComplete, "Wall Time");
        SameWallTimerID = _timerManager.NewTimer(SameWallTime, SameWallTimerComplete, "Same Wall Timer");

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
        controller.WallRunRight = wallRight;
        wallLeft = Physics.Raycast(transform.position, -orientation.right, out leftWallCheck, wallCheckDist, wallLayer);
        controller.WallRunLeft = wallLeft;
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

           // if (wallRunTimer > 0) wallRunTimer -= Time.deltaTime;

            //if(wallRunTimer <=0 && controller.wallRunning)
            //{
            //    exitingWall = true;
            //    // exitWallTimer = exitWallTime;
            //    _timerManager.RestartTimer(wallRunExitTimerID);
            //}

            

        }
        else if (exitingWall)
        {
            if (controller.wallRunning)
            {
                StopWallRun();
            }            
        }
        else
        {
            if (controller.wallRunning) StopWallRun();
        }
    }

    void ExitTimerComplete()
    {
        exitingWall = false;
    }

    void WallRunTimerComplete()
    {
        if (controller.wallRunning)
        {
            exitingWall = true;
            // exitWallTimer = exitWallTime;
            _timerManager.RestartTimer(wallRunExitTimerID);
        }
    }
    void SameWallTimerComplete()
    {
        LastWall = null;
    }

    

    void startWallRun()
    {
        Transform wall = wallRight ? rightWallCheck.transform : leftWallCheck.transform;
        if (wall != LastWall)
        {
           // wallRunTimer = wallRunTime;
           _timerManager.RestartTimer(wallRunTimerID);
        }
        else
        {
            _timerManager.SetTimerState(wallRunTimerID, true);
        }
            LastWall = wall;


        _timerManager.RestartTimer(SameWallTimerID);


        controller.wallRunning = true;
        OnWallRunStart?.Invoke(wallRight);

		noiseEmitter?.EmitWallRunNoise();
		nextWallRunNoiseTime = Time.time + wallRunNoiseInterval;
	}

    void StopWallRun()
    {
        _timerManager.SetTimerState(wallRunTimerID, false);
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

        // Pulsing noise on sustained wall runs
		if (controller.wallRunning && Time.time >= nextWallRunNoiseTime) {
			noiseEmitter?.EmitWallRunNoise(0.6f);
			nextWallRunNoiseTime = Time.time + wallRunNoiseInterval;
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
        if (!AboveGround())
        {
            LastWall = null;
            _timerManager.SetTimerState(SameWallTimerID, false);
        }
    }

    private void WallJump()
    {
        if (controller.wallRunning == false) return;
        exitingWall = true;
        //exitWallTimer = exitWallTime;
        _timerManager.RestartTimer(wallRunExitTimerID);

        Vector3 wallNormal = wallRight ? rightWallCheck.normal : leftWallCheck.normal;

        Vector3 forceToApply = transform.up * wallJumpUpForce + wallNormal * wallJumpSideForce;

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

        rb.AddForce(forceToApply, forceMode);

        
    }
}
