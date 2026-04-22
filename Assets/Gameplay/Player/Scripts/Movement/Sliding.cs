using UnityEngine;
using UnityEngine.Rendering;

public class Sliding : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform orientation;
    [SerializeField] CapsuleCollider _capsuleCollider;
    [SerializeField] Rigidbody rb;
    [SerializeField]private InputController controller;

    [Header("Sliding")]
    [SerializeField] float maxSlideTime;
    [SerializeField] float SlideDownforce;
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
        startYScale = _capsuleCollider.height;   
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
        //Adds down force when sliding
        rb.AddForce(Vector3.down * SlideDownforce, ForceMode.Force);
    }

    void StartSlide()
    {
        
        controller.sliding = true;
        //Animator Call

        _capsuleCollider.height = slideYScale;
        _capsuleCollider.center = Vector3.up * -0.35f;

        rb.AddForce(Vector3.down, ForceMode.Impulse);


        slideTimer = maxSlideTime;
    }

    void StopSlide()
    {
        if (!controller.sliding) return;

        controller.sliding = false;
        //Animator Call
        _capsuleCollider.center = Vector3.up * 0.15f;
        
        _capsuleCollider.height = startYScale;

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
