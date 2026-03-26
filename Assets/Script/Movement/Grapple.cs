using Unity.VisualScripting;
using UnityEngine;

public class Grapple : MonoBehaviour
{
    [Header("References")]
    public InputController controller;
    [SerializeField] InputManagerBase manager;
    IInputManager inputManager => manager.InputManager;

    public Transform Camera;
    public Transform GrappleOrigin;
    public LayerMask Grappleable;
    public LineRenderer lineRenderer;

    public float overshootYAxis;

    [Header("Grapple")]
    bool grappling;
    public float maxGrappleDist;
    public float grappleDelayTime;
    float grappleDelayTimer;
    [SerializeField] AnimationCurve AnimCurve;

    public Vector3 grapplePoint;

    [Header("Timer")]
    public float grapplingCoolDown;
    float grapplingCoolDownTimer;

    bool grappleHit;

    //TEST 
    public GameObject GrappleAnchor;

    private void OnEnable()
    {
        inputManager.OnGrappleReceived += StartGrapple;
    }

    private void OnDisable()
    {
        inputManager.OnGrappleReceived -= StartGrapple;
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
        lineRenderer.SetPosition(1, GrappleAnchor.transform.position);
    }

    

    private void FixedUpdate()
    {
        if(grappling) lineRenderer.SetPosition(0,GrappleOrigin.position);
    }

    //Edge case, IF grapple misses, then hits, Invoke StopGrapple Still calls
    void StartGrapple(bool value)
    {
        if(grapplingCoolDownTimer > 0 || grappling) return;
        grappling = true;

        controller.freeze = true;

        //RaycastHit hit;
        //if(Physics.Raycast(Camera.position, Camera.forward, out hit, maxGrappleDist, Grappleable)){
        //    grapplePoint = hit.point;

        //    grappleDelayTimer = grappleDelayTime;
        //    grappleHit = true;
        //}
        //else
        //{
        //    grapplePoint = Camera.position + Camera.forward * maxGrappleDist;

        //    grappleDelayTimer = grappleDelayTime;
        //    grappleHit = false;
        //}


        //Ray cast , if no hit then clear path
       // launch player after graple complete
       // idfk
       // make grapple selector script
        grapplePoint = GrappleAnchor.transform.position - (Vector3.down*-1)*4;
        grappleDelayTimer = grappleDelayTime;
        grappleHit = true;
        lineRenderer.enabled = true;
        //lineRenderer.SetPosition(1, grapplePoint);
    }

    void ExecuteGrapple()
    {
        controller.freeze = false;

        Vector3 lowestPoint = new Vector3(transform.position.x,transform.position.y -1,transform.position.z);

        float grapplePointRelativeY = grapplePoint.y - lowestPoint.y;
        float highestPointOnArc =  grapplePointRelativeY + overshootYAxis;

        if(grapplePointRelativeY < 0) highestPointOnArc = overshootYAxis;

        controller.JumpToPosition(grapplePoint, highestPointOnArc);
        Invoke(nameof(StopGrapple), 1.1f);
    }

    public void StopGrapple()
    {
        controller.freeze = false;
        grappling = false;


        grapplingCoolDownTimer = grapplingCoolDown;

        lineRenderer.enabled = false;

        controller.ResetRestrictions();
        
    }
}
