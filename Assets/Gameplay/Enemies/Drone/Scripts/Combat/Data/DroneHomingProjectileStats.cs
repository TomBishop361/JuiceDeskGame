using System;
using UnityEngine;

namespace Game.AI.Drone {
	[Serializable]
	public class DroneHomingProjectileStats {
		[Header("Motion")]
		[Tooltip("Units per second travelled by the homing projectile.")]
		[SerializeField] private float speed = 9.0f;
		[Tooltip("Maximum turn speed while the projectile is at full seek strength (degrees per second).")]
		[SerializeField] private float turnRate = 280.0f;
		[Tooltip("Total lifetime before the projectile expires or dies out.")]
		[SerializeField] private float lifetime = 3.5f;
		[Tooltip("How long the projectile keeps homing. Set below or equal to zero for infinite homing.")]
		[SerializeField] private float homingDuration = 2.5f;
		[Tooltip("Vertical aim offset used when steering/proximity checking against the target.")]
		[SerializeField] private float targetAimHeight = 1.0f;

		[Header("Arming")]
		[Tooltip("Minimum time after launch before the projectile can explode on contact. Set to 0 for instant arming.")]
		[SerializeField] private float armingDelay = 0.22f;
		[Tooltip("Minimum distance the projectile must travel before it can explode. Set to 0 to disable distance-based arming.")]
		[SerializeField] private float minimumArmingDistance = 1.5f;

		[Header("Seek")]
		[Tooltip("How long the projectile flies with reduced steering before ramping to full turn strength.")]
		[SerializeField] private float seekDelay = 0.18f;
		[Tooltip("How long it takes to blend from initial seek strength to full turn strength.")]
		[SerializeField] private float seekRampDuration = 0.4f;
		[Tooltip("Turn strength used before the ramp reaches full homing. 0 = straight launch | 1 = full turn rate immediately.")]
		[Range(0.0f, 1.0f)]
		[SerializeField] private float initialSeekStrength = 0.0f;

		[Header("Impact")]
		[Tooltip("Explosion / splash radius around the hit point or timeout point.")]
		[SerializeField] private float explosionRadius = 2.5f;
		[Tooltip("If true, the projectile explodes when lifetime expires after it has armed.")]
		[SerializeField] private bool explodeOnTimeout = true;
		[Tooltip("Attack data used for timeout explosion splash.")]
		[SerializeField] private AttackData timeoutAttackData;
		[Tooltip("Layers this projectile can damage / affect.")]
		[SerializeField] private LayerMask damageLayers = ~0;

		[Header("Proximity Fuse")]
		[Tooltip("If true, the projectile can detonate when it gets close enough to the target instead of relying only on direct impact.")]
		[SerializeField] private bool useProximityFuse = true;
		[Tooltip("Distance from the target aim point that will trigger a proximity detonation. Keep this at or below hit radius for consistent splash damage")]
		[SerializeField] private float proximityFuseRadius = 1.75f;

		public float Speed => speed;
		public float TurnRate => turnRate;
		public float Lifetime => lifetime;
		public float HomingDuration => homingDuration;
		public float TargetAimHeight => targetAimHeight;
		public float ArmingDelay => armingDelay;
		public float MinimumArmingDistance => minimumArmingDistance;
		public float SeekDelay => seekDelay;
		public float SeekRampDuration => seekRampDuration;
		public float InitialSeekStrength => initialSeekStrength;
		public float ExplosionRadius => explosionRadius;
		public bool ExplodeOnTimeout => explodeOnTimeout;
		public AttackData TimeoutAttackData => timeoutAttackData;
		public LayerMask DamageLayers => damageLayers;
		public bool UseProximityFuse => useProximityFuse;
		public float ProximityFuseRadius => proximityFuseRadius;
	}
}