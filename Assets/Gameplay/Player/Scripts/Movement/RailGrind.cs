using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class RailGrind : MonoBehaviour
{
    [SerializeField] InputController controller;
    [Header("Grinding")]

    [SerializeField] Transform cameraTransform;
    //Rail
    public bool onRail;
    public bool canRailGrind = true;

    public float railGrindTime = 1;
    float railGrindTimer;
    private int RailtimerID;

    public float railBoost;

    [SerializeField] float grindSpeed;
    [SerializeField] float heightOffset; // playerheight/2
    float timeForFullSpline;
    float elapsdTime;
    [SerializeField] RailScript currentRailScript;
    

    [SerializeField] GameObject SparkVFX;

    bool isRailGrinding {
        get => controller.isRailGrinding;
        set => controller.isRailGrinding = value;
    }

    TimerManager _timerManager;

    private void OnEnable()
    {
        EventManager.instance.Subscribe("OnDamage", damageHandler);
        //if(_health != null)
        //{
        //    _health.OnDamageDealt += throwOffRail;
        //}

        _timerManager = TimerManager.instance;
        RailtimerID = _timerManager.NewTimer(railGrindTime, RailTimerEnd, "RailGrind Timer");
        controller.InputManager.OnJumpReceived += JumpInput;
    }

    private void OnDisable()
    {
        EventManager.instance.Unsubscribe("OnDamage", damageHandler);
        //if (_health != null)
        //{
        //    _health.OnDamageDealt -= throwOffRail;
        //}
        controller.InputManager.OnJumpReceived -= JumpInput;
    }
    void JumpInput(bool input)
    {
        if (input)
        {
           throwOffRail();
        }
    }

    void damageHandler(object data)
    {
        throwOffRail();
    }

    private void FixedUpdate()
    {
        
        if (railGrindTimer > 0)
        {
            railGrindTimer -= Time.deltaTime;
            if (railGrindTimer <= 0)
            {
                canRailGrind = true;
            }
        }
       
        movePlayerAlongRail();
    }

    private void movePlayerAlongRail()
    {
        if (!isRailGrinding || currentRailScript == null || !onRail) return;

        float progess = elapsdTime / timeForFullSpline;
        if (progess < 0 || progess > 1)
        {
            throwOffRail();
            return;
        }

        // 1. Calculate current and next positions exactly like your smooth version
        float nextTime;
        if (currentRailScript.normalDir)
            nextTime = (elapsdTime + Time.fixedDeltaTime);
        else
            nextTime = (elapsdTime - Time.fixedDeltaTime);

        float3 pos, tan, up;
        float3 nextPosFloat, nextTan, nextUp;
        SplineUtility.Evaluate(currentRailScript.railSpline.Spline, progess, out pos, out tan, out up);
        SplineUtility.Evaluate(currentRailScript.railSpline.Spline, nextTime / timeForFullSpline, out nextPosFloat, out nextTan, out nextUp);

        Vector3 worldPos = currentRailScript.LocalToWorldConversion(pos);
        Vector3 nextPos = currentRailScript.LocalToWorldConversion(nextPosFloat);
        Vector3 worldUp = currentRailScript.transform.TransformDirection(up);

        //The Target: where the player SHOULD be
        Vector3 targetSnapPos = worldPos + (worldUp * heightOffset);

        //correct the velocity        
        Vector3 correctionDir = (targetSnapPos - transform.position);

        //keep smooth forward velocity
        Vector3 forwardVel = (nextPos - worldPos).normalized * grindSpeed;

        //add the correction but multiply it to make it "snappy" without jitter        
        controller._rb.linearVelocity = forwardVel + (correctionDir * 0.75f);

        //Rotation
        Vector3 horizontalView = new Vector3(controller._rb.linearVelocity.x, 0, controller._rb.linearVelocity.z);

        Quaternion targetRotation = Quaternion.LookRotation(horizontalView, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10 * Time.deltaTime);

        //Update Time
        if (currentRailScript.normalDir)
            elapsdTime += Time.fixedDeltaTime;
        else
            elapsdTime -= Time.fixedDeltaTime;

        EventManager.instance.Invoke("OnRailGrind");
    }

    void RailTimerEnd()
    {
        canRailGrind = true;
    }

    private void OnCollisionEnter(Collision collision)
    {
        
        if (collision.gameObject.tag == "Rail" && !isRailGrinding && canRailGrind)
        {
            
            isRailGrinding = true;
            onRail = true;

            currentRailScript = collision.gameObject.GetComponent<RailScript>();
            CalculateAndSetRailPosition();

            
        }
    }

    private void CalculateAndSetRailPosition()
    {
        timeForFullSpline = currentRailScript.totalSplineLength / grindSpeed;
        Vector3 splinePoint;
        float normalisedTime = currentRailScript.calclulateTargetRailPoint(transform.position, out splinePoint);
        elapsdTime = timeForFullSpline * normalisedTime;
        float3 pos, forward, up;
        SplineUtility.Evaluate(currentRailScript.railSpline.Spline, normalisedTime, out pos, out forward, out up);
        currentRailScript.CalculateDirection(forward, transform.forward);
        transform.position = splinePoint + (transform.up * heightOffset);

        SparkVFX.SetActive(true);
    }

    void throwOffRail()
    {
        if (isRailGrinding)
        {
            isRailGrinding = false;
            onRail = false;
            controller._rb.useGravity = true;
            currentRailScript = null;            
            canRailGrind = false;
            railGrindTimer = railGrindTime;
            _timerManager.RestartTimer(RailtimerID);
            SparkVFX.SetActive(false);

            Vector3 launchDir = transform.forward;
            launchDir.y = 0f;
            controller._rb.AddForce((launchDir.normalized * railBoost )+Vector3.up * 10, ForceMode.VelocityChange);
            controller.OverrideMoveSpeed(7);
        }
    }
}
