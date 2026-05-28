using Game.Audio;
using UnityEngine;

public class SFXListener : MonoBehaviour {
   EventManager _EventManager => EventManager.instance;

    [Header("SMG SFX")]
    [Tooltip("Played every time the SMG fires.")]
    [SerializeField] private SFXDefinition smgFireSFX;

    [Header("Sniper SFX")]
    [Tooltip("Played when the sniper fires a charged shot.")]
    [SerializeField] private SFXDefinition sniperChargeShootSFX;

	private void OnEnable() {
        _EventManager.Subscribe("SniperShot", PlaySniperSound);
        _EventManager.Subscribe("SMGShot", PlaySMG);
    }

	private void OnDisable() {
		_EventManager.Unsubscribe("SMGShot", PlaySMG);
		_EventManager.Unsubscribe("SniperShot", PlaySniperSound);
	}

	private void PlaySniperSound(object data) {
        Transform origin = data as Transform;

        if (origin == null)
            return;
        // Play the full charge-up + shot sound immediately
        SFXManager.PlayAttached(sniperChargeShootSFX, origin);
    }

	private void PlaySMG(object data) {
        Transform origin = data as Transform;

        if (origin == null)
            return;
        SFXManager.PlayAttached(smgFireSFX, origin);
    }
}