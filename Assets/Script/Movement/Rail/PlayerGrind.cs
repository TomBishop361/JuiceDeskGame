using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

public class PlayerGrind : MonoBehaviour
{

    [Header("Variables")]
    public bool onRail;
    [SerializeField] float grindSpeed;
    [SerializeField] float heightOffset; // playerheight/2
    float timeForFullSpline;
    float elapsdTime;
    [SerializeField] float lerpSpeed = 10;

    [Header("Refereneces")]
    [SerializeField] RailScript currentRailScript;
    Rigidbody rb;
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>(); 

    }


    //MoveToInputController?
    private void FixedUpdate()
    {
        if (onRail) movePlayerAlongRail();
    }

    private void movePlayerAlongRail()
    {
        if (currentRailScript != null && onRail)
        {
            float progess = elapsdTime / timeForFullSpline;
            if(progess <0 || progess > 1)
            {
                throwOffRail();
                return;
            }
            float nextTimeNormalised;
            if (currentRailScript.normalDir)
            {
                nextTimeNormalised = (elapsdTime + Time.deltaTime) / timeForFullSpline;
            }
            else
            {
                nextTimeNormalised = (elapsdTime - Time.deltaTime) / timeForFullSpline;
            }

            float3 pos, tangent, up;
            float3 nextPosFloat, nextTan, nextUp;
            SplineUtility.Evaluate(currentRailScript.railSpline.Spline, progess, out pos, out tangent, out up);
            SplineUtility.Evaluate(currentRailScript.railSpline.Spline, nextTimeNormalised,out nextPosFloat, out nextTan, out nextUp);

            Vector3 worldPos = currentRailScript.LocalToWorldConversion(pos);
            Vector3 nextPos = currentRailScript.LocalToWorldConversion(nextPosFloat);

            rb.linearVelocity =  worldPos + (transform.up * heightOffset);
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.LookRotation(nextPos- worldPos), lerpSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.FromToRotation(transform.up, up) * transform.rotation, lerpSpeed * Time.deltaTime);

            if (currentRailScript.normalDir)
                elapsdTime += Time.deltaTime;
            else elapsdTime -= Time.deltaTime;  
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.tag == "Rail")
        {
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
        SplineUtility.Evaluate(currentRailScript.railSpline.Spline,normalisedTime,out pos, out forward, out up);
        currentRailScript.CalculateDirection(forward,transform.forward);
        transform.position = splinePoint + (transform.up * heightOffset);
    }

    //MoveToInputController?
    void throwOffRail()
    {
        onRail = false;
        rb.useGravity = true;
        currentRailScript = null;
        transform.position += transform.forward * 1;
    }
}
