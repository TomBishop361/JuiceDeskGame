
using UnityEngine;
using System;
using Game.AI;

public class Grapple : MonoBehaviour
{
    [Header("References")]
    public InputController controller;
    [SerializeField] InputManagerBase manager;
    IInputManager inputManager => manager.InputManager;

   [SerializeField] GrappleAnchorSelector selector;

    public Transform Camera;
    public Transform GrappleOrigin;
    public LayerMask Grappleable;
    public LineRenderer lineRenderer;

    public float overshootYAxis;

    [Header("Grapple")]
    bool grappling;
    
    public float grappleDelayTime;
    float grappleDelayTimer;
    [SerializeField] AnimationCurve AnimCurve;

    public Vector3 grapplePoint;

    [Header("Timer")]
    public float grapplingCoolDown;
    float grapplingCoolDownTimer;

    bool grappleHit;
    public GameObject BestGrappleAnchor;
    public GameObject CurrentGrappleAnchor;

    public event Action<Vector3> OnGrapple;
    public event Action OnGrappleEnd;

	private PlayerNoiseEmitter noiseEmitter;

	private void Awake() {
		noiseEmitter = GetComponent<PlayerNoiseEmitter>();
	}

	private void OnEnable()
    {
        inputManager.OnGrappleReceived += StartGrapple;
        selector.OnAnchorFound += setAnchorPoint;
    }

    private void OnDisable()
    {
        inputManager.OnGrappleReceived -= StartGrapple;
		selector.OnAnchorFound -= setAnchorPoint;
	}

    private void Update()
    {
        if (grapplingCoolDownTimer > 0) {
            grapplingCoolDownTimer -= Time.deltaTime;

        }

        //Delay Timer
        if (grappling && grappleDelayTimer > 0)
        {
            grappleDelayTimer -= Time.deltaTime;
            RopeAnim();
            if (grappleDelayTimer <= 0)
            {
                if (grappleHit) ExecuteGrapple();
                else StopGrapple();
            }
            
        }

    }

    void RopeAnim()
    {
        float t = ReMap.map(grappleDelayTimer, grappleDelayTime, 0, 0, 1);
        Vector3 position = Vector3.Lerp(lineRenderer.GetPosition(1), grapplePoint,t );
        float yDisplace = AnimCurve.Evaluate(t);
        position = new Vector3(position.x ,position.y+yDisplace,position.z);        
        lineRenderer.SetPosition(1, CurrentGrappleAnchor.transform.position);
    }

    void setAnchorPoint(AnchorPoint anchor)
    {
        if(anchor != null) BestGrappleAnchor = anchor.gameObject;
        else BestGrappleAnchor = null;  
    }

    private void FixedUpdate()
    {
        if (grappling)
        {
            lineRenderer.SetPosition(0, GrappleOrigin.position);

            // Better exit condition: If we are close to the point OR flying past it
            float distToPoint = Vector3.Distance(transform.position, grapplePoint);
            if (distToPoint < 1.5f)
            {
                StopGrapple();
            }
        }
    }

    //Edge case, IF grapple misses, then hits, Invoke StopGrapple Still calls
    void StartGrapple(bool value)
    {
        if (grapplingCoolDownTimer > 0 || grappling) return;
        CurrentGrappleAnchor = BestGrappleAnchor;
        if (CurrentGrappleAnchor == null) return;

        if (Vector3.Distance(CurrentGrappleAnchor.transform.position, transform.position) < 30)
        {
           
            if (Physics.Raycast(transform.position, (CurrentGrappleAnchor.transform.position - transform.position).normalized, 30,Grappleable))
            {
                return;
            }
             grappling = true;

            controller.freeze = true;
            grapplePoint = CurrentGrappleAnchor.transform.position - (Vector3.down* -2.5f) ;
            grappleDelayTimer = grappleDelayTime;
            grappleHit = true;
            lineRenderer.enabled = true;
            OnGrapple?.Invoke(grapplePoint);
			noiseEmitter?.EmitGrappleNoise();
		}
        
        //lineRenderer.SetPosition(1, grapplePoint);
    }

    void ExecuteGrapple()
    {
        // Cancel the "safety" stop timer if it exists
        CancelInvoke(nameof(StopGrapple));

        controller.freeze = false;
        Vector3 lowestPoint = transform.position; // Simplify reference

        float distance = Vector3.Distance(transform.position, grapplePoint);
        // Dynamic height: short grapples don't need a massive arc
        float dynamicHeight = Mathf.Clamp(distance * 0.5f, 2f, overshootYAxis);

        Vector3 velocity = JumpVelocityCalc.CalculateJumpVelocity(transform.position, grapplePoint, dynamicHeight);

        // Launch immediately rather than using Invoke
        controller.JumpToPosition(grapplePoint, dynamicHeight);

        // Safety timeout in case we never reach the point
        Invoke(nameof(StopGrapple), 2.0f);
    }

    public void StopGrapple()
    {
        if (!grappling) return;
        controller.freeze = false;
        grappling = false;

        lineRenderer.enabled = false;
        CancelInvoke(nameof(StopGrapple));
        controller.ResetRestrictions();
        controller.AnchorLaunch();
        OnGrappleEnd?.Invoke();
    }

}
