using System.Collections;
using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEngine;

public class CameraShoulderHandler : MonoBehaviour
{
    [Header("Shoulder")]    
    [Range(-1,1)]
    [SerializeField]float XShoulderOffset;
    [SerializeField] float DutchTilt;

    float targetOffset;
    float currentOffset;

    float targetDutch;
    float currentDutch;

    [Header("References")]
    [SerializeField]WallRunning controller;
    [SerializeField] CinemachineThirdPersonFollow cineCam;
    [SerializeField] CinemachineCamera cinemachineCamera;

    Coroutine cameraLerp;

    private void Start()
    {
        currentOffset = XShoulderOffset;
        
        cineCam.ShoulderOffset.x = XShoulderOffset;
        
    }
    private void OnEnable()
    {
        controller.OnWallRunStart += changeShoulder;
        controller.OnWallRunEnd += resetDutch;
        
    }
    private void OnDisable()
    {
        controller.OnWallRunStart -= changeShoulder;
        controller.OnWallRunEnd -= resetDutch;
    }

    void changeShoulder(bool right)
    {
        Debug.Log("Right" + right);
        if (right)
        {
            targetOffset = -XShoulderOffset;
            targetDutch = DutchTilt;
        }
        else
        {            
            targetDutch = -DutchTilt;
        }

        //Begin Lerp
        if(cameraLerp != null) StopCoroutine(cameraLerp);
        cameraLerp = StartCoroutine("MoveCameraOffset");
    }

    void resetDutch()
    {
        targetOffset = XShoulderOffset;
        targetDutch = 0;
        if (cameraLerp != null) StopCoroutine(cameraLerp);
        cameraLerp = StartCoroutine("MoveCameraOffset");
    }

    IEnumerator MoveCameraOffset()
    {
        Debug.Log("Lerping");
        float t =0;
        while (t < 1)
        {
            currentDutch = Mathf.Lerp(currentDutch, targetDutch, t);
            currentOffset = Mathf.Lerp(currentOffset, targetOffset, t);
            t += Time.deltaTime;
            yield return null;
            cinemachineCamera.Lens.Dutch = currentDutch;
            cineCam.ShoulderOffset.x = currentOffset;
        }
    }

    
}
