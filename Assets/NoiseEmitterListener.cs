using Game.AI;
using UnityEngine;


public class NoiseEmitterListener : MonoBehaviour
{
    EventManager _EventManager => EventManager.instance;
    private PlayerNoiseEmitter noiseEmitter;

    [Header("Gun Emitter")]
    [SerializeField] private float weaponNoiseInterval = 0.15f;    
    private float nextWeaponNoiseTime;

    [Header("Slide Emitter")]
    [SerializeField] private float slideNoiseInterval = 0.35f;    
    private float nextSlideNoiseTime;

    [SerializeField] private float railGrindNoiseInterval;
    private float nextRailGrindNoiseTime = 0.35f;
    

    private void Awake()
    {
        noiseEmitter = GetComponentInParent<PlayerNoiseEmitter>();
    }

    private void OnEnable()
    {
        _EventManager.Subscribe("SMGShot", EmitPrimaryFireNoise);
        _EventManager.Subscribe("OnSlide", EmitSlide);
        _EventManager.Subscribe("OnRailGrind", EmitRail);
    }

    private void EmitSlide(object data)
    {
        if (Time.time >= nextSlideNoiseTime)
        {
            noiseEmitter?.EmitSlideNoise(0.65f);
            nextSlideNoiseTime = Time.time + slideNoiseInterval;
        }
    }

    private void EmitPrimaryFireNoise(object data)
    {
        if (Time.time < nextWeaponNoiseTime)
        {
            return;
        }

        noiseEmitter?.EmitWeaponNoise();
        nextWeaponNoiseTime = Time.time + weaponNoiseInterval;
    }

    private void EmitRail(object data)
    {
        // Pulse noise for continued rail grinding
        if (Time.time >= nextRailGrindNoiseTime)
        {
            noiseEmitter?.EmitRailGrindNoise(0.7f);
            nextRailGrindNoiseTime = Time.time + railGrindNoiseInterval;
        }
    }

}
