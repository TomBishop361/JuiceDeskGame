using System.Collections;
using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEngine;

public class CameraShoulderHandler : MonoBehaviour
{
    [Header("Shoulder")]    
    [Range(-1,1)]
    [SerializeField]float XShoulderOffset;

    float targetOffset;
    float currentOffset;

    [Header("References")]
    [SerializeField]WallRunning controller;
    [SerializeField] CinemachineThirdPersonFollow cineCam;



    private void Start()
    {
        currentOffset = XShoulderOffset;
        cineCam.ShoulderOffset.x = XShoulderOffset;
    }
    private void OnEnable()
    {
        controller.OnWallRunStart += changeShoulder;
    }
    private void OnDisable()
    {
        controller.OnWallRunStart -= changeShoulder;
    }

    void changeShoulder(bool right)
    {
        Debug.Log("Right" + right);
        if (right) targetOffset = -XShoulderOffset;
        else targetOffset = XShoulderOffset;

        //Begin Lerp       
        StartCoroutine("MoveCameraOffset");
    }

    IEnumerator MoveCameraOffset()
    {
        Debug.Log("Lerping");
        float t =0;
        while (t < 1)
        {
            currentOffset = Mathf.Lerp(currentOffset, targetOffset, t);
            t += Time.deltaTime;
            yield return null;
            cineCam.ShoulderOffset.x = currentOffset;
        }
    }

    
}
