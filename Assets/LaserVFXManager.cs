using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.VFX;

[RequireComponent(typeof(VisualEffect))]
public class LaserVFXManager : MonoBehaviour
{    
    
    [SerializeField] VisualEffect laser;
    public Vector3 laserDirection;
    TimerManager timerManager;
    private void Reset()
    {
        laser = GetComponent<VisualEffect>();
    }

    void handleShot()
    {
        
        laser.SetVector3("LaserDirection", laserDirection);
        laser.Play();
    }


    public void FireLaser(Vector3 pos, quaternion rot, float dist)
    {
        laser.gameObject.transform.position = pos;
        laser.gameObject.transform.rotation = rot;
        laserDirection = transform.forward * dist;
        laser.SetFloat("Range", dist);
        laser.gameObject.SetActive(true);
        handleShot();
    }


    private void OnEnable()
    {
        timerManager = TimerManager.instance;

        timerManager.NewTimer(1, timerCallback, "Sniper Timer");
    }

    void timerCallback()
    {
        laser.gameObject.SetActive(false);
    }
}
