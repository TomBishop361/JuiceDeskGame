using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class Shotgun : MonoBehaviour
{
    [SerializeField]
    GunInputManagerBase _gunInputManager;
    IGunInputManager gunInputManager => _gunInputManager.InputManager;



    bool shootinput;

    [Header("References")]
    [SerializeField] Transform OrientationObj;
    [SerializeField] GameObject ShotgunhitBox;

    [Header("Shotgun")]    
    public float hitboxTime;
    float hitboxTimer;

    public float ShotgunCoolDownTime;
    float ShotgunCooldownTimer;

    bool OnCoolDown;
    bool shooting;



    private void OnEnable()
    {
        gunInputManager.onSecondFire += shoot;
    }

    private void OnDisable()
    {
        gunInputManager.onSecondFire -= shoot;
    }

    void shoot(bool value)
    {
        Debug.Log("ShootingShotgun");
        shootinput = value;
    }

    void Timers()
    {
        if(hitboxTimer > 0)
        {
            hitboxTimer -= Time.deltaTime;
            
        }
        else if (shooting) {
            ShotgunhitBox.SetActive(false);
            ShotgunCooldownTimer = ShotgunCoolDownTime;
            shooting = false;
        }

        if (ShotgunCooldownTimer > 0)
        {
            ShotgunCooldownTimer -= Time.deltaTime;
        }
        else if  (!shooting)
        {
            OnCoolDown = false;
        }
    }

    // Update is called once per frame
    void Update()
    {
        Timers();
        HandleShoot();
    }

    private void HandleShoot()
    {
        if (OnCoolDown || !shootinput) return;
        OnCoolDown = true;
        shooting = true;
        ShotgunhitBox.SetActive(true);
        hitboxTimer = hitboxTime;
        

        
    }
}
