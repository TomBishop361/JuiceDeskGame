using System;
using UnityEngine;
using UnityEngine.UI;

public class WeaponManager : MonoBehaviour
{
    /// <summary>
    /// Manage CanFire States    & cooldowns
    /// In Future will manage what secondary fire is equip/ what bullets
    /// </summary>
    [Header("References")]
    [SerializeField] InputController _controller;
    [SerializeField] Sniper _Sniper;
    [SerializeField] Image Fillimage;

    public event Action OnSniperCoolDown = delegate { };
    public bool CanPrimaryFire = true;

    [Header("Sniper")]
    bool SniperOnCoolDown;
    [SerializeField] float sniperCDTime = 15;
    float sniperCDTimer;


    private void OnEnable()
    {
        _Sniper.OnShotTaken += SniperStartCooldown;
    }

    void SniperStartCooldown()
    {
        SniperOnCoolDown = true;
        sniperCDTimer = sniperCDTime;
        Fillimage.fillAmount = 0;
    }

    private void FixedUpdate()
    {
        if((_controller.wallRunning || _controller.sliding || _controller.isRailGrinding) && SniperOnCoolDown)
        {            
            sniperCDTimer -= Time.deltaTime;
            Fillimage.fillAmount = Mathf.InverseLerp(sniperCDTime,0 , sniperCDTimer);
            if (sniperCDTimer <= 0) {
                
                SniperOnCoolDown =false;
                _Sniper.isOnCoolDown = false;
            }
        }
    }

}
