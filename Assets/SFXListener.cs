using Game.Audio;
using UnityEngine;
using UnityEngine.UIElements;

public class SFXListener : MonoBehaviour
{
    EventManager _EventManager = EventManager.instance;

    [Header("Sniper SFX")]
    [Tooltip("Played when the sniper fires a charged shot.")]
    [SerializeField] private SFXDefinition sniperChargeShootSFX;
    [Tooltip("Time into the sniper charge/shoot SFX where the actual shot blast happens.")]
    [SerializeField] private float sniperShotFireDelay = 1.02f;
    

    [Header("SMG SFX")]
    [Tooltip("Played every time the SMG fires.")]
    [SerializeField] private SFXDefinition smgFireSFX;


    void OnEnable()
    {
        _EventManager.Subscribe("SniperShot", PlaySniperSound);
        _EventManager.Subscribe("SMGShot",PlaySMG);
    }


    void PlaySniperSound(object data)
    {
        Transform origin = data as Transform;

        if (origin == null)
            return;
        // Play the full charge-up + shot sound immediately
        SFXManager.PlayAttached(sniperChargeShootSFX, origin);
    }

    void PlaySMG(object data)
    {
        Transform origin = data as Transform;

        if (origin == null)
            return;
        SFXManager.PlayAttached(smgFireSFX, origin);
    }
}
