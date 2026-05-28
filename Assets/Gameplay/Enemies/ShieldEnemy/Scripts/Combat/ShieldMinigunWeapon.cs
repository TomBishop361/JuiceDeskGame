using Game.Audio;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.AI.Shield {
	[DisallowMultipleComponent]
	public sealed class ShieldMinigunWeapon : MonoBehaviour {
		[Header("References")]
		[Tooltip("Shared hitscan hitbox used to apply damage to the raycast target.")]
		[SerializeField] private HitScanHitbox hitScanHitbox;
		[Tooltip("Optional rotating barrel transform used for spin visuals while firing.")]
		[SerializeField] private Transform minigunBarrel;
		[Tooltip("Transform used as the origin point for minigun hitscan shots.")]
		[SerializeField] private Transform minigunMuzzle;
		[Tooltip("Optional pooled tracer prefab used as a short-lived bullet tracer.")]
		[SerializeField] private ShieldMinigunTracer minigunBulletTracerPrefab;
		[Tooltip("Renderers used for minigun heat / emission feedback.")]
		[SerializeField] private Renderer[] minigunHeatRenderers;

		[Header("Damage")]
		[Tooltip("Base attack data used for minigun shots before distance falloff is applied.")]
		[SerializeField] private AttackData minigunAttackData = new AttackData();
		[Tooltip("Layers that can be hit by minigun shots.")]
		[SerializeField] private LayerMask minigunHitMask;

		[Header("Range")]
		[Tooltip("Minimum distance required before the shield enemy will start firing the minigun.")]
		[SerializeField] private float fireMinRange = 3.0f;
		[Tooltip("Distance where minigun shots still deal full damage.")]
		[SerializeField] private float minigunFullDamageRange = 20.0f;
		[Tooltip("Distance where minigun shots drop to medium damage.")]
		[SerializeField] private float minigunMediumDamageRange = 35.0f;
		[Tooltip("Maximum range for valid minigun shots.")]
		[SerializeField] private float minigunMaxRange = 45.0f;

		[Header("Firing")]
		[Tooltip("Shots fired per second while the minigun is active.")]
		[SerializeField] private float minigunFireRate = 5.0f;
		[Tooltip("How long the tracer visual should remain alive.")]
		[SerializeField] private float minigunBulletLifetime = 0.06f;

		[Header("Aim")]
		[Tooltip("How far behind the target the minigun aims using recorded aim history.")]
		[SerializeField] private float minigunAimDelay = 0.18f;
		[Tooltip("Random positional offset added to the delayed aim point.")]
		[SerializeField] private float minigunAimOffsetRadius = 0.6f;
		[Tooltip("Small random spread angle added per shot.")]
		[SerializeField] private float minigunInaccuracyDegrees = 2.0f;
		[Tooltip("Maximum correction distance the aim point can move between consecutive shots.")]
		[SerializeField] private float minigunMaxAimSnapPerShot = 1.2f;

		[Header("Overheat")]
		[Tooltip("How long the owner is exposed after overheating.")]
		[SerializeField] private float minigunExposeDuration = 2.0f;
		[Tooltip("How long the minigun can fire continuously before overheating.")]
		[SerializeField] private float minigunOverheatDuration = 3.0f;
		[Tooltip("How long the minigun takes to visually cool down after it stops firing.")]
		[SerializeField] private float minigunCooldownDuration = 1.5f;

		[Header("Heat Visuals")]
		[Tooltip("Base emission color when the minigun is idle.")]
		[SerializeField] private Color baseEmissionColor = Color.black;
		[Tooltip("Maximum emission color when the minigun is close to overheating.")]
		[SerializeField] private Color maxEmissionColor = new Color(1.5f, 0.4f, 0.1f);
		[Tooltip("Material property used for the emission color.")]
		[SerializeField] private string emissionColorProperty = "_EmissionColor";

		[Header("Pooling")]
		[Tooltip("Initial number of pooled tracers created")]
		[SerializeField] private int defaultTracerPoolCapacity = 16;
		[Tooltip("Maximum number of pooled tracers allowed before extra released ones are destroyed")]
		[SerializeField] private int maxTracerPoolSize = 64;
		[Tooltip("Populate the tracer pool during Awake so the first shot doesn't cause any lag.")]
		[SerializeField] private bool populateTracerPoolAtStartup = true;

		[Header("Animation")]
		[Tooltip("Animator bool name used while continuously firing.")]
		[SerializeField] private string fireBoolName = "IsFiring";

		[Header("SFX")]
		[Tooltip("Played every time the shield minigun fires a hitscan shot.")]
		[SerializeField] private SFXDefinition minigunFireSFX;
		[Tooltip("Played when the minigun overheats and forces the shield enemy into exposed state.")]
		[SerializeField] private SFXDefinition minigunOverheatSFX;

		// Stores historical target positions with timestamps
		// So the minigun can aim at a delayed target position
		private struct AimSample {
			public Vector3 Position;
			public float Time;

			public AimSample(Vector3 position, float time) {
				Position = position;
				Time = time;
			}
		}
		// List of recent target positions (stores oldest -> newest)
		private readonly List<AimSample> minigunAimHistory = new List<AimSample>();

		// Cooldown/State timers
		private float nextMinigunShotTime = -Mathf.Infinity;
		private float minigunFireStartTime = -Mathf.Infinity;
		private float currentHeatPercent;
		private int fireBoolHash;

		private Vector3 lastMinigunAimPoint;
		private bool hasLastMinigunAimPoint;
		private MaterialPropertyBlock minigunEmissionBlock;
		private int emissionColorPropertyId;
		private ShieldEnemy activeOwner;

		private IObjectPool<PooledObject> tracerPool;
		private AudioSource minigunFireLoopSource;

		// Shield Enemy Minigun Specific Properties 

		// True while the weapon is actively firing
		public bool IsFiring { get; private set; }
		// True if the target distance is valid for minigun fire
		public bool InFireRange(float distanceToTarget) {
			return distanceToTarget >= fireMinRange && distanceToTarget <= minigunMaxRange;
		}

		// Gets how "hot" the minigun currently is, normalised from 0 to 1
		public float HeatPercent => GetHeatPercent();

		private void Awake() {
			fireBoolHash = Animator.StringToHash(fireBoolName);

			if (hitScanHitbox != null) {
				hitScanHitbox.Initialise(minigunAttackData);
			}

			emissionColorPropertyId = Shader.PropertyToID(emissionColorProperty);
			minigunEmissionBlock = new MaterialPropertyBlock();

			// Make sure all assigned heat renderers start on base emission
			UpdateEmission(0.0f);

			SetupTracerPool();
		}

		// Resets weapon runtime state when the owner respawns or is reused from a pool
		// Clears minigun fire + cooldown state and ensures aim history is cleared 
		public void ResetRuntime(Animator animator) {
			nextMinigunShotTime = -Mathf.Infinity;
			minigunFireStartTime = -Mathf.Infinity;
			currentHeatPercent = 0.0f;
			IsFiring = false;
			activeOwner = null;

			ClearMinigunAimHistory();

			UpdateEmission(0.0f);

			if (animator != null) {
				animator.SetBool(fireBoolHash, false);
			}
		}

		// Returns true if the owner's shared state currently allows minigun fire
		public bool CanFire(ShieldEnemy owner) {
			return owner != null
				&& owner.IsDead == false
				&& owner.IsStunned == false
				&& owner.IsAttacking == false
				&& owner.IsExposed == false
				&& owner.HasTarget
				&& owner.HasLineOfSight
				&& InFireRange(owner.DistanceToTarget);
		}

		// Starts continuous firing if the owner's state allows it
		public bool TryStartFiring(ShieldEnemy owner, Animator animator, Transform target) {
			if (owner == null || target == null || CanFire(owner) == false) {
				return false;
			}

			if (IsFiring == false) {
				minigunFireStartTime = Time.time;

				// Start a continuous minigun loop without restarting it every bullet
				Transform sfxParent = minigunMuzzle != null ? minigunMuzzle : transform;
				minigunFireLoopSource = SFXManager.PlayAttached(minigunFireSFX, sfxParent);
			}

			activeOwner = owner;
			IsFiring = true;
			RecordTargetSample(target.position);

			// Advance & Hold shield whilst firing
			// TODO NOTE: Do we want this design?
			owner.SetShieldRaised(true);
			owner.FaceTarget(target.position);
			owner.StopMove();

			if (animator != null) {
				animator.SetBool(fireBoolHash, true);
			}

			return true;
		}

		// Stops continuous firing and clears temporary aim state
		public void StopFiring(Animator animator) {
			SFXManager.Stop(minigunFireLoopSource);
			minigunFireLoopSource = null;

			IsFiring = false;
			activeOwner = null;
			minigunFireStartTime = -Mathf.Infinity;
			ClearMinigunAimHistory();

			if (animator != null) {
				animator.SetBool(fireBoolHash, false);
			}




			// Prevent instantly resetting emission
			// TickFire() cools the heat down gradually while IsFiring is false
		}

		// Updates firing + barrel spin + tracer timing + aim history + overheat
		// Call this every frame while the owner is alive
		public void TickFire(ShieldEnemy owner, Animator animator, Transform target) {
			if (owner == null) {
				StopFiring(animator);
				return;
			}

			// Update minigun heat every frame
			// While firing, heat rises toward overheat
			// While not firing, heat gradually cools back down
			if (IsFiring) {
				currentHeatPercent += Time.deltaTime / Mathf.Max(0.01f, minigunOverheatDuration);
			}
			else {
				currentHeatPercent -= Time.deltaTime / Mathf.Max(0.01f, minigunCooldownDuration);
			}

			currentHeatPercent = Mathf.Clamp01(currentHeatPercent);
			UpdateEmission(currentHeatPercent);

			if (IsFiring == false) {
				return;
			}

			if (target == null || CanFire(owner) == false) {
				StopFiring(animator);
				return;
			}

			owner.SetShieldRaised(true);
			owner.FaceTarget(target.position);
			owner.StopMove();

			RecordTargetSample(target.position);

			// Fire cooldown check
			if (Time.time >= nextMinigunShotTime) {
				Vector3 aimPoint = GetLaggedAimPoint(target.position);
				FireShot(owner, aimPoint);
				nextMinigunShotTime = Time.time + (1.0f / Mathf.Max(0.01f, minigunFireRate));
			}

			// Barrel rotation
			if (minigunBarrel != null) {
				float heat = GetHeatPercent();
				float spinSpeed = Mathf.Lerp(1500.0f, 3000.0f, heat);
				minigunBarrel.Rotate(0.0f, 0.0f, spinSpeed * Time.deltaTime, Space.Self);
			}

			// Overheat check
			if (HasMinigunOverheated()) {
				// Play the overheat fail sound before entering exposed state
				SFXManager.PlayAttached(minigunOverheatSFX, transform);

				StopFiring(animator);
				owner.SetShieldRaised(false);
				owner.EnterExposedState(minigunExposeDuration);
			}
		}

		// Updates the current target position into a time-based history buffer
		// This allows the minigun to aim at a delayed position instead of the live target
		private void RecordTargetSample(Vector3 position) {
			// Store position + timestamp
			minigunAimHistory.Add(new AimSample(position, Time.time));

			// Remove old samples beyond the history window
			// Ensures delayed lookup remains relevant
			while (minigunAimHistory.Count > 0 && Time.time - minigunAimHistory[0].Time > 0.75f) {
				minigunAimHistory.RemoveAt(0);
			}
		}

		// Clears all stored aim history
		// Called when target is lost or firing stops to prevent redundant aim data
		private void ClearMinigunAimHistory() {
			minigunAimHistory.Clear();

			// Reset smoothing so next shot doesn't interpolate from an old position
			hasLastMinigunAimPoint = false;
		}

		// Calculates the final aim point used for the minigun shot
		// Combines delayed tracking + random offset + smoothing
		private Vector3 GetLaggedAimPoint(Vector3 fallback) {
			// Get smooth delayed tracking point from aim history
			Vector3 delayedPoint = GetDelayedAimPoint(minigunAimHistory, minigunAimDelay, fallback);

			// Add spray offset
			// This creates a clustered spray instead of perfect tracking
			delayedPoint += Random.insideUnitSphere * minigunAimOffsetRadius;

			// Limit how much the aim point can jump per shot
			if (hasLastMinigunAimPoint) {
				delayedPoint = Vector3.MoveTowards(lastMinigunAimPoint, delayedPoint, minigunMaxAimSnapPerShot);
			}

			if (minigunMuzzle == null) {
				return delayedPoint;
			}

			// Convert to shot direction from muzzle
			Vector3 direction = (delayedPoint - minigunMuzzle.position).normalized;

			// Add angular inaccuracy
			// Calculate small random euler angle offset (this represents inaccuracy)
			// Create new quaternion direction for each bullet - allows deviation from center aim point
			direction = Quaternion.Euler(
				Random.Range(-minigunInaccuracyDegrees, minigunInaccuracyDegrees),
				Random.Range(-minigunInaccuracyDegrees, minigunInaccuracyDegrees),
				0.0f) * direction;

			// Project out to weapon range
			Vector3 finalPoint = minigunMuzzle.position + (direction * minigunMaxRange);

			// Store pre-direction delayed target point
			lastMinigunAimPoint = delayedPoint;
			hasLastMinigunAimPoint = true;

			return finalPoint;
		}

		// Returns a delayed aim point from history interpolated between samples
		// Aim history is stored oldest -> newest because new samples are appended to the end
		private Vector3 GetDelayedAimPoint(List<AimSample> aimHistory, float delay, Vector3 fallbackPoint) {
			if (aimHistory == null || aimHistory.Count == 0) {
				return fallbackPoint;
			}

			float targetTime = Time.time - delay;

			// If there is not enough history yet: use the oldest available sample instead of snapping to live aim
			if (targetTime <= aimHistory[0].Time) {
				return aimHistory[0].Position;
			}

			int lastIndex = aimHistory.Count - 1;

			// If the target time is newer than the newest sample: use the latest known target position
			if (targetTime >= aimHistory[lastIndex].Time) {
				return aimHistory[lastIndex].Position;
			}

			for (int i = 1; i < aimHistory.Count; i++) {
				AimSample older = aimHistory[i - 1];
				AimSample newer = aimHistory[i];

				// Stop once the desired delayed timestamp has been reached
				if (newer.Time >= targetTime) {
					float t = Mathf.InverseLerp(older.Time, newer.Time, targetTime);
					return Vector3.Lerp(older.Position, newer.Position, t);
				}
			}

			return fallbackPoint;
		}

		private void FireShot(ShieldEnemy owner, Vector3 aimPoint) {
			if (hitScanHitbox == null || minigunMuzzle == null) {
				return;
			}

			Vector3 bulletOrigin = minigunMuzzle.position;

			Vector3 direction = aimPoint - bulletOrigin;
			if (direction.sqrMagnitude < 0.0001f) {
				direction = transform.forward;
			}
			direction.Normalize();

			Vector3 endPoint = bulletOrigin + direction * minigunMaxRange;

			// Prevent changing attack data by storing a copy
			var shotAttackData = BuildMinigunAttackData(owner);

			// Cast ray towards bullet tracer direction - check for contact
			if (Physics.Raycast(bulletOrigin, direction, out RaycastHit hit, minigunMaxRange, minigunHitMask, QueryTriggerInteraction.Collide)) {
				// Adjust minigun bullet tracer to match hit point
				endPoint = hit.point;

				// Damage falloff based on distance to hit point
				shotAttackData.Damage = CalculateDamageFalloff(bulletOrigin, endPoint);

				hitScanHitbox.Initialise(shotAttackData);
				// Notify HitScanHitbox of a successful hit
				hitScanHitbox.ApplyHitScanHit(hit, shotAttackData);
			}
			else {
				hitScanHitbox.Initialise(minigunAttackData);
			}

			SpawnHitScanTracer(bulletOrigin, endPoint);
		}

		private AttackData BuildMinigunAttackData(ShieldEnemy owner) {
			ShieldEnemy sourceOwner = owner != null ? owner : activeOwner;

			AttackData attackData = minigunAttackData;
			attackData.Attacker = sourceOwner != null ? sourceOwner.gameObject : gameObject;
			attackData.AttackerFaction = sourceOwner != null ? sourceOwner.OwnerFaction : Faction.Enemy;
			attackData.Type = DamageType.Ranged;

			return attackData;
		}


		// Distance-based damage falloff
		private float CalculateDamageFalloff(Vector3 origin, Vector3 target) {
			float distanceToHit = Vector3.Distance(origin, target);

			// Damage Falloff
			// 0-20m = 100% damage
			if (distanceToHit <= minigunFullDamageRange) {
				return minigunAttackData.Damage;
			}

			// 20-35m = 75% damage
			if (distanceToHit <= minigunMediumDamageRange) {
				return minigunAttackData.Damage * 0.75f;
			}

			// 35-45m = 50% damage OR 45m+ = 50% damage
			return minigunAttackData.Damage * 0.50f;
		}

		// Visual tracer for minigun bullets
		private void SpawnHitScanTracer(Vector3 start, Vector3 end) {
			if (tracerPool == null) {
				return;
			}

			PooledObject pooledTracer = tracerPool.Get();
			ShieldMinigunTracer tracer = pooledTracer.GetComponent<ShieldMinigunTracer>();

			if (tracer == null) {
				pooledTracer.ReturnToPool();
				Debug.LogWarning($"{name}: Pooled tracer is missing ShieldMinigunTracer.", this);
				return;
			}

			pooledTracer.transform.SetPositionAndRotation(start, Quaternion.identity);
			tracer.Play(start, end, minigunBulletLifetime);
		}

		private float GetHeatPercent() {
			return currentHeatPercent;
		}

		// Updates all assigned minigun heat renderers using the same overheat percentage
		// Uses MaterialPropertyBlock so each renderer can glow independently without creating runtime material instances
		private void UpdateEmission(float heatPercent) {
			if (minigunHeatRenderers == null || minigunHeatRenderers.Length == 0) {
				return;
			}

			if (minigunEmissionBlock == null) {
				minigunEmissionBlock = new MaterialPropertyBlock();
			}

			float smoothedHeat = heatPercent * heatPercent;
			Color emission = Color.Lerp(baseEmissionColor, maxEmissionColor, smoothedHeat);

			for (int i = 0; i < minigunHeatRenderers.Length; i++) {
				Renderer heatRenderer = minigunHeatRenderers[i];

				if (heatRenderer == null) {
					continue;
				}

				// Keep any other property block values already on this renderer
				heatRenderer.GetPropertyBlock(minigunEmissionBlock);

				// Apply the current heat emission colour
				minigunEmissionBlock.SetColor(emissionColorPropertyId, emission);

				heatRenderer.SetPropertyBlock(minigunEmissionBlock);
			}
		}

		private bool HasMinigunOverheated() {
			return currentHeatPercent >= 1.0f;
		}

		private void SetupTracerPool() {
			if (tracerPool != null || minigunBulletTracerPrefab == null) {
				return;
			}

			tracerPool = new ObjectPool<PooledObject>(
				CreateTracer,
				OnTakeTracerFromPool,
				OnReturnTracerToPool,
				OnDestroyTracerFromPool,
				true,
				Mathf.Max(1, defaultTracerPoolCapacity),
				Mathf.Max(1, maxTracerPoolSize)
			);

			if (populateTracerPoolAtStartup) {
				PopulateTracerPool();
			}

		}

		private PooledObject CreateTracer() {
			PooledObject tracerPrefabPooledObject = minigunBulletTracerPrefab.GetComponent<PooledObject>();

			PooledObject pooledTracer = Instantiate(tracerPrefabPooledObject);
			pooledTracer.SetPool(tracerPool);
			pooledTracer.SourcePrefab = tracerPrefabPooledObject;
			pooledTracer.gameObject.SetActive(false);

			return pooledTracer;
		}

		private void PopulateTracerPool() {
			int count = Mathf.Min(Mathf.Max(1, defaultTracerPoolCapacity), Mathf.Max(1, maxTracerPoolSize));

			PooledObject[] borrowedTracers = new PooledObject[count];
			for (int i = 0; i < count; i++) {
				borrowedTracers[i] = tracerPool.Get();
			}

			for (int i = 0; i < borrowedTracers.Length; i++) {
				tracerPool.Release(borrowedTracers[i]);
			}
		}

		private void OnTakeTracerFromPool(PooledObject item) {
			if (item == null) {
				return;
			}

			item.transform.SetParent(null, true);
			item.gameObject.SetActive(true);

			ShieldMinigunTracer tracer = item.GetComponent<ShieldMinigunTracer>();
			tracer?.OnSpawned();
		}

		private void OnReturnTracerToPool(PooledObject item) {
			if (item == null) {
				return;
			}

			ShieldMinigunTracer tracer = item.GetComponent<ShieldMinigunTracer>();
			tracer?.OnDespawned();

			item.transform.SetParent(transform, false);
			item.transform.localPosition = Vector3.zero;
			item.transform.localRotation = Quaternion.identity;
			item.gameObject.SetActive(false);
		}

		private void OnDestroyTracerFromPool(PooledObject item) {
			if (item != null && item.gameObject != null) {
				Destroy(item.gameObject);
			}
		}

		// - DEPRECATED - 

		// Reason: Not fully smoothed but instead snapping with MoveTowards() dampening the effect
		//// Calculates the final aim point used for the minigun shot
		//// Combines delayed tracking + random offset + smoothing
		//private Vector3 GetLaggedAimPoint(Vector3 fallback) {
		//	Vector3 delayedPoint = fallback;

		//	foreach (AimSample sample in minigunAimHistory) {

		//		// Stop once we reach the desired delayed timestamp
		//		if (Time.time - sample.Time >= minigunAimDelay) {
		//			delayedPoint = sample.Position;
		//			break;
		//		}
		//	}

		//	// Apply random offset in a circular area
		//	// This creates a clustered spray instead of perfect tracking
		//	//Vector2 randomOffset = Random.insideUnitCircle * minigunAimOffsetRadius;
		//	//delayedPoint += new Vector3(randomOffset.x, randomOffset.y * 0.35f, randomOffset.y);

		//	// Apply random offset in a circular area
		//	// This creates a clustered spray instead of perfect tracking
		//	delayedPoint += Random.insideUnitSphere * minigunAimOffsetRadius;

		//	if (hasLastMinigunAimPoint) {
		//		delayedPoint = Vector3.MoveTowards(lastMinigunAimPoint, delayedPoint, minigunMaxAimSnapPerShot);
		//	}

		//	Vector3 direction = (delayedPoint - minigunMuzzle.position).normalized;
		//	direction = Quaternion.Euler(
		//		Random.Range(-minigunInaccuracyDegrees, minigunInaccuracyDegrees),
		//		Random.Range(-minigunInaccuracyDegrees, minigunInaccuracyDegrees),
		//		0.0f) * direction;

		//	delayedPoint = minigunMuzzle.position + (direction * minigunMaxRange);

		//	lastMinigunAimPoint = delayedPoint;
		//	hasLastMinigunAimPoint = true;

		//	return delayedPoint;
		//}
	}
}