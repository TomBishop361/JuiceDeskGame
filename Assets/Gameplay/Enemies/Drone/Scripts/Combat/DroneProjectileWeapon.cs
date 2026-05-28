using Game.Combat.Projectiles;
using UnityEngine;
using UnityEngine.Pool;
using Game.Audio;

namespace Game.AI.Drone {
	[DisallowMultipleComponent]
	public sealed class DroneProjectileWeapon : MonoBehaviour {
		[Header("Projectile Setup")]
		[Tooltip("Attack data used to initialise projectile damage and behaviour.")]
		[SerializeField] private AttackData projectileAttackData = new AttackData();
		[Tooltip("Projectile prefab the drone will spawn when firing.")]
		[SerializeField] private DroneHomingProjectile projectilePrefab;
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

		[Header("Pooling")]
		[Tooltip("Initial number of pooled projectiles created for this drone weapon.")]
		[SerializeField] private int defaultProjectilePoolCapacity = 4;
		[Tooltip("Maximum number of pooled projectiles allowed before extra released ones are destroyed.")]
		[SerializeField] private int maxProjectilePoolSize = 12;
		[Tooltip("Populate the projectile pool during Awake so the first shot doesn't cause any lag.")]
		[SerializeField] private bool populateProjectilePoolAtStartup = true;

		[Header("SFX")]
		[Tooltip("Played when the drone actually launches a homing projectile.")]
		[SerializeField] private SFXDefinition projectileLaunchSFX;

		// Cooldown/State timers
		private float nextFireTime = -Mathf.Infinity;

		private DroneVisualMotion droneVisualMotion;
		private DroneEnemy activeOwner;
		private IObjectPool<PooledObject> projectilePool;

		public float FireRange => fireRange;
		public DroneHomingProjectileStats HomingProjectileStats => homingProjectileStats;

		private void Awake() {
			droneVisualMotion = GetComponent<DroneVisualMotion>();
			SetupProjectilePool();
		}

		// Resets weapon runtime state when the owner respawns or is reused from a pool
		public void ResetRuntime() {
			nextFireTime = -Mathf.Infinity;
			activeOwner = null;
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
		public bool TryStartFire(DroneEnemy owner/*, Animator animator*/) {
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

			SetupProjectilePool();
			if (projectilePool == null) {
				Debug.LogWarning($"{name}: Projectile pool could not be created because no projectile prefab is assigned.", this);
				return false;
			}

			activeOwner = owner;
			nextFireTime = Time.time + fireCooldown;
			owner.BeginAttackLock(fireLockTime);

			//if (animator != null) {
			//	animator.SetTrigger(fireTriggerHash);
			//}

			//// Fire immediately if not using an animation event
			//if (fireOnAnimationEvent == false) {
			//	FireProjectile(owner.Target);
			//}

			// Fire immediately 
			FireProjectile(owner.Target);

			return true;
		}

		public void FireProjectile(Transform target) {
			if (projectilePrefab == null || projectileSpawn == null) {
				return;
			}

			SetupProjectilePool();
			if (projectilePool == null) {
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
			PooledObject pooledProjectile = projectilePool.Get();
			DroneHomingProjectile homingProjectile = pooledProjectile.GetComponent<DroneHomingProjectile>();

			if (homingProjectile == null) {
				pooledProjectile.ReturnToPool();
				Debug.LogWarning($"{name}: Pooled projectile is missing DroneHomingProjectile.", this);
				return;
			}

			//Vector3 spawnPosition = projectileSpawn.position + direction.normalized * 0.5f;
			//homingProjectile.transform.SetPositionAndRotation(spawnPosition, rotation);

			Vector3 spawnPosition = projectileSpawn.position + direction.normalized * 0.5f;
			homingProjectile.SetSpawnPose(spawnPosition, rotation);

			// Kick the drone slightly backwards to visualise them shooting
			droneVisualMotion?.PlayShotRecoil();

			// Launch homing projectile
			homingProjectile.Configure(BuildProjectileAttackData(), homingProjectileStats);
			homingProjectile.Init(target);
			homingProjectile.Launch(direction);

			// Play the launch sound at the actual projectile spawn position
			SFXManager.PlayAtPosition(projectileLaunchSFX, spawnPosition);

			//if (projectileObject.TryGetComponent(out Rigidbody rigidbody)) {
			//	rigidbody.linearVelocity = direction.normalized * 14.0f;
			//}
		}

		public void Cancel() {}

		private AttackData BuildProjectileAttackData() {
			DroneEnemy owner = activeOwner != null ? activeOwner : GetComponentInParent<DroneEnemy>();

			AttackData attackData = projectileAttackData;
			attackData.Attacker = owner != null ? owner.gameObject : gameObject;
			attackData.AttackerFaction = owner != null ? owner.OwnerFaction : Faction.Enemy;
			attackData.Type = DamageType.Ranged;

			return attackData;
		}

		private void SetupProjectilePool() {
			if (projectilePool != null || projectilePrefab == null) {
				return;
			}

			projectilePool = new ObjectPool<PooledObject>(
				CreateProjectile,
				OnTakeProjectileFromPool,
				OnReturnProjectileToPool,
				OnDestroyProjectileFromPool,
				true,
				Mathf.Max(1, defaultProjectilePoolCapacity),
				Mathf.Max(1, maxProjectilePoolSize)
			);

			if (populateProjectilePoolAtStartup) {
				PopulateProjectilePool();
			}
		}

		private PooledObject CreateProjectile() {
			PooledObject projectilePrefabPooledObject = projectilePrefab.GetComponent<PooledObject>();

			PooledObject pooledProjectile = Instantiate(projectilePrefabPooledObject);
			pooledProjectile.SetPool(projectilePool);
			pooledProjectile.SourcePrefab = projectilePrefabPooledObject;
			pooledProjectile.gameObject.SetActive(false);

			return pooledProjectile;
		}

		private void PopulateProjectilePool() {
			int count = Mathf.Min(Mathf.Max(1, defaultProjectilePoolCapacity), Mathf.Max(1, maxProjectilePoolSize));

			PooledObject[] borrowedProjectiles = new PooledObject[count];
			for (int i = 0; i < count; i++) {
				borrowedProjectiles[i] = projectilePool.Get();
			}

			for (int i = 0; i < borrowedProjectiles.Length; i++) {
				projectilePool.Release(borrowedProjectiles[i]);
			}
		}

		private void OnTakeProjectileFromPool(PooledObject item) {
			if (item == null) {
				return;
			}

			item.transform.SetParent(null, true);
			item.gameObject.SetActive(true);

			DroneHomingProjectile projectile = item.GetComponent<DroneHomingProjectile>();
			projectile?.OnSpawned();

			//foreach (IPoolLifecycleHandler handler in item.GetComponents<IPoolLifecycleHandler>()) {
			//	handler.OnSpawned();
			//}
		}

		private void OnReturnProjectileToPool(PooledObject item) {
			if (item == null) {
				return;
			}

			DroneHomingProjectile projectile = item.GetComponent<DroneHomingProjectile>();
			projectile?.OnDespawned();

			//foreach (IPoolLifecycleHandler handler in item.GetComponents<IPoolLifecycleHandler>()) {
			//	handler.OnDespawned();
			//}

			item.transform.SetParent(transform, false);
			item.transform.localPosition = Vector3.zero;
			item.transform.localRotation = Quaternion.identity;
			item.gameObject.SetActive(false);
		}

		private void OnDestroyProjectileFromPool(PooledObject item) {
			if (item != null && item.gameObject != null) {
				Destroy(item.gameObject);
			}
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
