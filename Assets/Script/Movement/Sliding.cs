using UnityEngine;
using UnityEngine.Rendering;

public class Sliding : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform orientation;
    [SerializeField] Transform playerObj;
    [SerializeField] Rigidbody rb;
    [SerializeField]private InputController controller;

    [Header("Sliding")]
    [SerializeField] float maxSlideTime;
    public float slideForce;
    private float slideTimer;
    //bool isSliding;

    [Header("Scaling")]
    public float slideYScale;
    float startYScale;

    Vector2 moveDir;
    float slideInput;
    //bool jump;

    private void OnEnable()
    {
        controller.InputManager.OnSlideReceived += SlideInput;
        controller.InputManager.OnMoveReceived += MoveInput;
        controller.JumpEvent += jumpListener;
        startYScale = transform.localScale.y;   
    }

    private void OnDisable()
    {
        controller.InputManager.OnSlideReceived -= SlideInput;
        controller.InputManager.OnMoveReceived -= MoveInput;
        controller.JumpEvent -= jumpListener;
    }

    void SlideInput(float val)
    {
        
        slideInput = val;
        Debug.Log(slideInput + "Slide INPUT ");
    }
    void MoveInput(Vector2 input)
    {
        moveDir = input;
    }
    

    void SlidingMovement()
    {
        Vector3 flatOrientationForward = new Vector3(orientation.forward.x, 0, orientation.forward.z);
        Vector3 slideDirection = flatOrientationForward * moveDir.y + orientation.right * moveDir.x;

        if (!controller.OnSlope() || rb.linearVelocity.y > -0.1f)
        {
            rb.AddForce(slideDirection.normalized * slideForce, ForceMode.Force);

            slideTimer -= Time.deltaTime;            
        }
        else
        {
            rb.AddForce(controller.GetSlopeMoveDirection(slideDirection) * slideForce, ForceMode.Force);
        }

        if (slideTimer <= 0)
        {
            StopSlide();
        }
    }

    void StartSlide()
    {
        
        controller.sliding = true;
        transform.localScale = new Vector3(transform.localScale.x, slideYScale, transform.localScale.z);
        rb.AddForce(Vector3.down, ForceMode.Impulse);


        slideTimer = maxSlideTime;
    }

    void StopSlide()
    {
        if (!controller.sliding) return;

        controller.sliding = false;
        transform.localScale = new Vector3(transform.localScale.x, startYScale, transform.localScale.z);
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    private void FixedUpdate()
    {
        if (controller.sliding)
        {
            SlidingMovement();
        }
    }
    void jumpListener()
    {
        StopSlide();
    }

    bool shouldStopSlide()
    {
        return controller.wallRunning ||
        controller.IsJumpReady == false || // jumping
        slideInput == 0;
    }

    // Update is called once per frame
    void Update()
    {
        if (slideInput==1 && moveDir != Vector2.zero && controller._isGrounded)
        {
            slideInput = -1;
            StartSlide();
        } 
        if(controller.sliding && shouldStopSlide())
        {
            slideInput = -1;
            StopSlide();
        }
       
    }
}
