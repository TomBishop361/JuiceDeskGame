using System;
using UnityEngine;

public class Shotgun : MonoBehaviour
{
    //[SerializeField]
    //GunInputManagerBase _gunInputManager;
    //IGunInputManager gunInputManager => _gunInputManager.InputManager;
    //[SerializeField] float shotGunKnockBackMulti = 10;
    

    //bool shootinput;

    //[Header("References")]
    //[SerializeField] InputController playerController;

    //[SerializeField] GameObject ShotgunhitBox;

    //[Header("Shotgun")]    
    //public float hitboxTime;
    //float hitboxTimer;

    //public float ShotgunCoolDownTime;
    //float ShotgunCooldownTimer;

    //bool OnCoolDown;
    //bool shooting;

    //[SerializeField] Rigidbody rb;    

    //private void OnEnable()
    //{
    //    //gunInputManager.onSecondFire += shoot;
    //}

    //private void OnDisable()
    //{
    //    //gunInputManager.onSecondFire -= shoot;
    //}

    //void shoot(bool value)
    //{
    //    Debug.Log("ShootingShotgun");
    //    shootinput = value;
    //}

    //void ApplyKnockBack()
    //{        
    //    Vector3 knockbackDir = -ShotgunhitBox.transform.up.normalized;
        
    //    float blastForce = 15f; 
    //    float verticalLift = 1.2f; 

    //    // 3. Calculate the new velocity
    //    Vector3 newVelocity = knockbackDir * blastForce;

       
    //    if (newVelocity.y > 0)
    //    {
    //       // newVelocity.y *= verticalLift;
    //        //newVelocity.y = Mathf.Min(newVelocity.y, 14f);
    //    }

      
    //    Vector3 currentHorizontal = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);

        
        
    //    rb.linearVelocity = new Vector3(newVelocity.x, newVelocity.y, newVelocity.z);

      
    //    playerController.ResetRestrictions();
    //}

    //void Timers()
    //{
    //    if(hitboxTimer > 0)
    //    {
    //        hitboxTimer -= Time.deltaTime;            
    //    }
    //    else if (shooting) {
    //        ShotgunhitBox.SetActive(false);
    //        ShotgunCooldownTimer = ShotgunCoolDownTime;
    //        shooting = false;
    //    }
    //    if (ShotgunCooldownTimer > 0)
    //    {
    //        ShotgunCooldownTimer -= Time.deltaTime;
    //    }
    //    else if  (!shooting)
    //    {
    //        OnCoolDown = false;
    //    }
    //}

    //// Update is called once per frame
    //void Update()
    //{
    //    Timers();
    //    HandleShoot();
    //}

    //private void HandleShoot()
    //{
    //    if (OnCoolDown || !shootinput) return;
    //    ApplyKnockBack();
    //    playerController.ResetRestrictions();
    //    OnCoolDown = true;
    //    shooting = true;
    //    ShotgunhitBox.SetActive(true);
    //    hitboxTimer = hitboxTime;
    //}
}
