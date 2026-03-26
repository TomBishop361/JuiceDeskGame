using UnityEngine;

public class Sniper : MonoBehaviour
{
    //Hold RightMouse to slow down time and aim
    //LMB while RMB to fire Sniper shot
    // While rmb zoom in camera, slow down timer
    // Post process effect (Enemy Highlight red?)

    [Header("References")]
    [SerializeField] InputController playerController;
    [SerializeField]
    GunInputManagerBase _gunInputManager;
    IGunInputManager gunInputManager => _gunInputManager.InputManager;

    private void OnEnable()
    {
        gunInputManager.onSecondFire += Aim;
    }
    private void OnDisable()
    {
        
    }

    void Aim(bool aim)
    {

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
