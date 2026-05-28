using Game.AI;
using UnityEngine;

public class NoiseEmitterListener : MonoBehaviour {
    EventManager _EventManager => EventManager.instance;
    private PlayerNoiseEmitter noiseEmitter;

    [Header("SMG Emitter")]
    [SerializeField] private float smgNoiseInterval = 0.15f;
    private float nextSmgNoiseTime;

	[Header("Sniper Emitter")]
	[SerializeField] private float sniperNoiseInterval = 0.5f;
	[SerializeField] private float sniperNoiseMultiplier = 1.35f;
	private float nextSniperNoiseTime;

	[Header("Slide Emitter")]
    [SerializeField] private float slideNoiseInterval = 0.35f;
    private float nextSlideNoiseTime;

	[Header("Rail Grind Emitter")]
	[SerializeField] private float railGrindNoiseInterval = 0.30f;
    private float nextRailGrindNoiseTime;

	private void Awake() {
        noiseEmitter = GetComponentInParent<PlayerNoiseEmitter>();
    }

    private void OnEnable() {
		_EventManager.Subscribe("SMGShot", EmitPrimaryFireNoise);
		_EventManager.Subscribe("SniperShot", EmitSniperNoise);
		_EventManager.Subscribe("OnSlide", EmitSlide);
		_EventManager.Subscribe("OnRailGrind", EmitRail);
	}

    private void OnDisable() {
		_EventManager.Unsubscribe("SMGShot", EmitPrimaryFireNoise);
		_EventManager.Unsubscribe("SniperShot", EmitSniperNoise);
		_EventManager.Unsubscribe("OnSlide", EmitSlide);
		_EventManager.Unsubscribe("OnRailGrind", EmitRail);
	}

	private void EmitPrimaryFireNoise(object data) {
		if (Time.time >= nextSmgNoiseTime) {
			noiseEmitter?.EmitWeaponNoise();
			nextSmgNoiseTime = Time.time + smgNoiseInterval;
		}
	}

	private void EmitSniperNoise(object data) {
		if (Time.time >= nextSniperNoiseTime) {
			// Emits a loud AI hearing noise pulse when the sniper charge begins
			noiseEmitter?.EmitWeaponNoise(sniperNoiseMultiplier);
			nextSniperNoiseTime = Time.time + sniperNoiseInterval;
		}

	}

	private void EmitSlide(object data) {
        if (Time.time >= nextSlideNoiseTime) {
            noiseEmitter?.EmitSlideNoise(0.65f);
            nextSlideNoiseTime = Time.time + slideNoiseInterval;
        }
    }

    private void EmitRail(object data) {
        // Pulse noise for continued rail grinding
        if (Time.time >= nextRailGrindNoiseTime) {
            noiseEmitter?.EmitRailGrindNoise(0.7f);
            nextRailGrindNoiseTime = Time.time + railGrindNoiseInterval;
        }
    }
}