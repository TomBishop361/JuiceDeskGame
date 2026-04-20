using System;
using UnityEngine;

namespace Game.AI.Drone {
	[Serializable]
	public class DroneHomingProjectileStats : MonoBehaviour {
		[Header("Motion")]
		[Tooltip("Units (per second) travelled by the homing projectile.")]
		[SerializeField] private float speed = 24.0f;
		[Tooltip("How fast the projectile can rotate toward the target (degrees per second).")]
		[SerializeField] private float turnRate = 540.0f;
		[Tooltip("Total lifetime before the projectile expires.")]
		[SerializeField] private float lifetime = 3.0f;
		[Tooltip("How long the projectile keeps homing. Set below zero for infinite homing.")]
		[SerializeField] private float homingDuration = 2.0f;

		[Header("Impact")]
		[Tooltip("Explosion / splash radius around the hit point or timeout point.")]
		[SerializeField] private float hitRadius = 0.75f;
		[Tooltip("If true, the projectile explodes when lifetime expires.")]
		[SerializeField] private bool explodeOnTimeout = true;
		[Tooltip("Attack data used for timeout explosion splash.")]
		[SerializeField] private AttackData timeoutAttackData;
		[Tooltip("Layers this projectile can damage / affect.")]
		[SerializeField] private LayerMask damageLayers = ~0;

		public float Speed => speed;
		public float TurnRate => turnRate;
		public float Lifetime => lifetime;
		public float HomingDuration => homingDuration;
		public float HitRadius => hitRadius;
		public bool ExplodeOnTimeout => explodeOnTimeout;
		public AttackData TimeoutAttackData => timeoutAttackData;
		public LayerMask DamageLayers => damageLayers;
	}
}