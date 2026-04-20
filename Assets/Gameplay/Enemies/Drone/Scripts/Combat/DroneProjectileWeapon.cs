using Game.Combat.Projectiles;
using UnityEngine;

namespace Game.AI.Drone {
	[DisallowMultipleComponent]
	public sealed class DroneProjectileWeapon : MonoBehaviour {
		[Header("Projectile Setup")]
		[Tooltip("Attack data used to initialise projectile damage and behaviour.")]
		[SerializeField] private AttackData projectileAttackData = new AttackData();
		[Tooltip("Projectile prefab the drone will spawn when firing.")]
		[SerializeField] private GameObject projectilePrefab;
		[Tooltip("Transform representing where projectiles are spawned from.")]
		[SerializeField] private Transform projectileSpawn;
		[Tooltip("Gameplay tuning pushed into each homing projectile when this drone fires.")]
		[SerializeField] private DroneHomingProjectileStats homingProjectileStats = new DroneHomingProjectileStats();
		
		[Header("Range")]
		[Tooltip("Maximum distance at which the drone is allowed to fire.")]
		[SerializeField] private float fireRange = 10.0f;
		//[Tooltip("If true, the drone must currently have LOS before it is allowed to fire.")]
		//[SerializeField] private bool requireLineOfSightToFire = true;

		[Header("Timing")]
		[Tooltip("Time (in seconds) between consecutive shots.")]
		[SerializeField] private float fireCooldown = 1.5f;
		[Tooltip("Minimum time the drone stays in attack state to prevent animation spam.")]
		[SerializeField] private float fireLockTime = 0.45f;

		[Header("Animation")]
		[Tooltip("Whether the drone will fire a projectile based on the animation event or not.")]
		[SerializeField] private bool fireOnAnimationEvent = true;
		[Tooltip("Animator trigger name used to start the fire animation.")]
		[SerializeField] private string fireTriggerName = "Fire";

		// Cooldown/State timers
		private float nextFireTime = -Mathf.Infinity;
		private int fireTriggerHash;

		private DroneVisualMotion droneVisualMotion;

		public float FireRange => fireRange;
		public DroneHomingProjectileStats HomingProjectileStats => homingProjectileStats;

		private void Awake() {
			fireTriggerHash = Animator.StringToHash(fireTriggerName);
			droneVisualMotion = GetComponent<DroneVisualMotion>();
		}

		// Resets weapon runtime state when the owner respawns or is reused from a pool
		public void ResetRuntime() {
			nextFireTime = -Mathf.Infinity;
		}

		// Returns true if the supplied distance is inside fire range
		public bool InFireRange(float distanceToTarget) {
			return distanceToTarget <= fireRange;
		}

		// Returns true if the owner is allowed to fire right now
		public bool CanFire(DroneEnemy owner) {
			return owner != null
				&& owner.IsDead == false
				&& owner.IsStunned == false
				&& owner.IsAttacking == false
				&& owner.IsKnockedDown == false
				&& Time.time >= nextFireTime;
				//&& (requireLineOfSightToFire == false || owner.HasLineOfSight);
		}

		// Attempts to start the fire attack by setting cooldowns + attack lock timing + the fire animation trigger
		public bool TryStartFire(DroneEnemy owner, Animator animator) {
			if (owner == null || owner.HasTarget == false) {
				return false;
			}

			//if (requireLineOfSightToFire&& owner.HasLineOfSight == false) {
			//	return false;
			//}

			if (CanFire(owner) == false || InFireRange(owner.DistanceToTarget) == false) {
				return false;
			}

			if (projectilePrefab == null || projectileSpawn == null || owner.Target == null) {
				return false;
			}

			nextFireTime = Time.time + fireCooldown;
			owner.BeginAttackLock(fireLockTime);

			if (animator != null) {
				animator.SetTrigger(fireTriggerHash);
			}

			// Fire immediately if not using an animation event
			if (fireOnAnimationEvent == false) {
                FireProjectile(owner.Target);
            }

			return true;
		}

		public void FireProjectile(Transform target) {
			if (projectilePrefab == null || projectileSpawn == null) {
				return;
			}

			Vector3 aimPosition;
			if (target != null) {
				// Get aim direction towards target (aim at chest height -> + Vector3.up * 1.0f)
				aimPosition = target.position + Vector3.up * 1.0f;
			}
			else {
				// Fallback if no valid target
				aimPosition = projectileSpawn.position + projectileSpawn.forward;
			}

			Vector3 direction = aimPosition - projectileSpawn.position;
			if (direction.sqrMagnitude < 0.0001f) {
				direction = projectileSpawn.forward;
			}

			Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

			// Spawn projectile
			GameObject projectileObject = Instantiate(projectilePrefab, projectileSpawn.position, rotation);

			// Kick the drone slightly backwards to visualise them shooting
			droneVisualMotion?.PlayShotRecoil();

			// Supply projectile hitbox with attack data
			ProjectileHitbox projectileHitbox = projectileObject.GetComponentInChildren<ProjectileHitbox>();
			if (projectileHitbox != null) {
				projectileHitbox.Initialise(projectileAttackData);
			}
			else {
				Debug.LogWarning("DroneProjectileWeapon: ProjectileHitbox cannot be found on instantiated projectile object " + projectileObject.name);
			}

			// Launch homing projectile
			if (projectileObject.TryGetComponent(out DroneHomingProjectile homingProjectile)) {
				homingProjectile.Configure(projectileAttackData, homingProjectileStats);
				homingProjectile.Init(target);
				homingProjectile.Launch(direction);
				return;
			}

			//if (projectileObject.TryGetComponent(out Rigidbody rigidbody)) {
			//	rigidbody.linearVelocity = direction.normalized * 14.0f;
			//}
		}

		public void Cancel() {
		}

		// - DEPRECATED - apply Hurtbox logic and extra null checks now with debug messages

		//public void FireProjectile(Transform target) {
		//	if (projectilePrefab == null || projectileSpawn == null) {
		//		return;
		//	}

		//	Transform lookTarget = target != null ? target : projectileSpawn;
		//	Vector3 direction = (lookTarget.position - projectileSpawn.position).normalized;
		//	Quaternion rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction, Vector3.up) : projectileSpawn.rotation;

		//	GameObject projectile = Instantiate(projectilePrefab, projectileSpawn.position, rotation);

		//	projectile.SendMessage("Initialise", projectileAttackData, SendMessageOptions.DontRequireReceiver);
		//	projectile.SendMessage("SetTarget", target, SendMessageOptions.DontRequireReceiver);
		//	projectile.SendMessage("SetAttackData", projectileAttackData, SendMessageOptions.DontRequireReceiver);
		//}
	}
}
