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

    private void OnEnable()
    {
        controller.InputManager.OnSlideReceived += SlideInput;
        controller.InputManager.OnMoveReceived += MoveInput;
        startYScale = transform.localScale.y;   
    }

    private void OnDisable()
    {
        controller.InputManager.OnSlideReceived -= SlideInput;
        controller.InputManager.OnMoveReceived -= MoveInput;
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
        Vector3 slideDirection = orientation.forward * moveDir.y + orientation.right * moveDir.x;

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
        Debug.Log("Slide Start");
        controller.sliding = true;
        transform.localScale = new Vector3(transform.localScale.x, slideYScale, transform.localScale.z);
        rb.AddForce(Vector3.down, ForceMode.Impulse);


        slideTimer = maxSlideTime;
    }

    void StopSlide()
    {
        Debug.Log("Slide STOP");
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

    // Update is called once per frame
    void Update()
    {
        if (slideInput==1 && moveDir != Vector2.zero)
        {
            slideInput = -1;
            StartSlide();
        } 
        if(slideInput==0 && controller.sliding)
        {
            slideInput = -1;
            StopSlide();
        }
    }
}
